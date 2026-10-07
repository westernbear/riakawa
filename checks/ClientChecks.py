"""Check saved observations from two real clients; does not simulate the game.

Run after capturing emote-one/two.json and split-one/two.json in artifacts/qa.
The recorded build hash identifies the tested package, not necessarily today's build.
"""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
qa = root / 'artifacts/qa'
load = lambda name: json.loads((qa / name).read_text())
build = load('network-build.json')
assert len(build['modSha256']) == 64
one, two = [load(f'emote-{name}.json') for name in ('one', 'two')]
assert one['netMode'] == two['netMode'] == 1
assert one['myPlayer'] != two['myPlayer']
for report in (one, two):
    players = {p['id']: p for p in report['players']}
    assert {p['character'] for p in players.values()} == {'Chiikawa', 'Usagi'}
    assert players[one['myPlayer']]['emote'] > 0, 'Emote missing on a client'
    assert report['emoteKeys'] == ['V']
    for p in players.values():
        assert (p['heldItem'], p['damage'], p['useTime'], p['useAnimation']) == (3507, 5, 13, 13)
assert one['label'] == '캐릭터' and one['koreanGlyphs']
assert two['label'] == 'Character'
projectiles = []
for name in ('one', 'two'):
    report = load(f'split-{name}.json')
    shots = {(p['identity'], p['owner']): p for p in report['projectiles']}
    assert len(shots) == 2, 'Expected a parent and a child test projectile'
    assert all((p['type'], p['weapon'], p['damage'], p['width'], p['height']) ==
               (1, 3507, 1, 10, 10) for p in shots.values())
    projectiles.append(set(shots))
assert projectiles[0] == projectiles[1], 'Projectile ownership differs across clients'
print(f"PASS: two-client characters, emote, locale and child provenance ({build['modSha256'][:12]})")

assert load('remapped-key.json')['emoteKeys'] == ['B']
assert load('unbound-key.json')['emoteKeys'] == [], 'Deliberate unbinding was overwritten'
summons = []
for name in ('one', 'two'):
    report = load(f'minion-{name}.json')
    summon = next(p for p in report['projectiles'] if p['type'] == 266)
    assert (summon['weapon'], summon['damage'], summon['width'], summon['height']) == (1309, 8, 24, 16)
    owner = next(p for p in report['players'] if p['id'] == summon['owner'])
    assert owner['character'] == 'Chiikawa'
    summons.append((summon['identity'], summon['owner']))
assert summons[0] == summons[1]
print('PASS: remap/unbind persistence; native minion source, owner, damage and hitbox on both clients')
for state, character in [('original', 'Original'), ('hachiware', 'Hachiware')]:
    reports = [load(f'{state}-{name}.json') for name in ('one', 'two')]
    for report in reports:
        players = {p['id']: p for p in report['players']}
        assert players[reports[0]['myPlayer']]['character'] == character
        assert players[reports[1]['myPlayer']]['character'] == 'Usagi'
for name in ('one', 'two'):
    beams = [p for p in load(f'prism-{name}.json')['projectiles'] if p['type'] in (632,633)]
    assert sum(p['type'] == 632 for p in beams) == 6
    assert sum(p['type'] == 633 for p in beams) == 1
    assert all(p['weapon'] == 3541 for p in beams)
print('PASS: character restoration/switch sync; prism holdout and all six child beams retain weapon source')

particles = load('heat-ray-particles.json')['particles']
assert particles['LastStyledCount'] > 0 and particles['atlasRestored'] and particles['nativeFramesIntact']
print('PASS: native Heat Ray produces styled particles; shared atlas and native dust frames restored')

before = load('before-late-join.json')
summon = next(p for p in before['projectiles'] if p['type'] == 266)
for name in ('one', 'two'):
    report = load(f'late-join-{name}.json')
    assert report['utc'] > before['utc'] and len(report['players']) == 2
    existing = next(p for p in report['projectiles'] if (p['identity'], p['owner']) ==
                    (summon['identity'], summon['owner']))
    assert (existing['type'], existing['weapon'], existing['damage']) == (266, 1309, 8)
    assert next(p for p in report['players'] if p['id'] == existing['owner'])['character'] == 'Chiikawa'
    sounds = load(f'instrument-{name}.json')
    assert any(s['weaponId'] == 4715 and s['requested'] == 'Terraria/Sounds/Item_134'
               and s['played'].startswith('Riakawa/Assets/Sounds/Weapons/chiikawa-chord134-') for s in sounds)
