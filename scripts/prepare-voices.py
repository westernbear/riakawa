#!/usr/bin/env python3
"""Normalize generated cues for tModLoader; listening approval stays separate."""
import json
import os
import subprocess
import wave
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
manifest_path = ROOT/'Riakawa/Assets/manifest.json'
manifest = json.loads(manifest_path.read_text())
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--characters', nargs='+')
args = parser.parse_args()
if args.characters and set(args.characters)-{p['character'].lower() for p in manifest['players']}:
    parser.error('Unknown character')
count = 0
for player in manifest['players']:
    name = player['character'].lower()
    if args.characters and name not in args.characters:
        continue
    for cue, limit in [('emote', 5), ('hurt', 1.5), ('death', 3.5)]:
        source = ROOT/f'artifacts/samples/voices/{name}-{cue}-draft.wav'
        asset = f'Assets/Sounds/{name}-{cue}'
        target = ROOT/'Riakawa'/f'{asset}.wav'
        filters = 'loudnorm=I=-18:TP=-2:LRA=11,afade=t=in:d=0.005'
        if cue != 'emote':
            # Trim only outer silence; preserve pauses and the complete spoken cue.
            filters = ('silenceremove=start_periods=1:start_duration=0.01:start_threshold=-45dB:start_silence=0.02,'
                       'areverse,silenceremove=start_periods=1:start_duration=0.01:start_threshold=-45dB:start_silence=0.06,'
                       'areverse,' + filters + ',areverse,afade=t=in:d=0.01,areverse')
        subprocess.run([os.environ.get('FFMPEG','ffmpeg'),'-nostdin','-v','error','-y','-i',str(source),
            '-af',filters,'-ar','44100','-ac','1','-c:a','pcm_s16le',str(target)],check=True)
        with wave.open(str(target)) as audio:
            assert (audio.getframerate(),audio.getnchannels(),audio.getsampwidth()) == (44100,1,2)
            assert 0 < audio.getnframes()/44100 < limit, f'{name}/{cue} unexpectedly long'
        player['voices'][cue] = asset
        count += 1
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print(f'PASS: {count} voice cues, PCM16 mono 44.1 kHz; hurt <1.5s, death <3.5s, emote <5s')
