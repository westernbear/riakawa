"""Regression boundaries for the pinned runtime weapon export."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
catalog = json.loads((root/'assets/vanilla-catalog.json').read_text())
weapons = {w['id']: w for w in catalog['weapons']}
ammo = {a['id']: a for a in catalog['ammunition']}
assert 949 in weapons and 949 in ammo, 'Throwable Snowball must remain a weapon and ammunition'
assert weapons[949]['shoot'] == ammo[949]['shoot'] == 166
assert 40 in ammo and 40 not in weapons, 'Wooden Arrow has no direct weapon use'
assert 1 not in weapons and 3509 not in weapons, 'Mining tools are outside the agreed scope'
assert 154 in weapons and 1809 in weapons, 'Bone and Rotten Egg are usable thrown weapons'
print(f'PASS: {len(weapons)} weapon candidates; dual-use ammunition retained, mining/ammo-only items excluded')
