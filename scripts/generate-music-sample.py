"""YuE2 BGM generation. --all produces the twelve themes after sample approval."""
import argparse
import json
from pathlib import Path
from huggingface_hub import HfApi
from yue2 import YuE2Pipeline

parser = argparse.ArgumentParser()
parser.add_argument('--all', action='store_true')
args = parser.parse_args()
out = Path('/workspace/riakawa/outputs/music')
out.mkdir(parents=True, exist_ok=True)
base = ('Instrumental cozy adventure game background music. No vocals, no singing, no spoken words. '
    'Soft marimba and music box melody, gentle pizzicato strings, acoustic bass, brushed percussion. '
    'Clear short repeating melody with an eight bar phrase, even volume, no dramatic ending. ')
themes = {
    'menu': 'Welcoming small magical world. Celesta, warm strings and gentle bells. C major, 88 BPM.',
    'overworld-day': 'Warm playful miniature world, friendly and a little shy. C major, 96 BPM.',
    'night': 'Peaceful moonlit village. Sparse music box, muted vibraphone, soft bass. F major, 72 BPM.',
    'underground': 'Curious cave exploration. Hollow marimba, droplets and plucked strings. D dorian, 90 BPM.',
    'jungle-mushroom': 'Whimsical glowing forest. Playful flute, mallets and light hand percussion. G major, 104 BPM.',
    'snow': 'Quiet snowy hills. Delicate celesta, soft bells, warm strings. D major, 80 BPM.',
    'desert': 'Sunny desert expedition. Plucked strings, wooden flute and light hand drums. A minor, 102 BPM.',
    'ocean': 'A warm sparkling shoreline. Gentle ukulele, marimba and a swaying bassline. F major, 92 BPM.',
    'danger': 'Cute characters bravely exploring a haunted cave. Minor music box, tense pizzicato ostinato, '
        'low strings, restrained percussion. E minor, 112 BPM. Suspenseful but playful, no harsh horror.',
    'events': 'A frantic miniature festival adventure. Nimble marimba, pizzicato strings, playful brass, '
        'rolling drums. G major, 136 BPM. Busy and exciting, clear melody.',
    'bosses': 'A courageous tiny hero battle. Fast marimba, urgent strings, timpani and crisp drums. '
        'A minor, 144 BPM. Cute melodic motifs with real tension, no vocals or choir.',
    'final-boss': 'A climactic cosmic battle for a tiny world. Music box motif over driving orchestral strings, '
        'brass and powerful drums. D minor, 152 BPM. Hopeful heroic answer phrases, intense but charming.',
}
revisions = {name: HfApi().model_info(name).sha for name in ('m-a-p/YuE2-3B', 'm-a-p/YuE2-Vae')}
pipe = YuE2Pipeline.from_pretrained('m-a-p/YuE2-3B', revision=revisions['m-a-p/YuE2-3B'],
    vae_revision=revisions['m-a-p/YuE2-Vae'], device='cuda')
try:
    for index, (theme, mood) in enumerate(themes.items()):
        if not args.all and theme != 'overworld-day':
            continue
        path = out / f'{theme}-draft.flac'
        if path.exists():
            continue
        seed = 42 if theme == 'overworld-day' else 4200 + index
        settings = {'style': base + mood, 'lyrics': '[Instrumental]\n[Instrumental]', 'cot': 'off',
            'seed': seed, 'semantic_sampling': {'max_tokens': 1500 if theme == 'overworld-day' else 2250}}
        song = pipe(**settings)
        song.save(str(path))
        song.save_artifacts(str(out / f'{theme}-settings'))
        path.with_suffix('.json').write_text(json.dumps({'models': revisions, 'settings': settings,
            'status': 'draft', 'license': 'CC-BY-NC-4.0', 'loopReviewed': False}, indent=2))
        print('MUSIC_THEME_READY', theme, flush=True)
finally:
    pipe.close()
print('MUSIC_BATCH_READY' if args.all else 'MUSIC_SAMPLE_READY', flush=True)
