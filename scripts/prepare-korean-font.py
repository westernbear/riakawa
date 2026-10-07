#!/usr/bin/env python3
"""Rasterize only the Hangul used by this mod, using the installed open font."""
import json
import shutil
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
source = ROOT / 'Riakawa/Localization/ko-KR.hjson'
characters = ''.join(sorted({c for c in source.read_text() if '\uac00' <= c <= '\ud7a3'}))
out = ROOT / 'Riakawa/Assets/Fonts'
out.mkdir(parents=True, exist_ok=True)
for size in (16, 22, 32):
    font = ImageFont.truetype('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc', size)
    cell = size + 4
    atlas = Image.new('RGBA', (cell * 16, cell * ((len(characters) + 15) // 16)))
    draw = ImageDraw.Draw(atlas)
    for i, char in enumerate(characters):
        box = font.getbbox(char)
        assert box[2] > box[0] and box[3] > box[1], f'Missing glyph: {char}'
        draw.text((i % 16 * cell + 2 - box[0], i // 16 * cell + 2 - box[1]),
                  char, font=font, fill='white')
    assert atlas.getchannel('A').getextrema() == (0, 255)
    atlas.save(out / f'korean-{size}.png')
(out / 'korean.json').write_text(json.dumps({'characters': characters}, ensure_ascii=False) + '\n')
shutil.copyfile('/usr/share/doc/fonts-wqy-zenhei/copyright', out / 'WenQuanYi-LICENSE.txt')
shutil.copyfile('/usr/share/common-licenses/GPL-2', out / 'GPL-2.txt')
print(f'PASS: {len(characters)} Hangul glyphs at three sizes, font notices included')
