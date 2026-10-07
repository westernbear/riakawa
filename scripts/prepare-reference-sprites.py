#!/usr/bin/env python3
"""Palette-restricted sprite studies from official reference silhouettes; drafts."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
PALETTE = [(250,247,247),(62,35,28),(255,179,196),(121,173,204),(255,241,194),(255,255,255)]
source = ROOT/'artifacts/reference-source'
out = ROOT/'artifacts/samples/pixel-studies'
out.mkdir(parents=True, exist_ok=True)
preview = Image.new('RGB',(960,520),(235,233,227))
draw = ImageDraw.Draw(preview)
for index, character in enumerate(('chiikawa','hachiware','usagi')):
    original = Image.open(source/f'{character}-official-reference.png').convert('RGBA')
    width = 30
    height = round(original.height * width / original.width)
    small = original.resize((width,height),Image.Resampling.NEAREST)
    palette = PALETTE[:3] + ([PALETTE[3]] if character == 'hachiware' else []) + \
        ([PALETTE[4]] if character == 'usagi' else []) + [PALETTE[5]]
    quantized = Image.new('RGBA',small.size)
    for y in range(height):
        for x in range(width):
            r,g,b,a = small.getpixel((x,y))
            if a < 128:
                continue
            color = min(palette,key=lambda c:(r-c[0])**2+(g-c[1])**2+(b-c[2])**2)
            quantized.putpixel((x,y),(*color,255))
    frame = Image.new('RGBA',(40,56))
    frame.alpha_composite(quantized,(5,50-height))
    assert frame.getchannel('A').getextrema()==(0,255)
    assert frame.getbbox()[3] == 50
    frame.save(out/f'{character}-idle-draft.png')
    enlarged=frame.resize((320,448),Image.Resampling.NEAREST)
    preview.paste(enlarged,(index*320,32),enlarged)
    draw.text((index*320+90,490),character,fill=(62,35,28))
    (out/f'{character}-idle-draft.json').write_text(json.dumps({
        'status':'draft','method':'reference downsample and palette reduction; animation not yet drawn',
        'source':'https://www.anime-chiikawa.jp/chara.html','frameSize':[40,56],
        'feet':[20,50],'palette':palette},indent=2)+'\n')
preview.save(out/'preview.png')
print('PASS: three transparent 40x56 draft frames, aligned feet, fixed palette')
