#!/usr/bin/env python3
"""Crossfade draft loops, normalize volume, and assemble a standard resource pack."""
import argparse
import json
import os
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BPM = dict(zip(['menu','overworld-day','night','underground','jungle-mushroom','snow',
    'desert','ocean','danger','events','bosses','final-boss'], [88,96,72,90,104,80,102,92,112,136,144,152]))


def loop_window(duration, bpm):
    # Preserve an integer number of eight-bar phrases at the requested tempo.
    # Generated tempo/cadence still requires listening; this is not beat tracking.
    fade = 60 / bpm * 2
    phrase = 60 / bpm * 32
    length = int((duration - 0.5 - fade) / phrase) * phrase
    if length < phrase:
        raise ValueError('Track too short for an eight-bar loop')
    return length, fade


def run(command):
    return subprocess.run(command, text=True, capture_output=True, check=True)


def prepare(source, target, bpm, ffmpeg):
    duration = float(run(['ffprobe','-v','error','-show_entries','format=duration',
        '-of','csv=p=0',str(source)]).stdout.strip())
    length, fade = loop_window(duration, bpm)
    frames, fade_frames = round(length * 48000), round(fade * 48000)
    # Integer boundaries prevent a one-sample-short tail from losing the crossfade.
    graph = (f'[0:a]aresample=48000,atrim=start_sample=24000:end_sample={24000+frames+fade_frames},'
        'asetpts=PTS-STARTPTS,asplit=3[h][m][t];'
        f'[h]atrim=end_sample={fade_frames},asetpts=PTS-STARTPTS[head];'
        f'[m]atrim=start_sample={fade_frames}:end_sample={frames},asetpts=PTS-STARTPTS[mid];'
        f'[t]atrim=start_sample={frames},asetpts=PTS-STARTPTS[tail];'
        f'[tail]afade=t=out:ss=0:ns={fade_frames}[tailfade];'
        f'[head]afade=t=in:ss=0:ns={fade_frames}[headfade];'
        '[tailfade][headfade]amix=inputs=2:duration=shortest:normalize=0[seam];'
        '[mid][seam]concat=n=2:v=0:a=1')
    analysis = run([ffmpeg,'-hide_banner','-i',str(source),'-filter_complex',
        graph+',loudnorm=I=-18:TP=-2:LRA=50:print_format=json','-f','null','-']).stderr
    measured = json.loads(analysis[analysis.rfind('\n{'):])
    normalizer = (f",loudnorm=I=-18:TP=-2:LRA=50:linear=true:measured_I={measured['input_i']}:"
        f"measured_TP={measured['input_tp']}:measured_LRA={measured['input_lra']}:"
        f"measured_thresh={measured['input_thresh']}:offset={measured['target_offset']},"
        f"aresample=48000,atrim=end_sample={frames},asetpts=N/SR/TB,"
        f"afade=t=in:ss=0:ns=240,afade=t=out:ss={frames-240}:ns=240")
    temporary = target.with_suffix('.tmp.ogg')
    run([ffmpeg,'-v','error','-y','-i',str(source),'-filter_complex',graph+normalizer,
        '-ac','2','-c:a','libvorbis','-q:a','5',str(temporary)])
    encoded_length = float(run(['ffprobe','-v','error','-show_entries','format=duration',
        '-of','csv=p=0',str(temporary)]).stdout.strip())
    if abs(encoded_length - frames / 48000) > 2 / 48000:
        raise ValueError(f'Crossfade duration mismatch: {target.name}: {encoded_length} vs {frames / 48000}')
    temporary.replace(target)
    return {'source':str(source.relative_to(ROOT)), 'seconds':length,'crossfadeSeconds':fade,
        'boundaryRampSeconds':0.005,'requestedBpm':bpm,'sourceMeasurement':measured,
        'loopReviewed':False,'status':'draft'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=ROOT/'artifacts/samples/music')
    parser.add_argument('--ffmpeg', default=os.environ.get('FFMPEG','ffmpeg'))
    parser.add_argument('--pack-only', action='store_true', help='Package the existing processed OGG files')
    args = parser.parse_args()
    assert loop_window(60,96) == (40.0,1.25)
    try:
        loop_window(2,96)
    except ValueError:
        pass
    else:
        raise AssertionError('short loop accepted')
    mapping_path = ROOT/'assets/music-map.json'
    mapping = json.loads(mapping_path.read_text())
    sources = {theme:args.source/f'{theme}-draft.flac' for theme in mapping['themes']}
    missing = [str(path) for path in sources.values() if not path.is_file()]
    if missing and not args.pack_only:
        raise FileNotFoundError('Missing source themes: '+', '.join(missing))
    music_dir = ROOT/'assets/music'
    music_dir.mkdir(exist_ok=True)
    if not args.pack_only:
        report = {}
        for theme, source in sources.items():
            target = music_dir/f'{theme}.ogg'
            report[theme] = prepare(source, target, BPM[theme], args.ffmpeg)
            mapping['themes'][theme].update(audio=str(target.relative_to(ROOT)), status='draft',loopReviewed=False)
            print('LOOP_READY',theme,report[theme]['seconds'],flush=True)
        (music_dir/'processing.json').write_text(json.dumps(report,indent=2)+'\n')
        mapping_path.write_text(json.dumps(mapping,indent=2)+'\n')
    pack = ROOT/'dist/RiakawaMusic'
    content = pack/'Content/Music'
    content.mkdir(parents=True,exist_ok=True)
    if any((content/f'Music_{i}.ogg').exists() for i in (28,45)):
        raise ValueError('Existing pack overrides preserved ambience')
    for track in mapping['tracks']:
        shutil.copy2(music_dir/f"{track['theme']}.ogg",content/f"Music_{track['id']}.ogg")
    (pack/'pack.json').write_text(json.dumps({'Name':'Riakawa Music',
        'Author':'Riakawa contributors','Description':'12 original AI-assisted themes for Riakawa. '
        'Noncommercial fan project. Includes Otherworld; preserves rain/wind ambience. '
        'Companion pack: https://github.com/westernbear/riakawa', 'Version':{'major':0,'minor':1}},indent=2)+'\n')
    shutil.copy2(ROOT/'docs/CREDITS.md',pack/'CREDITS.md')
    shutil.copy2(ROOT/'Riakawa/icon.png',pack/'icon.png')
    expected = {f"Music_{track['id']}.ogg" for track in mapping['tracks']}
    actual = {path.name for path in content.iterdir()}
    if actual != expected:
        raise ValueError('Unexpected/missing files in music pack')
    approved = all(t['status'] == 'approved' and t['loopReviewed'] for t in mapping['themes'].values())
    name = 'RiakawaMusic' if approved else 'RiakawaMusic-preview'
    shutil.make_archive(str(ROOT/'dist'/name), 'zip', pack)
    print('PACK_READY',pack,len(expected),'tracks;', 'reviewed' if approved else 'draft')


if __name__ == '__main__':
    main()
