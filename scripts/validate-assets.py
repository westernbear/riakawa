#!/usr/bin/env python3
"""One asset gate: draft permits pending work; --release requires reviewed coverage."""
import argparse
import hashlib
import json
import subprocess
from functools import cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHARACTERS = {'Chiikawa', 'Hachiware', 'Usagi'}
ANIMATIONS = {'idle', 'walk', 'jump', 'swim', 'attack', 'mount', 'hurt', 'death', 'emote'}
THEMES = {'menu', 'overworld-day', 'night', 'underground', 'jungle-mushroom', 'snow',
          'desert', 'ocean', 'danger', 'events', 'bosses', 'final-boss'}


def no_duplicate_keys(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f'duplicate JSON key: {key}')
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(), object_pairs_hook=no_duplicate_keys)


def asset_path(base, name, suffix=''):
    if not isinstance(name, str) or not name or '\\' in name:
        raise ValueError(f'invalid asset path: {name!r}')
    relative = Path(name)
    if relative.is_absolute() or '..' in relative.parts:
        raise ValueError(f'asset path escapes root: {name}')
    path = (base / (name + suffix)).resolve()
    if not path.is_relative_to(base.resolve()):
        raise ValueError(f'asset symlink escapes root: {name}')
    if not path.is_file():
        raise ValueError(f'missing asset: {path.relative_to(ROOT)}')
    return path


def png_size(path):
    from PIL import Image
    with Image.open(path) as image:
        image.verify()
    with Image.open(path) as image:
        # Rectangular objects such as the native Ruler fill their entire canvas.
        if image.mode != 'RGBA' or image.getchannel('A').getextrema()[1] == 0:
            raise ValueError(f'PNG must be RGBA and contain visible pixels: {path}')
        return list(image.size)


@cache
def audio_info(path):
    result = subprocess.run(['ffprobe', '-v', 'error', '-show_entries',
        'format=duration:stream=codec_name,sample_rate,channels,codec_type', '-of', 'json', str(path)],
        capture_output=True, text=True, check=True)
    info = json.loads(result.stdout)
    streams = info.get('streams', [])
    if len(streams) != 1 or streams[0]['codec_type'] != 'audio':
        raise ValueError(f'expected one audio stream: {path}')
    stream = streams[0]
    if stream['channels'] not in (1, 2) or int(stream['sample_rate']) not in (44100, 48000):
        raise ValueError(f'audio must be mono/stereo, 44.1 or 48 kHz: {path}')
    if float(info['format']['duration']) <= 0:
        raise ValueError(f'empty audio: {path}')
    if path.suffix == '.ogg' and stream['codec_name'] != 'vorbis':
        raise ValueError(f'tModLoader OGG must contain Vorbis: {path}')
    return info


