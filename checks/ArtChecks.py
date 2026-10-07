"""Pinned weapon scope, recorded attack coverage, frame geometry and native pose regression."""
import json
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parents[1]
read=lambda name:json.loads((root/name).read_text())
m=read('Riakawa/Assets/manifest.json');catalog=read('assets/vanilla-catalog.json')
weapons={(w['itemId'],w['character']):w for w in m['weapons']}
characters=['Chiikawa','Hachiware','Usagi']
native=read('artifacts/qa/native-defaults.json')['items']
expected={int(id) for id,row in native.items() if (f:=row['fields'])['damage']>0 and
    f['useStyle']>0 and f['pick']==f['axe']==f['hammer']==0 and f['createTile']==-1 and not f['accessory']}
# Pinned ItemID.Sets.Deprecated: unused Boring Bow and First Fractal prototypes.
expected-={4058,4722}
assert expected=={w['id'] for w in catalog['weapons']},'Runtime weapon scope differs'
assert set(weapons)=={(id,char) for id in expected for char in characters}
projectiles={p['id']:p for p in catalog['projectiles']}
paths={};frames=0
for w in m['weapons']:
    paths[w['texture']]=('Item',w['itemId'])
    for b in w.get('heldBindings',[]):paths[b['texture']]=(b['slot'],b['id'])
    for a in w['attacks']:
        if a['texture']:paths[a['texture']]=('Projectile',a['projectileId'])
        else:
            reference=Image.open(root/f"artifacts/qa/reference-textures/Projectile_{a['projectileId']}.png").convert('RGBA')
            assert a.get('visualMode')=='particles' or a['bindings'],(w['itemId'],a['projectileId'])
            assert reference.getchannel('A').getextrema()[1]==0
        for b in a['bindings']:paths[b['texture']]=(b['slot'],b['id'])
alpha_checked=0
for path,(slot,id) in paths.items():
    art=Image.open(root/'Riakawa'/f'{path}.png').convert('RGBA')
    source=Image.open(root/f'artifacts/qa/reference-textures/{slot}_{id}.png').convert('RGBA')
    assert art.size==source.size,(path,art.size,source.size)
    if '/batch-' in path or '/binding-' in path or '/held-' in path:
        assert art.getchannel('A').tobytes()==source.getchannel('A').tobytes(),path
        alpha_checked+=1
    if slot=='Projectile':
        count=5 if projectiles[id]['whip'] else max(1,projectiles[id]['frames'])
        assert art.height//count>0,path
        frames+=count
attacks={(id,char):{a['projectileId']:a for a in w['attacks']} for (id,char),w in weapons.items()}
edges=read('assets/attack-observations.json')['edges']
for e in edges:
    for char in characters:assert e['projectileId'] in attacks[e['weaponId'],char],e
for e in catalog['ammoProjectiles']:
    for char in characters:assert e['projectileId'] in attacks[e['weaponId'],char],e
bindings=read('assets/attack-bindings.json')['bindings']
for b in bindings:
    for char in characters:
        a=attacks[b['weaponId'],char][b['projectileId']]
        if b['slot']=='Projectile' and b['id']==b['projectileId']:
            assert a['texture'] or a.get('visualMode')=='particles' or a['bindings'];continue
        assert any(x['slot']==b['slot'] and x['id']==b['id'] for x in a['bindings']),(b,char)
poses=read('artifacts/qa/poses/results.json')
assert {p['itemId'] for p in poses}==expected, f'Incomplete native pose run: {len(poses)}/{len(expected)}'
cell=Image.open(root/f"artifacts/qa/poses/item-{poses[0]['itemId']}.png").height//4
for row in poses:
    assert len(row['poses'])==48,row['itemId']
    for pose in row['poses']:
        native_pose=next(p for p in row['poses'] if p['character']=='Original' and p['orientation']==pose['orientation'] and p['phase']==pose['phase'])
        shift=(0 if pose['character']=='Original' else characters.index(pose['character'])+1)*cell
        geometry=lambda p,dy:[(x.get('binding'),x['source'],round(x['x'],3),round(x['y']-dy,3),round(x['rotation'],5),x['effects']) for x in p['layers'] if x['held'] or x.get('binding')]
        assert geometry(native_pose,0)==geometry(pose,shift),(row['itemId'],pose['character'],pose['orientation'],pose['phase'])
report={'weaponScope':len(expected),'itemDesigns':len(weapons),'textureCanvases':len(paths),'exactAlphaSheets':alpha_checked,
    'primaryAnimationFrames':frames,'observedSpawnEdges':len(edges),'observedBindings':len(bindings),
    'ammoMappings':len(catalog['ammoProjectiles']),'nativePoseCases':sum(len(p['poses']) for p in poses),
    'heldGeometryUnchanged':True,'scope':'File/frame geometry and native pose pipeline; live attack rendering is checked separately'}
(root/'artifacts/qa/art-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS:',report)
