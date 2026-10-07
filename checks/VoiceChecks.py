"""Real two-client hurt/death evidence, including receiver-local voice preferences."""
import hashlib
import json
import argparse
from pathlib import Path
from PlaybackChecks import decode, match

root = Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--qa',type=Path,default=root/'artifacts/qa')
parser.add_argument('--characters',nargs='+',default=['Chiikawa','Hachiware','Usagi'])
args=parser.parse_args()
qa = args.qa
build = json.loads((qa/'voice-build.json').read_text())
assert hashlib.sha256((root/build.get('testedPackage','dist/Riakawa.tmod')).read_bytes()).hexdigest() == build['dist/Riakawa.tmod'], 'Voice run is from a different package'
rows = json.loads((qa/'voice-matrix/results.json').read_text())
assert len(rows) == len(args.characters)*4+3, f'Incomplete voice matrix: {len(rows)}'
assert {row['character'] for row in rows}==set(args.characters)|{'Original'}
expected_cases={(c,cue,on,not on) for c in args.characters for cue in ('hurt','death') for on in (True,False)}
expected_cases.update({('Original','hurt',True,True),('Original','death',True,True),
                      (args.characters[0],'mutedhurt',True,True)})
assert {(r['character'],r['cue'],r['localVoices'],r['remoteVoices']) for r in rows}==expected_cases
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
expected_audio={cue for p in manifest['players'] for cue in p['voices'].values()}
for w in manifest['weapons']:
    if w.get('useSound'):expected_audio.add(w['useSound'])
    expected_audio.update(w.get('sounds',{}).values())
    for a in w['attacks']:expected_audio.update(a['sounds'].values())
assert {x['path'] for x in assets['audio']}==expected_audio and all(x['seconds']>0 for x in assets['audio'])
print('PASS: selected hurt/death cues on both clients; per-listener toggle, Original fallback and intentional silence')
print('PASS: outgoing HurtInfo retains native sound flags/damage/knockback; SFX toggle remains independent')
print(f"PASS: {len(assets['loaded'])} packaged textures and {len(assets['audio'])} audio cues load; all {len(assets['cases'])} owner/observer draw contracts")
(qa/'voice-capture-checks.json').write_text(json.dumps(captures,indent=2)+'\n')
print(f'PASS: {len(captures)} real audio-output captures match packaged hurt/death waveforms; listening review remains separate')
