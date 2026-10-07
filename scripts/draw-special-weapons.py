#!/usr/bin/env python3
"""Draft bow, whip, summon and beam art using the approved character pixels.

Frame sizes and segment boundaries follow the pinned game's native renderers.
Coverage stays unreviewed: this does not imply every arrow type is completed.
"""
import importlib.util
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('characters', ROOT/'scripts/draw-sprite-trial.py')
characters = importlib.util.module_from_spec(spec)
spec.loader.exec_module(characters)
INK = characters.INK
ACCENTS = {'Chiikawa': '#f99fbf', 'Hachiware': '#70b9d6', 'Usagi': '#f5b35c'}
SIZES = json.loads((ROOT/'assets/texture-sizes.json').read_text())


def badge(image, name, box):
    face = characters.sprite(name.lower()).crop((5, 3 if name == 'Usagi' else 12, 36, 40))
    face.thumbnail(box[2:], Image.Resampling.NEAREST)
    image.alpha_composite(face, (box[0] + (box[2]-face.width)//2, box[1]))


def item(name, kind):
    size = SIZES[f'Item/{kind}']
    image = Image.new('RGBA', size)
    d = ImageDraw.Draw(image)
    accent = ACCENTS[name]
    if kind == 39:
        points = {'Chiikawa': [(2,2),(8,5),(11,10),(10,16),(11,22),(8,27),(2,30)],
                  'Hachiware': [(3,2),(11,8),(12,13),(9,16),(12,19),(11,24),(3,30)],
                  'Usagi': [(3,2),(9,5),(13,11),(13,21),(9,27),(3,30)]}[name]
        d.line((3,3,3,29), fill='#f5e2c6')
        d.line(points, fill=INK, width=4)
        d.line(points, fill=accent, width=2)
        if name == 'Chiikawa':
            d.polygon([(6,12),(11,15),(15,12),(15,19),(11,17),(6,19)], fill=accent, outline=INK)
        elif name == 'Hachiware':
            d.polygon([(5,4),(9,1),(13,4),(9,7)], fill='#fff7df', outline=INK)
            d.point((10,3), fill=INK)
        else:
            d.line((10,9,6,5,6,2), fill='#fff0bf', width=2)
            d.line((11,9,12,4,11,1), fill='#fff0bf', width=2)
        d.line((10,14,10,18), fill=INK, width=2)
    elif kind == 1309:
        d.line((2,32,21,10), fill=INK, width=4)
        d.line((3,31,20,11), fill=accent, width=2)
        d.ellipse((10,3,24,17), fill=accent, outline=INK)
        badge(image, name, (10,0,15,16))
        if name == 'Chiikawa':
            d.polygon([(11,19),(8,16),(7,22)], fill=accent, outline=INK)
        elif name == 'Hachiware':
            d.polygon([(21,16),(25,19),(21,22),(19,19)], fill='#fff7df', outline=INK)
        else:
            d.line((16,19,21,23,22,19), fill=accent, width=2)
    elif kind == 4672:
        curves = {'Chiikawa': [(6,25),(12,14),(18,17),(18,28),(24,30),(30,25),(32,9)],
                  'Hachiware': [(5,28),(12,15),(19,12),(28,16),(29,23),(23,26),(17,21),(20,6)],
                  'Usagi': [(5,29),(14,14),(19,25),(25,28),(31,22),(27,15),(30,5)]}
        d.line(curves[name], fill=INK, width=4)
        d.line(curves[name], fill=accent, width=2)
        d.line((3,31,13,17), fill=INK, width=5)
        d.line((4,30,13,18), fill='#fff0bf', width=2)
        if name == 'Chiikawa':
            d.polygon([(28,3),(31,5),(34,3),(36,6),(32,12),(28,8)], fill=accent, outline=INK)
        elif name == 'Hachiware':
            d.polygon([(17,5),(21,1),(25,5),(21,9)], fill='#fff7df', outline=INK)
            d.point((22,4), fill=INK)
        else:
            d.line((27,6,26,1), fill='#fff0bf', width=3)
            d.line((32,7,35,2), fill='#fff0bf', width=3)
    else:
        shapes = {'Chiikawa': [(5,13),(8,5),(18,5),(22,14),(21,25),(13,29),(4,25)],
                  'Hachiware': [(13,0),(25,14),(19,28),(7,28),(1,14)],
                  'Usagi': [(4,7),(10,3),(16,3),(22,7),(23,24),(18,28),(8,28),(3,24)]}
        d.polygon(shapes[name], fill=accent, outline=INK)
        d.line((6,24,13,27,20,24), fill='white')
        d.line((4,18,8,23), fill='#fff7df')
        badge(image, name, (5, 1,16,20))
    return image


def attack(name, projectile):
    image = Image.new('RGBA', SIZES[f'Projectile/{projectile}'])
    d = ImageDraw.Draw(image)
    accent = ACCENTS[name]
    if projectile == 1:
        d.line((7,6,7,29), fill=INK, width=3)
        d.line((7,8,7,29), fill=accent)
        if name == 'Chiikawa':
            d.polygon([(7,1),(3,4),(3,7),(7,11),(11,7),(11,4)], fill=accent, outline=INK)
        elif name == 'Hachiware':
            d.polygon([(7,0),(3,5),(7,10),(11,5)], fill='#fff7df', outline=INK)
            d.point((8,4), fill=INK)
            d.polygon([(7,9),(4,13),(10,13)], fill=accent, outline=INK)
        else:
            d.polygon([(7,0),(3,10),(11,10)], fill=accent, outline=INK)
            d.line((5,5,8,5), fill='#fff0bf')
        d.line((3,25,7,28,11,25), fill=accent, width=2)
    elif projectile == 266:
        for frame, height in enumerate((23,25,27,25,23,21)):
            pose = characters.sprite(name.lower(), 'walk', frame%4)
            pose = pose.crop(pose.getbbox())
            width = round(pose.width * height / pose.height)
            pose = pose.resize((width,height), Image.Resampling.NEAREST)
            image.alpha_composite(pose, ((44-width)//2, frame*30+28-height))
    elif projectile == 841:
        # Native BlandWhip: five 28px slices, with two spacer rows per slice.
        for frame in range(5):
            y = frame*28
            if frame == 0:
                d.rounded_rectangle((2,y+3,7,y+24),radius=1,fill=INK)
                d.rectangle((3,y+5,6,y+21),fill=accent)
            else:
                d.line((4,y,4,y+25), fill=INK, width=3)
                d.line((4,y,4,y+25), fill=accent)
                if name == 'Chiikawa':
                    d.polygon([(1,y+8),(4,y+10),(8,y+8),(7,y+15),(4,y+13),(1,y+15)],fill=accent,outline=INK)
                elif name == 'Hachiware':
                    d.polygon([(4,y+6),(8,y+11),(4,y+16),(1,y+11)],fill='#fff7df',outline=INK)
                    d.point((5,y+10),fill=INK)
                else:
                    d.polygon([(1,y+7),(8,y+7),(4,y+18)],fill=accent,outline=INK)
                    d.line((3,y+7,2,y+3),fill='#fff0bf')
                    d.line((5,y+7,7,y+3),fill='#fff0bf')
    elif projectile == 633:
        # Five 24px native frames; holdout origin and rotations remain untouched.
        source = item(name,3541)
        source.thumbnail((24,22),Image.Resampling.NEAREST)
        for frame in range(5):
            image.alpha_composite(source,((26-source.width)//2,frame*24+1))
            ImageDraw.Draw(image).point((5+frame*3,frame*24+20), fill='white')
    else:
        # RainbowLaserDraw samples three 26px caps/segments and applies its hue.
        for segment in range(3):
            y=segment*26
            d.rectangle((10,y,15,y+25),fill=(255,255,255,210))
            d.line((12,y,12,y+25),fill='white',width=2)
            if name == 'Chiikawa':
                d.polygon([(13,y+16),(6,y+9),(8,y+5),(13,y+8),(18,y+5),(20,y+9)],fill='white')
            elif name == 'Hachiware':
                d.polygon([(13,y+3),(21,y+12),(13,y+21),(5,y+12)],outline='white',width=2)
            else:
                d.line((5,y,9,y+8,5,y+16,9,y+25),fill='white',width=2)
                d.line((21,y,17,y+8,21,y+16,17,y+25),fill='white',width=2)
    return image


def main():
    assert json.loads((ROOT/'assets/art-review.json').read_text())['referenceApproved']
    manifest_path = ROOT/'Riakawa/Assets/manifest.json'
    manifest = json.loads(manifest_path.read_text())
    weapons = {39: [1], 1309: [266], 4672: [841], 3541: [633,632]}
    preview = Image.new('RGB',(720,640),'#ebe9e3')
    seen = set()
    for row, (item_id, shots) in enumerate(weapons.items()):
        for column, name in enumerate(ACCENTS):
            prefix = f'Assets/Weapons/{name.lower()}-{item_id}'
            image = item(name,item_id)
            assert image.size == tuple(SIZES[f'Item/{item_id}'])
            assert image.tobytes() not in seen, 'Duplicate item design'
            seen.add(image.tobytes())
            image.save(ROOT/'Riakawa'/f'{prefix}.png')
            large=image.resize((image.width*3,image.height*3),Image.Resampling.NEAREST)
            preview.paste(large,(column*240+8,row*160+12),large)
            ImageDraw.Draw(preview).text((column*240+8,row*160+136),f'{name} / {item_id}',fill=INK)
            attacks=[]
            for projectile in shots:
                path=f'{prefix}-attack-{projectile}'
                art=attack(name,projectile)
                assert art.size == tuple(SIZES[f'Projectile/{projectile}'])
                assert art.getchannel('A').getextrema() == (0,255)
                art.save(ROOT/'Riakawa'/f'{path}.png')
                bindings=[]
                if projectile == 841:
                    line=Image.new('RGBA',SIZES['FishingLine/0'])
                    ImageDraw.Draw(line).line((5,0,5,11),fill=ACCENTS[name],width=2)
                    line.save(ROOT/'Riakawa'/f'{prefix}-line.png')
                    bindings=[{'slot':'FishingLine','id':0,'texture':f'{prefix}-line'}]
                attacks.append({'projectileId':projectile,'texture':path,'bindings':bindings,'sounds':{}})
            entry=next(w for w in manifest['weapons'] if w['itemId']==item_id and w['character']==name)
            entry.update(status='draft',texture=prefix,attacks=attacks,coverageReviewed=False)
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    preview.save(ROOT/'artifacts/samples/pixel-polished/special-weapons.png')
    print('PASS: 12 distinct items; 15 native-size attack sheets; whip line bindings')


if __name__ == '__main__':
    main()
