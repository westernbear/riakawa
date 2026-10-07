"""Compare recorded native uses; limited to four weapons, not full combat parity."""
import json
from pathlib import Path

qa = Path(__file__).resolve().parents[1]/'artifacts/qa'
rows = json.loads((qa/'use-comparison.json').read_text())
results = {}
for row in rows:
    before, after = [row[key]['players'][0] for key in ('before','after')]
    assert before['character'] == after['character'] == row['character']
    assert not before['dead'] and not after['dead']
    arrows = lambda p: sum(a['stack'] for a in p['ammunition'] if a['type'] == 40)
    results[row['character'],row['itemId']] = (
        after['weaponDamage'], after['useTime'], after['useAnimation'], after['itemAnimationMax'],
        before['heldStack'] - after['heldStack'], arrows(before) - arrows(after))
for item in (3507,39,739,949):
    assert results['Original',item] == results['Chiikawa',item], item
assert results['Chiikawa',39][-1] == 1
assert results['Chiikawa',949][-2] == 1
for row in rows:
    if row['itemId'] in (39,739):
        expected = (1,9,10,10) if row['itemId'] == 39 else (121,15,10,10)
        assert expected in {(p['type'],p['damage'],p['width'],p['height']) for p in row['after']['projectiles']}
payments = json.loads((qa/'client-mana-one.mana.json').read_text())
for character in ('Original','Chiikawa'):
    used = [p for p in payments if p['character'] == character and p['itemId'] == 739]
    assert used and all(p['before']-p['after'] == 5 for p in used), (character,used)
print('PASS: four native-use comparisons retain damage/use duration; arrow/throwable counts and actual mana payments match')
print('PASS: observed arrow/staff projectile damage and hitboxes match; range/knockback/all-weapon parity remains untested')
