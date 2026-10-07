#!/usr/bin/env python3
"""Add character particle cells; the client copies its native dust atlas at runtime."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[1]
width,height=json.loads((root/'assets/texture-sizes.json').read_text())['Dust/0']
atlas=Image.new('RGBA',(width,height+12))
colors=('#ffafc5','#85c7e8','#ffe1a2')
for character,color in enumerate(colors):
    for frame in range(3):
        cell=Image.new('RGBA',(8,8));d=ImageDraw.Draw(cell)
        if character==0:
            d.polygon([(0,2),(2,0),(4,2),(6,0),(7,2),(7,4),(4,7),(1,5)],fill=color,outline='#854957')
            d.point((2,2),fill='white')
        elif character==1:
            d.polygon([(0,3),(3,0),(6,2),(7,1),(7,6),(6,5),(3,7)],fill=color,outline='#41617c')
            d.point((2,3),fill='white')
        else:
            d.rounded_rectangle((1,0,3,6),radius=1,fill=color,outline='#94703f')
            d.rounded_rectangle((5,0,7,6),radius=1,fill=color,outline='#94703f')
            d.line((2,6,6,6),fill=color,width=2)
        if frame==1: cell=cell.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if frame==2:
            ImageDraw.Draw(cell).line((3,2,3,5),fill='white')
        atlas.alpha_composite(cell,(character*30+frame*10,height+2))
assert atlas.crop((0,0,width,height)).getbbox() is None
assert atlas.crop((90,height,100,height+12)).getbbox() is None
destination=root/'Riakawa/Assets/Particles';destination.mkdir(exist_ok=True)
atlas.save(destination/'atlas.png')
p=root/'Riakawa/Assets/manifest.json';m=json.loads(p.read_text());m['particleAtlas']='Assets/Particles/atlas'
p.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n')
print('PASS: nine particle cells; native atlas area and reduced-effect cell are empty')