sounds = load('instrument-one.json')
assert any(s['weaponId'] == 0 and s['requested'] == s['played'] == 'Terraria/Sounds/Item_' for s in sounds)
assert any(s['weaponId'] == 0 and s['requested'] == s['played'] == 'Terraria/Sounds/NPC_Hit_1' for s in sounds)
print('PASS: pre-existing summon survives late join; remote instruments follow owner; unowned/NPC sounds stay native')

for name in ('normal', 'reduced', 'disabled'):
    report = load(f'effects-{name}.json')
    assert report['settings']['ReducedEffects'] == (name == 'reduced')
    assert report['settings']['Decorations'] == (name != 'disabled')
    assert (report['particles']['LastStyledCount'] > 0) == (name != 'disabled')
    assert report['particles']['atlasRestored'] and report['particles']['nativeFramesIntact']
report = load('weapons-disabled.json')
assert not report['settings']['ReplaceWeapons'] and report['particles']['LastStyledCount'] == 0
before = load('emote-moving-before.json')['players'][0]
for name in ('one', 'two'):
    player = next(p for p in load(f'emote-moving-{name}.json')['players'] if p['id'] == before['id'])
    assert player['emote'] > 0 and player['itemAnimation'] > 0
    assert player['position'][0] > before['position'][0]
assert load('gravity-inverted.json')['players'][0]['gravDir'] == -1
swimmer = load('swim-one.json')['players'][0]
assert swimmer['wet'] and not swimmer['dead']
print('PASS: effect toggles, moving/attacking emote sync, inverted gravity and native water state')

for casts, body in [(1, 833), (4, 834), (7, 835)]:
    report = load(f'tiger-tier-{casts}.json')
    assert any(p['type'] == body and p['weapon'] == 4607 for p in report['projectiles'])
    assert all(p['weapon'] == 0 for p in report['projectiles'] if p['type'] == 623), 'Armor summon was reskinned'
report = load('tiered-after-weapon-switch.json')
assert report['players'][0]['heldItem'] == 3507
assert {(p['type'],p['weapon']) for p in report['projectiles']} >= {(835,4607),(963,5114)}
for file, expected in [('wisdom-alt.json',(704,3852)), ('staff-alt.json',(708,3858)),
                       ('charged-blaster.json',(461,2882))]:
    assert expected in {(p['type'],p['weapon']) for p in load(file)['projectiles']}
print('PASS: all tiger tiers/Abigail retain staff source, armor summon excluded, alternate/full-charge branches recorded')

shader_draws = load('client-releasecheck-one.shader-draws.json')
assert {s['weaponId'] for s in shader_draws} == {113, 218, 495, 561, 4956, 5005}
assert all(s['texture'].startswith('Assets/Attacks/binding-chiikawa-') for s in shader_draws)
for name in ('one','two'):
    report = load(f'custom-tiger-join-{name}.json')
    assert report['utc'] > load('custom-tiger-before-join.json')['utc']
    assert report['settings']['ReplaceWeapons']
    assert any(p['type'] == 833 and p['weapon'] == 4607 and p['owner'] == 0 for p in report['projectiles'])
report = load('sfx-disabled.json')
assert report['settings']['ReplaceWeapons'] and not report['settings']['SoundEffects']
sounds = [s for s in load('sfx-toggle-playback.json') if s['weaponId'] == 1295 and s['requested'] == 'Terraria/Sounds/Item_12']
assert any(not s['enabled'] and s['played'] == s['requested'] for s in sounds)
assert any(s['enabled'] and s['played'].startswith('Riakawa/') for s in sounds)
print('PASS: six native shaders receive custom images; custom tiger joins remotely; SFX toggle is independent of visuals')

shards = []
for name in ('one','two'):
    report = load(f'crystal-multi-{name}.json')
    assert report['settings']['ReplaceWeapons']
    assert {p['id']:p['character'] for p in report['players']} == {0:'Chiikawa',1:'Usagi'}
    shards.append({(p['identity'],p['owner']) for p in report['projectiles']
                   if p['type'] == 90 and p['weapon'] == 95 and p['owner'] == 0})
assert shards[0] & shards[1], 'Both clients must see the same crystal-bullet child'
print('PASS: current preview package loads on two clients; crystal-bullet children retain the firing weapon/owner')
