"""Verify recorded Momonga scenarios from two real game clients."""
import hashlib
import json
from pathlib import Path

root=Path(__file__).resolve().parents[1]
qa=root/'artifacts/qa/momonga'
load=lambda name:json.loads((qa/f'{name}.json').read_text())
build=load('network-build')
assert hashlib.sha256((root/'dist/Riakawa.tmod').read_bytes()).hexdigest()==build['modSha256']
one,two=[load(f'emote-{c}') for c in ('one','two')]
owner=one['myPlayer'];observer=two['myPlayer']
assert owner!=observer
for d in (one,two):
    assert d['netMode']==1 and d['emoteKeys']==['V']
    players={p['id']:p for p in d['players']}
    assert players[owner]['character']=='Momonga' and players[observer]['character']=='Usagi'
    assert players[owner]['emote']>0
assert one['characterLabel']=='모몽가' and one['emoteLabel']=='칭찬해라!' and one['koreanGlyphs']
for c in ('one','two'):
    players=load(f'same-weapon-{c}')['players']
    assert all(p['heldItem']==989 and p['damage']==23 and p['useTime']==45 for p in players)
    d=load(f'original-{c}')
    assert next(p for p in d['players'] if p['id']==owner)['character']=='Original'
    d=load(f'emote-moving-{c}')
    p=next(p for p in d['players'] if p['id']==owner)
    before=next(p for p in load('emote-moving-before-one')['players'] if p['id']==owner)
    assert p['emote']>0 and p['itemAnimation']>0 and p['position'][0]>before['position'][0]
shots=[]
for c in ('one','two'):
    shots.append({(p['identity'],p['owner']) for p in load(f'split-{c}')['projectiles']
                  if (p['type'],p['weapon'],p['damage'],p['width'],p['height'])==(1,39,1,10,10)})
assert len(shots[0]&shots[1])==2
before=load('before-late-join-one')
summon=next(p for p in before['projectiles'] if p['type']==266 and p['weapon']==1309)
for c in ('one','two'):
    d=load(f'late-join-{c}')
    assert d['utc']>before['utc'] and len(d['players'])==2
    p=next(p for p in d['projectiles'] if (p['identity'],p['owner'])==(summon['identity'],summon['owner']))
    assert (p['type'],p['weapon'],p['damage'],p['width'],p['height'])==(266,1309,8,24,16)
    assert next(x for x in d['players'] if x['id']==p['owner'])['character']=='Momonga'
for mode in ('normal','reduced','disabled'):
    d=load(f'effects-{mode}-one')
    assert d['settings']['ReducedEffects']==(mode=='reduced')
    assert d['settings']['Decorations']==(mode!='disabled')
    assert (d['particles']['LastStyledCount']>0)==(mode!='disabled')
    assert d['particles']['atlasRestored'] and d['particles']['nativeFramesIntact']
d=load('weapons-disabled-one')
assert not d['settings']['ReplaceWeapons'] and d['particles']['LastStyledCount']==0
assert next(p for p in load('gravity-one')['players'] if p['id']==owner)['gravDir']==-1
assert next(p for p in load('invisible-one')['players'] if p['id']==owner)['invis']
assert next(p for p in load('swim-one')['players'] if p['id']==owner)['wet']
body=load('one.body-checks')
assert {'walk','mount','jump','swim','invisible'}<={r['expected'] for r in body if r['character']=='Momonga'}
report={'status':'pass',**build,'characters':['Momonga','Usagi'],
        'scenarios':['V emote','same weapon','moving/attacking emote','Original restoration',
                     'child projectile ownership','existing summon on late join','effect toggles',
                     'walk','mount','jump','swim','inverted gravity','invisibility']}
(qa/'network-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS:',', '.join(report['scenarios']))
