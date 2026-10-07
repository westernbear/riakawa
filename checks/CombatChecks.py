"""Compare deterministic native defaults in isolated tML runs with/without Riakawa."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
qa = root/'artifacts/qa'
native = json.loads((qa/'native-defaults.json').read_text())
modded = json.loads((qa/'riakawa-defaults.json').read_text())
assert not native['riakawaLoaded'] and modded['riakawaLoaded']
assert native['seedBase'] == modded['seedBase'] == 7300
for group, count in [('items',5455),('projectiles',1021)]:
    assert len(native[group]) == len(modded[group]) == count, group
    assert native[group] == modded[group], f'{group} defaults differ'
catalog = json.loads((root/'assets/vanilla-catalog.json').read_text())
assert len(catalog['weapons']) == 426
assert all(str(w['id']) in native['items'] for w in catalog['weapons'])
report = {
    'status':'pass', 'items':5455, 'projectiles':1021, 'weaponCandidates':426,
    'seedBase':7300, 'excludedFields':[],
    'scope':'All public primitive/enum instance defaults, damage classes and projectile frames/whip flags; not live AI, range or collision parity.',
    'artifacts':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [
        qa/'native-defaults.json',qa/'riakawa-defaults.json',root/'dist/Riakawa.tmod',
        root/'.tools/probe-native/Mods/CombatProbe.tmod',root/'.tools/probe-riakawa/Mods/CombatProbe.tmod']}
}
assert report['artifacts']['.tools/probe-native/Mods/CombatProbe.tmod'] == report['artifacts']['.tools/probe-riakawa/Mods/CombatProbe.tmod']
assert hashlib.sha256((root/'.tools/probe-riakawa/Mods/Riakawa.tmod').read_bytes()).hexdigest() == report['artifacts']['dist/Riakawa.tmod']
(qa/'combat-default-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS: all 5455 native item and 1021 projectile defaults unchanged with identical RNG seeds; covers 426 weapon candidates')