def validate(release=False):
    errors = []
    def require(condition, message):
        if not condition:
            errors.append(message)
    def checked(fn, *args):
        try:
            return fn(*args)
        except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as exc:
            errors.append(str(exc))
            return None

    manifest = read_json(ROOT / 'Riakawa/Assets/manifest.json')
    catalog = read_json(ROOT / 'assets/vanilla-catalog.json')
    music = read_json(ROOT / 'assets/music-map.json')
    sizes_path = ROOT / 'assets/texture-sizes.json'
    sizes = read_json(sizes_path) if sizes_path.exists() else {}
    require(manifest['schemaVersion'] == music['schemaVersion'] == 1, 'unsupported schema')
    require(manifest['tModLoader'] == catalog['tModLoader'] == music['tModLoader'] == '2026.08.3.0',
            'tModLoader version mismatch')
    require(not release or catalog['scopeReviewed'], 'weapon scope still needs human review')
    item_ids = [w['id'] for w in catalog['weapons']]
    items = {w['id']: w for w in catalog['weapons']}
    projectile_ids = {p['id'] for p in catalog['projectiles']}
    require(len(item_ids) == len(set(item_ids)), 'duplicate catalog weapon IDs')
    expected = {(i, c) for i in item_ids for c in CHARACTERS}
    actual = [(w['itemId'], w['character']) for w in manifest['weapons']]
    require(len(actual) == len(set(actual)), 'duplicate weapon/character entries')
    require(set(actual) == expected, f'weapon coverage mismatch: missing {len(expected-set(actual))}, extra {len(set(actual)-expected)}')
    players = [p['character'] for p in manifest['players']]
    require(len(players) == 3 and set(players) == CHARACTERS, 'exactly three player entries required')
    hashes = {}

    def texture(name, vanilla_key=None, unique=False):
        path = checked(asset_path, ROOT / 'Riakawa', name, '.png')
        if path is None:
            return None
        size = checked(png_size, path)
        if vanilla_key:
            require(vanilla_key in sizes, f'missing vanilla texture dimensions: {vanilla_key}')
            if vanilla_key in sizes:
                require(size == sizes[vanilla_key], f'texture dimension mismatch: {name}')
        if unique:
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            require(digest not in hashes, f'duplicate weapon image: {name} and {hashes.get(digest)}')
            hashes[digest] = name
        return size

    def sound(name):
        paths = [ROOT / 'Riakawa' / (str(name) + ext) for ext in ('.wav', '.ogg')]
        ext = next((p.suffix for p in paths if p.is_file()), '.ogg')
        path = checked(asset_path, ROOT / 'Riakawa', name, ext)
        if path:
            checked(audio_info, path)

    for player in manifest['players']:
        label = player['character']
        require(player['status'] in ('pending', 'draft', 'approved'), f'invalid player status: {label}')
        require(not release or player['status'] == 'approved', f'player not approved: {label}')
        size = texture(player['texture']) if player.get('texture') else None
        require(not release or size, f'player texture missing: {label}')
        if size:
            width, height = player['frameWidth'], player['frameHeight']
            require(width > 0 and height > 0, f'invalid frame size: {label}')
            require(width > 0 and height > 0 and size[0] % width == size[1] % height == 0,
                    f'spritesheet not divisible by frame dimensions: {label}')
            require(len(player['feet']) == 2 and 0 <= player['feet'][0] <= width and
                    0 <= player['feet'][1] <= height, f'invalid feet anchor: {label}')
            head = player['head']
            require(len(head) == 4 and min(head) >= 0 and head[2] > 0 and head[3] > 0 and
                    head[0]+head[2] <= width and head[1]+head[3] <= height, f'invalid head rectangle: {label}')
            require(ANIMATIONS <= set(player['animations']), f'animation states missing: {label}')
            for name, sequence in player['animations'].items():
                require(sequence['row'] >= 0 and sequence['frames'] > 0 and sequence['ticks'] > 0 and
                        (sequence['row']+1)*height <= size[1] and sequence['frames']*width <= size[0],
                        f'animation outside sheet: {label}/{name}')
        require(not release or {'hurt','death','emote'} <= set(player['voices']), f'voices missing: {label}')
        for name in player['voices'].values():
            sound(name)

    for weapon in manifest['weapons']:
        label = f"{weapon['itemId']}/{weapon['character']}"
        require(weapon['status'] in ('pending','draft','approved'), f'invalid weapon status: {label}')
        require(not release or (weapon['status'] == 'approved' and weapon['coverageReviewed']),
                f'weapon art/attack coverage not approved: {label}')
        require(not release or weapon.get('texture'), f'weapon texture missing: {label}')
        if weapon.get('texture'):
            texture(weapon['texture'], f"Item/{weapon['itemId']}", True)
        if weapon.get('useSound'):
            sound(weapon['useSound'])
        for name in weapon.get('sounds', {}).values():
            sound(name)
        require(not release or weapon.get('useSound') or not items.get(weapon['itemId'], {}).get('sound'),
                f'weapon use sound missing: {label}')
        held_bindings = weapon.get('heldBindings', [])
        held_keys = [(b['slot'], b['id']) for b in held_bindings]
        require(len(held_keys) == len(set(held_keys)), f'duplicate held texture bindings: {label}')
        for binding in held_bindings:
            require(binding['slot'] in ('Item', 'Extra', 'GlowMask', 'ItemFlame'), f'unsupported held binding: {label}')
            texture(binding['texture'], f"{binding['slot']}/{binding['id']}")
        ids = [a['projectileId'] for a in weapon['attacks']]
        require(len(ids) == len(set(ids)) and set(ids) <= projectile_ids, f'invalid/duplicate attack IDs: {label}')
        for attack in weapon['attacks']:
            particle_draw = attack.get('visualMode') == 'particles'
            require(not particle_draw or attack['projectileId'] in (255,260,294,295,296,297,305,309,357,433,521,522,524,587,619,620,624,698),
                    f'unverified particle-only renderer: {label}/{attack["projectileId"]}')
            require(not particle_draw or manifest.get('particleAtlas'), f'particle atlas missing: {label}')
            require(not release or attack.get('texture') or attack['bindings'] or particle_draw,
                    f'attack visual missing: {label}/{attack["projectileId"]}')
            if attack.get('texture'):
                texture(attack['texture'], f"Projectile/{attack['projectileId']}")
            binding_keys = [(b['slot'], b['id']) for b in attack['bindings']]
            require(len(binding_keys) == len(set(binding_keys)), f'duplicate texture bindings: {label}')
            for binding in attack['bindings']:
                texture(binding['texture'], f"{binding['slot']}/{binding['id']}")
            for name in attack['sounds'].values():
                sound(name)

    require(not release or manifest.get('particleAtlas'), 'attack particle atlas missing')
    if manifest.get('particleAtlas'):
        size = texture(manifest['particleAtlas'])
        native = sizes.get('Dust/0')
        require(native is not None and size == [native[0], native[1]+12], 'particle atlas dimensions mismatch')
    require(set(music['themes']) == THEMES, 'exactly the twelve named themes required')
    ids = [t['id'] for t in music['tracks']]
    require(len(ids) == len(set(ids)) and set(ids) == set(range(1,92))-{28,45}, 'music mapping gaps/duplicates')
    require({t['id'] for t in music['preservedAmbient']} == {28,45}, 'rain/wind must be preserved')
    require({t['theme'] for t in music['tracks']} == THEMES, 'undefined/unused music themes')
    require(next((t['theme'] for t in music['tracks'] if t['id']==51), None)=='menu', 'menu loop track 51 must be mapped')
    for theme, data in music['themes'].items():
        require(not release or data['status'] == 'approved' and data['loopReviewed'], f'music not reviewed: {theme}')
        require(not release or data.get('audio'), f'music file missing: {theme}')
        if data.get('audio'):
            path = checked(asset_path, ROOT, data['audio'])
            if path:
                checked(audio_info, path)
    linked = sum(bool(w.get('texture')) for w in manifest['weapons'])
    print(f"{'RELEASE' if release else 'DRAFT'}: {len(players)} characters, {linked}/{len(actual)} weapon textures linked, "
          f"{len(music['themes'])} themes, {len(ids)} mapped tracks; {len(errors)} errors")
    return errors


