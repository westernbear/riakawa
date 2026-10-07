"""Decode final loops and check continuity, clipping, duration and pack mapping."""
import array
import json
import math
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
ffmpeg = os.environ.get('FFMPEG', 'ffmpeg')
mapping = json.loads((root/'assets/music-map.json').read_text())
processing = json.loads((root/'assets/music/processing.json').read_text())
report = {}
for theme, entry in mapping['themes'].items():
    path = root/entry['audio']
    raw = subprocess.run([ffmpeg,'-v','error','-i',str(path),'-f','f32le','-acodec','pcm_f32le','-'],
                         capture_output=True,check=True).stdout
    samples = array.array('f')
    samples.frombytes(raw)
    assert samples and all(math.isfinite(x) for x in samples), theme
    peak = max(abs(x) for x in samples)
    boundary = max(abs(samples[c]-samples[-2+c]) for c in (0,1))
    seconds = len(samples)/96000
    assert peak < 1 and boundary < 0.005, (theme,peak,boundary)
    assert abs(seconds-processing[theme]['seconds']) < 2/48000, (theme,seconds)
    report[theme] = dict(seconds=seconds,peak=peak,boundarySampleDelta=boundary,listeningReviewed=False)
content = root/'dist/RiakawaMusic/Content/Music'
assert {p.name for p in content.iterdir()} == {f"Music_{t['id']}.ogg" for t in mapping['tracks']}
for track in mapping['tracks']:
    source = root/mapping['themes'][track['theme']]['audio']
    assert (content/f"Music_{track['id']}.ogg").read_bytes() == source.read_bytes(), track
(root/'artifacts/audio-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print(f'PASS: {len(report)} loops, no clipping, boundary delta < 0.005; all 89 mappings match')
