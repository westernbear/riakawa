"""Actual game music selection, native fades and captured output; not a listening review."""
import json
from pathlib import Path
import numpy as np
from PlaybackChecks import decode, match, rate

root=Path(__file__).resolve().parents[1]
qa=root/'artifacts/qa/music-matrix'
rows=json.loads((qa/'results.json').read_text())
expected={'day':{1,18},'night':{3},'event':{30},'boss':{5},'final':{38},
    'otherworld-final':{84},'otherworld-day':{63},'otherworld-night':{64},'music-box':{6},'volume-muted':{0}}
assert {row['case'] for row in rows}==set(expected) and len(rows)==len(expected)
mapping=json.loads((root/'assets/music-map.json').read_text())
results=[]
for row in rows:
    case=row['case'];music=row['state']['music'];track=music['curMusic']
    assert track in expected[case],(case,music)
    capture=decode(qa/f'{case}.wav')
    if case=='volume-muted':
        peak=float(np.max(np.abs(capture)))
        assert music['musicVolume']==0 and peak<.0001,(case,peak)
        results.append({'case':case,'peak':peak});continue
    assert music['musicVolume']==.5
    assert any(t['id']==track and t['type']=='OGGAudioTrack' and t['fade']>.99 for t in music['tracks']),(case,music)
    theme=next(t['theme'] for t in mapping['tracks'] if t['id']==track)
    source=decode(root/mapping['themes'][theme]['audio'])
    # Identify the track despite native sounds mixed into the game output.
    # Retain every window; two matches do not certify dropout-free playback.
    scores=[{'captureSeconds':start,**match(source,capture[start*rate:(start+1)*rate])}
        for start in range(1,7)]
    assert sum(s['correlation']>.97 for s in scores)>=2,(case,scores)
    if case in ('night','event','boss','final','otherworld-final','otherworld-day','otherworld-night'):
        assert any(len(s['tracks'])>=2 for s in row['transition']),(case,'crossfade not observed')
    results.append({'case':case,'track':track,'theme':theme,'windows':scores})
(qa/'checks.json').write_text(json.dumps({'results':results,'listeningReviewed':False,
    'uninterruptedPlaybackReviewed':False,
    'bossAI':'frozen for music-priority inspection; not a combat test'},indent=2)+'\n')
print('PASS: day/night/event/boss/final, Otherworld, music box and volume-zero output; native fades and nine captured track matches')
