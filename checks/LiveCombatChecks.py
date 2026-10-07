"""Compare complete native combat replays, including every recorded simulation tick."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def first_difference(a, b, path=''):
    if type(a) is not type(b):
        return path, a, b
    if isinstance(a, dict):
        if a.keys() != b.keys():
            return path, list(a), list(b)
        for key in a:
            if a[key] != b[key]:
                return first_difference(a[key], b[key], f'{path}/{key}')
    elif isinstance(a, list):
        if len(a) != len(b):
            return path, len(a), len(b)
        for i, (x, y) in enumerate(zip(a, b)):
            if x != y:
                return first_difference(x, y, f'{path}/{i}')
    elif a != b:
        return path, a, b
    return None


# These native fields contain display/audio state, not combat parameters.
# Source review: docs/qa/combat-cosmetic-fields.json. Keep raw values in traces.
COSMETIC_FIELDS = {
    644: ('ai', 0, 'RainbowCrystalExplosion HSL hue from Main.DiscoR/G/B'),
    700: ('localAI', 1, 'MonkStaffT2Ghast tracked audio SlotId'),
    706: ('localAI', 0, 'DD2PhoenixBowShot tracked audio SlotId'),
    969: ('localAI', 1, 'WeatherPainShot tracked audio SlotId'),
}


def combat_state(row):
    projectiles = []
    for p in row['projectiles']:
        if rule := COSMETIC_FIELDS.get(p['type']):
            field, index, _ = rule
            values = p[field].copy()
            values[index] = None
            p = {**p, field: values}
        projectiles.append(p)
    return {**row, 'projectiles': projectiles}


def compare(native, styled, full):
    a, b = [json.loads((p/'run.json').read_text()) for p in (native, styled)]
    assert not a['running'] and not b['running'], 'Replay is still running'
    assert not a['riakawaLoaded'] and b['riakawaLoaded']
    assert a['seededDamageRolls'] and b['seededDamageRolls']
    assert a['seededItemChecksAndProjectileUpdates'] and b['seededItemChecksAndProjectileUpdates']
    assert a['nativeBiomeRefreshEachTick'] and b['nativeBiomeRefreshEachTick']
    conditions = ('simulationTicksPerCase', 'attackStartTick', 'attackTicks', 'updateBatchSize',
        'perTickSeed', 'seededDamageRolls', 'seededItemChecksAndProjectileUpdates', 'nativeBiomeRefreshEachTick', 'dayTime')
    assert all(a[key] == b[key] for key in conditions), 'Replay conditions differ'
    ids = [v['itemId'] for v in a['completed']]
    assert len(ids) == len(set(ids)) and ids == [v['itemId'] for v in b['completed']]
    if full:
        catalog = json.loads((ROOT/'assets/vanilla-catalog.json').read_text())
        assert set(ids) == {w['id'] for w in catalog['weapons']}
    cases = []
    for item in ids:
        paths = [p/f'{item}.jsonl.gz' for p in (native, styled)]
        runs = [[json.loads(line) for line in gzip.open(p, 'rt')] for p in paths]
        assert all(len(rows) == a['simulationTicksPerCase'] for rows in runs), item
        difference = None
        for tick, (x, y) in enumerate(zip(*runs)):
            assert x['tick'] == y['tick'] == tick
            if (diff := first_difference(combat_state(x), combat_state(y))) is not None:
                difference = {'tick': tick, 'field': diff[0], 'native': diff[1], 'styled': diff[2]}
                break
        assert any(row['player']['itemAnimation'] > 0 for row in runs[0]), (item, 'weapon never used')
        cases.append({'itemId': item, 'ticks': len(runs[0]), 'equal': difference is None,
            'difference': difference, 'nativeTraceSha256': hashlib.sha256(paths[0].read_bytes()).hexdigest(),
            'styledTraceSha256': hashlib.sha256(paths[1].read_bytes()).hexdigest()})
    return {'nativeRun': str(native.relative_to(ROOT)), 'styledRun': str(styled.relative_to(ROOT)),
        'character': b['character'], 'weaponCount': len(ids), 'simulationTicks': sum(c['ticks'] for c in cases),
        'passed': all(c['equal'] for c in cases), 'cases': cases,
        'excludedCosmeticFields': [{'projectileType': kind, 'field': f'{field}[{index}]', 'meaning': meaning}
            for kind, (field, index, meaning) in COSMETIC_FIELDS.items()],
        'conditions': {key: a[key] for key in ('scope', *conditions)}}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('native', type=Path)
    parser.add_argument('styled', type=Path)
    parser.add_argument('--full', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    assert first_difference({'a': [1, 2]}, {'a': [1, 3]}) == ('/a/1', 2, 3)
    assert first_difference({'a': [1, 2]}, {'a': [1, 2]}) is None
    for kind, (field, index, _) in COSMETIC_FIELDS.items():
        a = {'projectiles': [{'type': kind, 'ai': [0, 0, 0], 'localAI': [0, 0, 0], 'damage': 130}]}
        b = json.loads(json.dumps(a))
        b['projectiles'][0][field][index] = .8
        assert first_difference(combat_state(a), combat_state(b)) is None
        assert a['projectiles'][0][field][index] == 0  # Raw evidence is unchanged.
        b['projectiles'][0]['damage'] += 1
        assert first_difference(combat_state(a), combat_state(b))[0] == '/projectiles/0/damage'
        b['projectiles'][0]['damage'] -= 1
        a['projectiles'][0]['type'] = b['projectiles'][0]['type'] = 643
        assert first_difference(combat_state(a), combat_state(b))[0] == f'/projectiles/0/{field}/{index}'
    report = compare(args.native.resolve(), args.styled.resolve(), args.full)
    args.output.write_text(json.dumps(report, indent=2)+'\n')
    failures = [c for c in report['cases'] if not c['equal']]
    assert not failures, failures[:10]
    print(f"PASS: {report['weaponCount']} weapons, {report['simulationTicks']} native simulation ticks ({report['character']})")
