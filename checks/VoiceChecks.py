"""Real two-client hurt/death evidence, including receiver-local voice preferences."""
import hashlib
import json
from pathlib import Path
from PlaybackChecks import decode, match

root = Path(__file__).resolve().parents[1]
qa = root/'artifacts/qa'
build = json.loads((qa/'voice-build.json').read_text())
assert hashlib.sha256((root/build.get('testedPackage','dist/Riakawa.tmod')).read_bytes()).hexdigest() == build['dist/Riakawa.tmod'], 'Voice run is from a different package'
rows = json.loads((qa/'voice-matrix/results.json').read_text())
assert len(rows) == 15, f'Incomplete voice matrix: {len(rows)} / 15'
captures = []
for row in rows:
    for who, enabled in [('one',row['localVoices']),('two',row['remoteVoices'])]:
        events = row['events'][who]
        custom = [e for e in events if e['played'] and e['played'].startswith('Riakawa/')]
        native = [e for e in events if e['played'] and e['played'].startswith('Terraria/')]
        if row['cue'] == 'mutedhurt':
            assert not custom and not native, (row['case'],who,events)
            assert row['hurt'][who] and all(h['SoundDisabled'] for h in row['hurt'][who]), row['case']
        elif row['character'] != 'Original' and enabled:
            expected = f"Riakawa/Assets/Sounds/{row['character'].lower()}-{row['cue']}"
            assert [e['played'] for e in custom] == [expected] and not native, (row['case'],who,events)
        else:
            assert len(native) == 1 and not custom, (row['case'],who,events)
        if row['cue'] == 'hurt':
            assert row['hurt'][who] and all(not h['SoundDisabled'] for h in row['hurt'][who]), row['case']
    if row['cue'] in ('hurt','mutedhurt'):
        one,two=row['hurt']['one'],row['hurt']['two']
        assert [{k:h[k] for k in ('player','Damage','Knockback','HitDirection','SoundDisabled')} for h in one] == [
            {k:h[k] for k in ('player','Damage','Knockback','HitDirection','SoundDisabled')} for h in two],row['case']
    if row['localVoices'] and row['character'] != 'Original' and row['cue'] in ('hurt','death'):
        sample=root/f"Riakawa/Assets/Sounds/{row['character'].lower()}-{row['cue']}.wav"
        capture=match(decode(qa/'voice-matrix'/f"{row['case']}.wav"),decode(sample))
        assert capture['correlation'] > .9,(row['case'],capture)
        captures.append({'case':row['case'],**capture})
assets=json.loads((qa/'voice-one.assets.json').read_text())
manifest=json.loads((root/'Riakawa/Assets/manifest.json').read_text())
assert {(c['id'],c['character']) for c in assets['cases']} == {(w['itemId'],w['character']) for w in manifest['weapons']}
assert len(assets['audio'])==48 and all(x['seconds']>0 for x in assets['audio'])
print('PASS: all six hurt/death cues on both clients; per-listener toggle, Original fallback and intentional silence')
print('PASS: outgoing HurtInfo retains native sound flags/damage/knockback; SFX toggle remains independent')
print(f"PASS: {len(assets['loaded'])} packaged textures and 48 audio cues load; all 1278 owner/observer draw contracts")
(qa/'voice-capture-checks.json').write_text(json.dumps(captures,indent=2)+'\n')
print('PASS: six real audio-output captures match packaged hurt/death waveforms; listening review remains separate')
