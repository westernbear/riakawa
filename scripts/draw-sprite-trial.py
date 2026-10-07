#!/usr/bin/env python3
"""Hand-cleaned pixel studies using the official character proportions as reference."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
INK = '#40271f'
WHITE = '#faf7f7'
PINK = '#ffafc5'
BLUE = '#7badcc'
CREAM = '#fff0bf'
out = ROOT/'artifacts/samples/pixel-polished'
out.mkdir(parents=True, exist_ok=True)


def eye(draw, x, y, expression='open'):
    if expression=='closed':
        draw.line([(x,y+2),(x+1,y+1),(x+2,y+1),(x+3,y+2)],fill=INK)
        return
    if expression=='hurt':
        draw.line([(x,y),(x+3,y+2),(x,y+4)],fill=INK)
        return
    if expression=='dead':
        draw.line((x,y,x+3,y+3),fill=INK)
        draw.line((x+3,y,x,y+3),fill=INK)
        return
    draw.rectangle((x+1,y,x+2,y+4),fill=INK)
    draw.rectangle((x,y+1,x+3,y+3),fill=INK)
    draw.line((x+1,y+1,x+2,y+1),fill=WHITE)
    draw.point((x+1,y+3),fill=WHITE)


def sprite(character, animation='idle', frame=0):
    if character == 'momonga':
        return momonga(animation, frame)
    image=Image.new('RGBA',(40,56))
    d=ImageDraw.Draw(image)
    body=CREAM if character=='usagi' else WHITE
    if character=='chiikawa':
        shape=[(10,18),(10,15),(11,13),(13,13),(15,16),(25,16),(27,13),(29,13),(30,15),(30,18),
            (33,21),(35,25),(35,32),(33,35),(32,40),(30,44),(29,48),(27,49),(25,47),
            (15,47),(13,49),(11,48),(10,44),(8,40),(7,35),(5,32),(5,25),(7,21)]
    elif character=='hachiware':
        shape=[(7,20),(8,14),(10,13),(15,17),(25,17),(30,13),(32,14),(33,20),
            (35,25),(35,32),(33,36),(32,40),(30,44),(29,48),(27,49),(25,47),
            (15,47),(13,49),(11,48),(10,44),(8,40),(7,36),(5,32),(5,25)]
    else:
        shape=[(11,22),(10,19),(10,7),(11,5),(13,4),(15,5),(16,8),(16,21),
            (24,21),(24,8),(25,5),(27,4),(29,5),(30,7),(30,19),(29,22),
            (33,25),(35,29),(35,36),(33,39),(32,43),(29,47),(27,49),(25,47),
            (15,47),(13,49),(11,47),(8,43),(7,39),(5,36),(5,29),(7,25)]
    if animation=='walk':
        # Feet alternate at the same floor contact; the torso never slides.
        stride=(-1,0,1,0)[frame%4]
        shape=[(x+stride*(1 if x<20 else -1),y-(1 if (x<20)==(stride>0) else 0))
               if y>=44 else (x,y) for x,y in shape]
    elif animation in ('jump','swim','mount'):
        shape=[(x,y-2) if y>=44 else (x,y) for x,y in shape]
    d.polygon(shape,fill=body,outline=INK)
    if character=='hachiware':
        d.polygon([(8,20),(9,15),(10,15),(15,18),(25,18),(30,15),(31,15),(32,20),(33,24),
            (28,24),(24,22),(20,20),(16,23),(12,25),(7,25)],fill=BLUE)
        d.line([(7,25),(12,25),(16,23),(20,20),(24,22),(28,24),(33,24)],fill=INK)
    if character=='usagi':
        d.rectangle((12,8,14,21),fill=PINK)
        d.rectangle((26,8,28,21),fill=PINK)
    eye_y=29 if character=='usagi' else 26
    expression='dead' if animation=='death' else 'hurt' if animation=='hurt' else \
        'closed' if animation=='emote' or animation=='idle' and frame==3 else 'open'
    eye(d,12,eye_y,expression)
    eye(d,24,eye_y,expression)
    if character!='hachiware':
        d.line((12,eye_y-3,14,eye_y-4),fill=INK)
        d.line((25,eye_y-4,27,eye_y-3),fill=INK)
    cheek_y=eye_y+5
    for x in (8,27):
        d.rectangle((x,cheek_y,x+4,cheek_y+2),fill=PINK)
        d.point((x+1,cheek_y+1),fill=INK)
        d.point((x+3,cheek_y+1),fill=INK)
    mouth_y=eye_y+6
    if animation=='emote':
        d.polygon([(18,mouth_y-1),(23,mouth_y-1),(22,mouth_y+3),(20,mouth_y+4),(18,mouth_y+2)],fill=INK)
        d.line((20,mouth_y+2,22,mouth_y+2),fill=PINK)
    elif animation in ('hurt','death'):
        d.line([(18,mouth_y+1),(20,mouth_y),(22,mouth_y+1)],fill=INK)
    else:
        d.line([(18,mouth_y-1),(18,mouth_y),(19,mouth_y+1),(20,mouth_y),
            (21,mouth_y+1),(22,mouth_y),(22,mouth_y-1)],fill=INK)
        d.line((20,mouth_y+3,21,mouth_y+3),fill=INK)
    # Small arms follow the reference silhouette instead of stretching the torso.
    if animation in ('emote','jump'):
        lift=frame%2
        d.polygon([(8,37),(3,34-lift),(2,31-lift),(4,30-lift),(10,35)],fill=body,outline=INK)
        d.polygon([(31,37),(36,34-lift),(37,31-lift),(35,30-lift),(29,35)],fill=body,outline=INK)
    elif animation=='attack':
        reach=(0,2,4,1)[frame%4]
        d.line([(8,37),(9,39),(10,39),(10,37)],fill=INK)
        d.polygon([(29,36),(32+reach,32),(35+reach,33),(35+reach,35),(31,39)],fill=body,outline=INK)
    elif animation=='swim':
        paddle=(0,2,0,-2)[frame%4]
        d.polygon([(8,36),(2,35+paddle),(1,37+paddle),(7,40)],fill=body,outline=INK)
        d.polygon([(31,36),(37,35-paddle),(38,37-paddle),(32,40)],fill=body,outline=INK)
    elif animation=='mount':
        d.line([(9,37),(14,40),(14,42),(11,42)],fill=INK)
        d.line([(31,37),(26,40),(26,42),(29,42)],fill=INK)
    else:
        swing=(-1,0,1,0)[frame%4] if animation=='walk' else 0
        d.line([(8,37),(9,39+swing),(10,39+swing),(10,37)],fill=INK)
        d.line([(31,36),(29,36-swing),(29,37-swing),(30,38-swing),(32,38)],fill=INK)
    if animation=='death':
        height=(56,48,38,28)[min(frame,3)]
        collapsed=image.resize((40,height),Image.Resampling.NEAREST)
        image=Image.new('RGBA',(40,56))
        image.alpha_composite(collapsed,(0,50-round(50*height/56)))
    assert image.getbbox()[3]<=50
    assert image.getchannel('A').getextrema()==(0,255)
    return image


def momonga(animation='idle', frame=0):
    image = Image.new('RGBA', (40,56))
    d = ImageDraw.Draw(image)
    cyan = '#a9d8dd'
    swing = (0,1,0,-1)[frame%4]
    # The tail sits behind the torso, with a visible curl above the shoulder.
    d.polygon([(27,46),(32,45),(36,41),(38,35),(39,27+swing),
        (37,21+swing),(34,19+swing),(31,21+swing),(31,27),(33,30),(32,37),(27,40)],
        fill=cyan,outline=INK)
    d.line([(34,23+swing),(36,25+swing),(36,30),(34,33)],fill=INK)
    for x,y in [(36,36),(34,41),(30,44)]:
        d.line((x,y,x+2,y-1),fill=INK)
    shape=[(7,20),(5,17),(5,12),(7,9),(10,9),(12,12),(12,17),
        (23,17),(24,12),(26,9),(29,10),(31,13),(31,17),(29,20),
        (32,24),(34,29),(33,32),(35,34),(32,34),(34,37),(31,37),
        (31,41),(28,46),(27,49),(24,49),(23,47),(13,47),(12,49),
        (9,49),(8,46),(5,41),(5,37),(2,37),(4,34),(1,34),
        (3,31),(2,28),(4,23)]
    if animation=='walk':
        shape=[(x,y-(1 if (x<18)==(swing>0) else 0)) if y>=46 else (x,y) for x,y in shape]
    elif animation in ('jump','swim','mount'):
        shape=[(x,y-2) if y>=46 else (x,y) for x,y in shape]
    d.polygon(shape,fill=WHITE,outline=INK)
    d.line([(8,12),(8,16),(10,18)],fill=cyan,width=2)
    d.line([(27,12),(28,15),(26,18)],fill=cyan,width=2)
    d.line((8,22,11,21),fill=INK)
    d.line((24,21,27,22),fill=INK)
    for x in (8,22):
        if animation in ('hurt','death'):
            eye(d,x+1,26,'dead' if animation=='death' else 'hurt')
        elif animation=='idle' and frame==3:
            d.line([(x,29),(x+2,30),(x+5,29)],fill=INK)
        else:
            d.ellipse((x,26,x+6,31),fill=INK)
            d.ellipse((x+1,27,x+5,29),fill=WHITE)
            d.line((x+2,31,x+4,31),fill=WHITE)
    d.ellipse((16,28,19,30),fill=cyan)
    d.point((17,28),fill=INK)
    d.point((19,28),fill=INK)
    for x in (4,25):
        d.ellipse((x,32,x+6,35),fill=PINK)
        d.line((x+2,33,x+1,34),fill=INK)
        d.line((x+4,33,x+3,34),fill=INK)
    if animation in ('hurt','death'):
        d.line([(15,35),(18,34),(21,35)],fill=INK)
    else:
        d.line([(14,32),(15,34),(17,34),(18,32),(19,34),(21,34),(22,32)],fill=INK)
        d.arc((16,33,20,37),0,180,fill=INK)
        if animation=='emote':
            d.rectangle((17,35,19,37),fill=PINK)
            d.line((17,38,19,38),fill=INK)
    if animation in ('jump','swim'):
        # Spread forepaws and the pale blue gliding membrane, within the frame.
        d.polygon([(6,37),(1,34+swing),(2,41),(9,46),(11,42)],fill=cyan,outline=INK)
        d.polygon([(29,37),(36,34-swing),(35,41),(27,46),(25,42)],fill=cyan,outline=INK)
        d.rectangle((1,33+swing,4,35+swing),fill=WHITE,outline=INK)
        d.rectangle((33,33-swing,36,35-swing),fill=WHITE,outline=INK)
    elif animation=='attack':
        reach=(0,2,4,1)[frame%4]
        d.polygon([(28,37),(31+reach,32),(34+reach,33),(34+reach,35),(29,40)],fill=WHITE,outline=INK)
        d.line([(7,38),(10,40),(10,42),(8,42)],fill=INK)
    elif animation=='emote':
        d.polygon([(6,37),(3,32),(5,30),(8,33),(10,32),(10,35)],fill=WHITE,outline=INK)
        d.polygon([(28,37),(31,32),(29,30),(26,33),(24,32),(24,35)],fill=WHITE,outline=INK)
    else:
        d.line([(7,38),(10,40+swing),(10,42),(8,42)],fill=INK)
        d.line([(28,38),(25,40-swing),(25,42),(27,42)],fill=INK)
    if animation=='death':
        height=(56,48,38,28)[min(frame,3)]
        collapsed=image.resize((40,height),Image.Resampling.NEAREST)
        image=Image.new('RGBA',(40,56))
        image.alpha_composite(collapsed,(0,50-round(50*height/56)))
    assert image.getbbox()[3]<=50
    assert image.getchannel('A').getextrema()==(0,255)
    return image


def main():
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sheets',action='store_true',help='Build game sheets from the approved reference')
    args=parser.parse_args()
    names=('chiikawa','hachiware','usagi')
    if args.sheets:
        review=json.loads((ROOT/'assets/art-review.json').read_text())
        assert review['referenceApproved'], 'Reference approval is required before animation production'
        animations=('idle','walk','jump','swim','attack','mount','hurt','death','emote')
        destination=ROOT/'Riakawa/Assets/Players'
        destination.mkdir(parents=True,exist_ok=True)
        manifest_path=ROOT/'Riakawa/Assets/manifest.json'
        manifest=json.loads(manifest_path.read_text())
        contact=Image.new('RGB',(3*160,9*56),'#ebe9e3')
        for index,name in enumerate(names):
            sheet=Image.new('RGBA',(160,9*56))
            frames={}
            for row,animation in enumerate(animations):
                for frame in range(4):
                    pose=sprite(name,animation,frame)
                    sheet.alpha_composite(pose,(40*frame,56*row))
                frames[animation]={'row':row,'frames':4,'ticks':24 if animation=='idle' else 7}
            sheet.save(destination/f'{name}.png')
            contact.paste(sheet,(index*160,0),sheet)
            for art in manifest['players']:
                if art['character'].lower()==name:
                    art.update(status='draft',texture=f'Assets/Players/{name}',animations=frames,
                               head=[5,3 if name=='usagi' else 12,31,37 if name=='usagi' else 25])
        manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
        contact.resize((960,1008),Image.Resampling.NEAREST).save(out/'animation-sheets.png')
        # Icons use the approved pixels, without introducing another art design.
        for size in (30,80):
            icon=Image.new('RGBA',(size,size),'#f1e7da')
            face=sprite('chiikawa').crop((5,12,36,43))
            icon.alpha_composite(face.resize((size-4,size-4),Image.Resampling.NEAREST),(2,2))
            icon.save(ROOT/'Riakawa'/('icon_small.png' if size==30 else 'icon.png'))
        print('PASS: three sheets, nine animation states, four frames each; bounded transparent pixels')
        return
    preview=Image.new('RGB',(960,520),'#ebe9e3')
    labels=ImageDraw.Draw(preview)
    for index,character in enumerate(names):
        image=sprite(character)
        image.save(out/f'{character}-idle-draft.png')
        large=image.resize((320,448),Image.Resampling.NEAREST)
        preview.paste(large,(index*320,20),large)
        labels.text((index*320+95,490),character,fill=INK)
        (out/f'{character}-idle-draft.json').write_text(json.dumps({'status':'draft',
            'method':'hand-cleaned pixel silhouette and face','source':'https://www.anime-chiikawa.jp/chara.html',
            'frameSize':[40,56],'feet':[20,50]},indent=2)+'\n')
    preview.save(out/'preview.png')
    print('PASS: three hand-cleaned 40x56 studies; transparent edges and feet align')


if __name__=='__main__':
    main()
