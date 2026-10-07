"""Original creature voices. Combat cues reuse our own synthetic emote references."""
import argparse
import hashlib
import json
from pathlib import Path
import soundfile as sf
import torch
from qwen_tts import Qwen3TTSModel

parser = argparse.ArgumentParser()
parser.add_argument('--combat', action='store_true')
parser.add_argument('--references', type=Path)
parser.add_argument('--output', type=Path, default=Path('/workspace/riakawa/outputs/voices'))
args = parser.parse_args()
if args.combat and not args.references:
    parser.error('--combat requires --references containing our synthetic emote WAV/JSON pairs')
name = 'Qwen/Qwen3-TTS-12Hz-1.7B-' + ('Base' if args.combat else 'VoiceDesign')
revision = 'fd4b254389122332181a7c3db7f27e918eec64e3' if args.combat else '5ecdb67327fd37bb2e042aab12ff7391903235d3'
model = Qwen3TTSModel.from_pretrained(name, revision=revision, device_map='cuda:0',
    dtype=torch.bfloat16, attn_implementation='sdpa')
out = args.output
out.mkdir(parents=True, exist_ok=True)
voices = {
    'chiikawa': ('わーい！', 'A very small cute fictional creature with a soft, high-pitched, slightly breathy voice. '
        'Shy and gentle, then a short happy squeak. Clear Japanese, light and tender, no background sound.'),
    'hachiware': ('なんとかなれー！', 'A tiny cheerful fictional creature with a bright, clear, high-pitched youthful voice. '
        'Friendly, optimistic and brave. A short excited Japanese exclamation, no background sound.'),
    'usagi': ('ヤハ！', 'A tiny mischievous fictional creature with a high-pitched, nasal and energetic squeaky voice. '
        'Wildly enthusiastic, bouncy, playful and loud but not harsh. A quick Japanese exclamation, no background sound.'),
}
combat = {'chiikawa': {'hurt': 'あっ！', 'death': 'うう…'},
          'hachiware': {'hurt': 'いたっ！', 'death': 'ああ…'},
          'usagi': {'hurt': 'ウラッ！', 'death': 'ううう…'}}
for index, (character, (emote, description)) in enumerate(voices.items()):
    reference = {}
    if args.combat:
        ref = args.references / f'{character}-emote-draft.wav'
        meta = json.loads(ref.with_suffix('.json').read_text())
        assert meta['model'] == 'Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign', 'Only our synthetic references are accepted'
        prompt = model.create_voice_clone_prompt(ref_audio=str(ref), ref_text=meta['text'], x_vector_only_mode=False)
        reference = {'reference': ref.name, 'referenceSha256': hashlib.sha256(ref.read_bytes()).hexdigest(),
                     'referenceModel': meta['model'], 'referenceRevision': meta['revision']}
    for cue, text in (combat[character] if args.combat else {'emote': emote}).items():
        seed = 420 + index + (10 if cue == 'hurt' else 20 if cue == 'death' else 0)
        torch.manual_seed(seed)
        if args.combat:
            wavs, rate = model.generate_voice_clone(text=text, language='Japanese', voice_clone_prompt=prompt,
                                                   max_new_tokens=128)
        else:
            wavs, rate = model.generate_voice_design(text=text, language='Japanese', instruct=description,
                                                     max_new_tokens=256)
        path = out / f'{character}-{cue}-draft.wav'
        sf.write(path, wavs[0], rate, subtype='PCM_16')
        path.with_suffix('.json').write_text(json.dumps({'model': name, 'revision': revision, 'text': text,
            'instruct': description if not args.combat else None, 'seed': seed, 'sampleRate': rate,
            'status': 'draft', **reference}, indent=2, ensure_ascii=False) + '\n')
        print('VOICE_READY', character, cue, len(wavs[0])/rate, flush=True)
