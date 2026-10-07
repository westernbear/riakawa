#!/usr/bin/env python3
"""Three original Copper Shortsword designs, aligned to its 32x32 vanilla frame."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
INK='#40271f'
names=('Chiikawa','Hachiware','Usagi')
out=ROOT/'Riakawa/Assets/Weapons'
out.mkdir(parents=True,exist_ok=True)
preview=Image.new('RGB',(768,304),'#ebe9e3')
manifest_path=ROOT/'Riakawa/Assets/manifest.json'
manifest=json.loads(manifest_path.read_text())
digests=set()
for index,name in enumerate(names):
    sprite=Image.new('RGBA',(20,32))
    d=ImageDraw.Draw(sprite)
    accent={'Chiikawa':'#f99fbf','Hachiware':'#70b9d6','Usagi':'#f5b35c'}[name]
    d.rectangle((8,8,11,29),fill=INK)
    d.rectangle((9,9,10,28),fill=accent)
    if name=='Chiikawa':
        points=[(3,1),(3,7),(6,10),(13,10),(16,7),(16,1)]
        d.line(points,fill=INK,width=4)
        d.line(points,fill=accent,width=2)
        d.line((3,1,3,5),fill='#fff5fa')
        d.line((16,1,16,5),fill='#fff5fa')
        d.rectangle((8,17,11,19),fill='#ffdaca',outline=INK)
    elif name=='Hachiware':
        points=[(4,0),(3,3),(4,7),(7,10),(12,10),(15,7),(16,3),(15,0)]
        d.line(points,fill=INK,width=4)
        d.line(points,fill=accent,width=2)
        d.point((4,2),fill='white'); d.point((15,2),fill='white')
        d.polygon([(11,19),(15,21),(15,24),(12,23)],fill='#fff5dc',outline=INK)
        d.point((14,22),fill=accent)
        d.line((9,25,10,25),fill='white')
    else:
        # Usagi's two-ended staff has a distinct silhouette, not a fork recolor.
        d.rectangle((7,3,12,27),fill=INK)
        d.rectangle((8,4,11,26),fill='#fff0bf')
        for y in (1,25):
            d.rounded_rectangle((5,y,14,y+5),radius=1,fill=INK)
            d.rectangle((6,y+1,13,y+4),fill=accent)
            d.line((7,y+1,12,y+1),fill='#ffe8ad')
        d.line((8,15,11,15),fill=accent)
        d.line((8,17,11,17),fill=accent)
    sprite=sprite.rotate(-45,resample=Image.Resampling.NEAREST,expand=True)
    box=sprite.getbbox()
    sprite=sprite.crop(box)
    sprite.thumbnail((30,30),Image.Resampling.NEAREST)
    frame=Image.new('RGBA',(32,32))
    frame.alpha_composite(sprite,((32-sprite.width)//2,(32-sprite.height)//2))
    assert frame.getchannel('A').getextrema()==(0,255)
    assert frame.tobytes() not in digests
    digests.add(frame.tobytes())
    path=f'Assets/Weapons/{name.lower()}-copper-shortsword'
    frame.save(ROOT/'Riakawa'/f'{path}.png')
    for art in manifest['weapons']:
        if art['itemId']==3507 and art['character']==name:
            art.update(status='draft',texture=path,useSound='Assets/Sounds/soft-swish')
            art['attacks']=[{'projectileId':938,'texture':path,'bindings':[],'sounds':{}}]
    large=frame.resize((256,256),Image.Resampling.NEAREST)
    preview.paste(large,(index*256,0),large)
    ImageDraw.Draw(preview).text((index*256+60,275),name,fill=INK)
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
preview.save(ROOT/'artifacts/samples/pixel-polished/copper-shortsword.png')
print('PASS: three unique 32x32 item/attack designs for weapon 3507, projectile 938')
