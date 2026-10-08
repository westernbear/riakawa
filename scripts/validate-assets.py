#!/usr/bin/env python3
"""Check the five player sheets used by the appearance-only mod."""
import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
NAMES = {'Chiikawa', 'Hachiware', 'Usagi', 'Momonga', 'Doro'}
POSES = {'idle', 'walk', 'jump', 'swim', 'attack', 'mount', 'hurt', 'death'}


def validate(release=False):
    manifest = json.loads((ROOT/'Riakawa/Assets/manifest.json').read_text())
    json.loads((ROOT/'Riakawa/Localization/ko-KR.hjson').read_text())  # RiakawaLocalization parses it as strict JSON
    assert manifest['schemaVersion'] == 1 and set(manifest) == {'schemaVersion', 'tModLoader', 'players'}
    assert manifest['tModLoader'] == '2026.08.3.0'
    assert len(manifest['players']) == 5
    assert {p['character'] for p in manifest['players']} == NAMES
    for art in manifest['players']:
        name = art['character']
        assert art['status'] in {'draft', 'approved'}, name
        assert not release or art['status'] == 'approved', name
        width, height = 80, 112
        assert art['frameWidth'] == width and art['frameHeight'] == height, name
        assert art['feet'] == ([40, 100] if width == 80 else [20, 50]), name
        assert set(art['animations']) >= POSES, name
        assert ('run' in art['animations']) == (name == 'Doro'), name
        path = ROOT/'Riakawa'/f"{art['texture']}.png"
        assert path.resolve().is_relative_to((ROOT/'Riakawa').resolve()), name
        with Image.open(path) as image:
            columns = 4 if name == 'Doro' else 8
            assert image.mode == 'RGBA' and image.size == (width*columns, height*len(art['animations'])), name
            rows = set()
            for pose, spec in art['animations'].items():
                frames = 8 if name != 'Doro' and pose == 'walk' else 4
                assert spec['frames'] == frames and spec['ticks'] > 0, (name, pose)
                assert 0 <= spec['row'] < len(art['animations']), (name, pose)
                rows.add(spec['row'])
                for frame in range(frames):
                    tile = image.crop((frame*width, spec['row']*height,
                                       (frame+1)*width, (spec['row']+1)*height))
                    assert tile.getbbox() is not None, (name, pose, frame)
                    assert tile.getpixel((0, 0))[3] == 0 and tile.getpixel((width-1, height-1))[3] == 0, (name, pose, frame)
            assert len(rows) == len(art['animations']), name
            if name != 'Doro':
                heads = {image.crop((frame*width, row*height,
                                    (frame+1)*width, row*height+66)).tobytes()
                         for pose in ('idle', 'walk')
                         for row in [art['animations'][pose]['row']]
                         for frame in range(art['animations'][pose]['frames'])}
                assert len(heads) == 1, f'{name} idle/walk face must stay stable'
                distinct = {image.crop((0, spec['row']*height, width*4,
                                        (spec['row']+1)*height)).tobytes()
                            for spec in art['animations'].values()}
                assert len(distinct) == len(art['animations']), f'{name} poses must not reuse walking frames'
            if name == 'Doro':
                run_y = art['animations']['run']['row']*height
                faces = {image.crop((frame*width+25, run_y+37, frame*width+73, run_y+81)).tobytes()
                         for frame in range(4)}
                assert len(faces) == 1, 'Doro run face must stay still'
                ribbons = {image.crop((frame*width+8, run_y+52, frame*width+16, run_y+62)).tobytes()
                           for frame in range(4)}
                assert len(ribbons) == 1, 'Doro run ribbon must stay still'
                mount_y = art['animations']['mount']['row']*height
                stand = image.crop((0, run_y, width, run_y+height)).tobytes()
                assert all(image.crop((frame*width, mount_y, (frame+1)*width, mount_y+height)).tobytes() == stand
                           for frame in range(4)), 'Doro rides on all fours, standing still'
        head = art['head']
        assert len(head) == 4 and 0 <= head[0] < width and 0 <= head[1] < height and head[2] > 0 and head[3] > 0
        assert head[0]+head[2] <= width and head[1]+head[3] <= height, name
    print(f"PASS: {len(NAMES)} character sheets and animation frames" + (' approved' if release else ''))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release', action='store_true')
    validate(parser.parse_args().release)