def self_check():
    from PIL import Image
    from tempfile import TemporaryDirectory
    with TemporaryDirectory() as directory:
        path = Path(directory)/'sprite.png'
        for alpha in (255, 140):  # Opaque ruler and translucent spell glow.
            Image.new('RGBA', (2, 2), (255, 255, 255, alpha)).save(path)
            assert png_size(path) == [2, 2]
        Image.new('RGBA', (2, 2)).save(path)
        try:
            png_size(path)
        except ValueError:
            pass
        else:
            raise AssertionError('Invisible draft accepted as finished art')
    assert no_duplicate_keys([('a', 1)]) == {'a': 1}
    for pairs in ([('a',1),('a',2)],):
        try:
            no_duplicate_keys(pairs)
        except ValueError:
            pass
        else:
            raise AssertionError('duplicate JSON accepted')
    for name in ('../outside', '/etc/passwd', 'foo\\bar'):
        try:
            asset_path(ROOT, name)
        except ValueError:
            pass
        else:
            raise AssertionError('unsafe path accepted')
    print('PASS: duplicate JSON keys and asset path boundaries')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release', action='store_true')
    parser.add_argument('--self-check', action='store_true')
    args = parser.parse_args()
    if args.self_check:
        self_check()
    try:
        errors = validate(args.release)
    except (ValueError, KeyError, TypeError, OSError) as exc:
        errors = [f'invalid manifest: {exc}']
    for error in errors[:20]:
        print('ERROR:', error)
    if len(errors) > 20:
        print(f'... {len(errors)-20} additional errors')
    raise SystemExit(bool(errors))
