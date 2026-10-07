"""Check generated weapon effects and their real manifest mappings; no listening claim."""
import array
import hashlib
import json
import wave
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root/'Riakawa/Assets/manifest.json').read_text())
catalog = {w['id']: w for w in json.loads((root/'assets/vanilla-catalog.json').read_text())['weapons']}
cues = json.loads((root/'assets/weapon-sounds.json').read_text())['cues']
assets = {c['asset'] for c in cues}
hashes = set()
chords = {}
for cue in cues:
    path = root/'Riakawa'/(cue['asset']+'.wav')
    with wave.open(str(path)) as f:
        assert (f.getnchannels(), f.getframerate(), f.getsampwidth()) == (1,44100,2), path
        samples = array.array('h', f.readframes(f.getnframes()))
    assert 0.08 < len(samples)/44100 < 0.6, path
    assert 0.05 < max(abs(x) for x in samples)/32768 < 0.9, path
    assert abs(samples[0]) < 100 and abs(samples[-1]) < 100, path
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest not in hashes, f'Duplicate generated cue: {path}'
    hashes.add(digest)
    if cue.get('notes'):
        assert len(cue['notes']) == 3 and cue['notes'][0] < cue['notes'][1] < cue['notes'][2]
        chords.setdefault(cue['character'],set()).add(cue['family'])
assert all(chords.get(c) == {f'chord{i}' for i in range(133,139)} for c in ['Chiikawa','Hachiware','Usagi'])
for weapon in manifest['weapons']:
    if catalog[weapon['itemId']]['sound']:
        assert weapon['useSound'] in assets, weapon['itemId']
    for source, replacement in weapon.get('sounds',{}).items():
        assert 'NPC_' not in source and replacement in assets, (source,replacement)
    for attack in weapon['attacks']:
        for source, replacement in attack['sounds'].items():
            assert ('NPC_' not in source or source.endswith(('NPC_Killed_17','NPC_Killed_19'))) and replacement in assets, (source,replacement)
print(f'PASS: {len(cues)} distinct PCM effects, bounded peaks/duration/edges; all native use cues mapped')

voices = []
for player in manifest['players']:
    assert set(player['voices']) == {'emote', 'hurt', 'death'}, player['character']
    for cue, asset in player['voices'].items():
        path = root/'Riakawa'/(asset+'.wav')
        with wave.open(str(path)) as f:
            assert (f.getnchannels(), f.getframerate(), f.getsampwidth()) == (1,44100,2), path
            samples = array.array('h', f.readframes(f.getnframes()))
        duration = len(samples)/44100
        peak = max(abs(x) for x in samples)/32768
        assert 0.12 < duration < {'hurt': 1.5, 'death': 3.5, 'emote': 5}[cue], path
        assert 0.05 < peak < 0.95, path
        if cue != 'emote':
            assert abs(samples[0]) < 100 and abs(samples[-1]) < 100, path
            raw = root/f'artifacts/samples/voices/{player["character"].lower()}-{cue}-draft.json'
            meta = json.loads(raw.read_text())
            reference = raw.parent/meta['reference']
            assert hashlib.sha256(reference.read_bytes()).hexdigest() == meta['referenceSha256'], raw
            assert meta['referenceModel'].endswith('-VoiceDesign'), raw
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest not in hashes, path
        hashes.add(digest)
        voices.append({'character': player['character'], 'cue': cue, 'seconds': duration, 'peak': peak, 'sha256': digest})
(root/'artifacts/voice-checks.json').write_text(json.dumps(voices, indent=2)+'\n')
print('PASS: nine unique voice cues, short hurt/death, synthetic reference provenance and clean PCM edges')
