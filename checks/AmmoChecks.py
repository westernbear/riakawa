"""Verify native ammo discovery and shared child mappings; inferred coverage is not art approval."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
catalog = json.loads((root/'assets/vanilla-catalog.json').read_text())
manifest = json.loads((root/'Riakawa/Assets/manifest.json').read_text())
run = json.loads((root/'artifacts/qa/ammo-sweep/sweep.json').read_text())
representatives = {}
for row in catalog['ammoProjectiles']:
    representatives.setdefault(row['projectileId'],(row['weaponId'],row['ammoId']))
assert not run['running'] and run['ammoSweep']
assert len(run['completed']) == len(representatives) == 114
assert {(r['itemId'],r['ammoId']) for r in run['completed']} == set(representatives.values())
assert all(r['observedProjectiles'] for r in run['completed'])

art = {(w['itemId'],w['character']):w for w in manifest['weapons']}
children = {89:90, 91:92, 478:480, 639:640, 776:779, 777:779, 778:779,
            780:783, 781:783, 782:783, 803:862, 804:863}
checked = 0
for ammo in catalog['ammoProjectiles']:
    child = children.get(ammo['projectileId'])
    if child is None: continue
    for character in ('Chiikawa','Hachiware','Usagi'):
        attack = next(a for a in art[ammo['weaponId'],character]['attacks'] if a['projectileId'] == child)
        assert attack['texture'] or attack['bindings'] or attack.get('visualMode') == 'particles'
        checked += 1
print(f'PASS: 114 native ammo cases observed; {checked} cross-weapon child mappings cover known splits and fragments')
