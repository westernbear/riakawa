#!/usr/bin/env python3
"""Draft the remaining inventory/held skins from installed vanilla silhouettes.

Keeps each weapon's recognizable construction and native canvas/handle location;
uses the approved character palette plus physically different character ornaments.
This produces drafts, not attack coverage or release approval.
"""
import importlib.util
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('special', ROOT/'scripts/draw-special-weapons.py')
special = importlib.util.module_from_spec(spec)
spec.loader.exec_module(special)
INK = (64,39,31)
PALETTES = {'Chiikawa': (249,159,191), 'Hachiware': (112,185,214), 'Usagi': (245,179,92)}


def mix(a,b,t):
    return tuple(round(x*(1-t)+y*t) for x,y in zip(a,b))


def redraw(source, weapon, name):
    w,h = source.size
    pixels = list(source.get_flattened_data())
    colors = [p[:3] for p in pixels if p[3] > 128 and max(p[:3])-min(p[:3]) > 30]
    material = tuple(sum(p[i] for p in colors)//len(colors) for i in range(3)) if colors else (190,190,190)
    accent = PALETTES[name]
    base = mix(accent,material,0.3)
    palette = [INK,mix(INK,base,0.58),base,mix(base,(255,247,222),0.65),(255,250,242)]
    image = Image.new('RGBA',source.size)
    colored=[]
    for r,g,b,a in pixels:
        if not a:
            colored.append((0,0,0,0)); continue
        light = (r*0.2126+g*0.7152+b*0.0722)/255
        level = min(4,int(light*5))
        colored.append((*palette[level],a))
    image.putdata(colored)
    # Place ornament at a native structural feature, away from the handle end.
    if weapon['useAmmo'] == 40:
        anchor=(w*0.62,h*0.5)
    elif weapon['useAmmo']:
        anchor=(w*0.28,h*0.45)
    elif weapon['consumable']:
        anchor=(w*0.5,h*0.45)
    elif weapon['damageClass'] in ('MagicDamageClass','SummonDamageClass'):
        anchor=(w*0.7,h*0.3)
    else:
        anchor=(w*0.4,h*0.6)
    opaque=[(x,y) for y in range(h) for x in range(w) if source.getpixel((x,y))[3] > 128]
    # Glow-only frames have no solid surface for a physical ornament.
    if not opaque: return image
    cx,cy=min(opaque,key=lambda p:(p[0]-anchor[0])**2+(p[1]-anchor[1])**2)
    size=min(12,max(6,min(w,h)//2))
    left=max(0,min(w-size,cx-size//2)); top=max(0,min(h-size,cy-size//2))
    d=ImageDraw.Draw(image)
    if name == 'Chiikawa':
        # Ribbon wings add a different silhouette from the cat charm and ears.
        d.polygon([(left,top+2),(left+size//2,top+size//2),(left,top+size-1)],fill=accent,outline=INK)
        d.polygon([(left+size-1,top+2),(left+size//2,top+size//2),(left+size-1,top+size-1)],fill=accent,outline=INK)
        d.ellipse((left+size//3,top+size//3,left+2*size//3,top+2*size//3),fill='#fff8eb',outline=INK)
    elif name == 'Hachiware':
        d.polygon([(left,top+size//2),(left+size//2,top+1),(left+size-2,top+size//2),
                   (left+size//2,top+size-2)],fill='#fff8eb',outline=INK)
        d.polygon([(left+size-3,top+size//2),(left+size-1,top+1),(left+size-1,top+size-2)],fill=accent,outline=INK)
        d.point((left+size//3,top+size//2-1),fill=INK)
    else:
        middle=left+size//2
        for x in (middle-3,middle+1):
            d.rounded_rectangle((x,top,x+2,top+size-2),radius=1,fill='#fff0bf',outline=INK)
            if size >= 9: d.line((x+1,top+2,x+1,top+size-4),fill='#ffafc5')
        d.line((middle-3,top+size-2,middle+3,top+size-2),fill=accent,width=2)
    # Staff heads use the approved mascot face as well as the ornament.
    if weapon['damageClass'] in ('MagicDamageClass','SummonDamageClass') and min(w,h) >= 24:
        special.badge(image,name,(left,top,size,size))
    assert image.size == source.size
    assert image.getchannel('A').getextrema()[1] > 0, (weapon['id'],name)
    return image


def main():
    assert json.loads((ROOT/'assets/art-review.json').read_text())['weaponDirectionApproved']
    manifest_path=ROOT/'Riakawa/Assets/manifest.json'
    manifest=json.loads(manifest_path.read_text())
    catalog=json.loads((ROOT/'assets/vanilla-catalog.json').read_text())
    weapons={w['id']:w for w in catalog['weapons']}
    selected=[4,44,96,98,121,155,187,190,197,242,272,305,426,434,511,519,682,757,948,1155,1264,1296,1553,1571,1801,1910,1930,1947,2223,2270,2624,2745,2797,2888,3018,3063,3389,3473,3531,3540,3569,3570,3818,3827,3859,4144,4348,4956]
    selected=[id for id in selected if id in weapons]
    preview=Image.new('RGB',(864,((len(selected)+2)//3)*156),'#ebe9e3')
    count=0
    unique=set()
    for entry in manifest['weapons']:
        if entry.get('texture') and '-batch-' not in entry['texture']: continue
        id=entry['itemId'];name=entry['character']
        source=Image.open(ROOT/f'artifacts/qa/reference-textures/Item_{id}.png').convert('RGBA')
        assert source.size == tuple(special.SIZES[f'Item/{id}'])
        image=redraw(source,weapons[id],name)
        digest=(image.size,image.tobytes())
        assert digest not in unique, f'Duplicate draft: {id}/{name}'
        unique.add(digest)
        path=f'Assets/Weapons/{name.lower()}-batch-{id}'
        image.save(ROOT/'Riakawa'/f'{path}.png')
        entry.update(texture=path,status='draft',coverageReviewed=False)
        count+=1
        if id in selected:
            index=selected.index(id);x=(index%3)*288+list(PALETTES).index(name)*96;y=(index//3)*156
            thumb=image.copy();thumb.thumbnail((86,112),Image.Resampling.NEAREST)
            scale=min(3,86//thumb.width,112//thumb.height)
            thumb=thumb.resize((thumb.width*scale,thumb.height*scale),Image.Resampling.NEAREST)
            preview.paste(thumb,(x+(96-thumb.width)//2,y),thumb)
            ImageDraw.Draw(preview).text((x,y+118),f'{id} {name[:3]}',fill=INK)
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    preview.save(ROOT/'artifacts/samples/pixel-polished/weapon-batch-preview.png')
    print(f'PASS: {count} distinct native-size draft items; attack coverage remains unreviewed')


if __name__ == '__main__':
    if '--self-check' in sys.argv:
        for alpha in (0, 1, 128):
            source = Image.new('RGBA', (4, 4), (50, 100, 200, alpha))
            result = redraw(source, {'useAmmo': 0, 'consumable': True}, 'Chiikawa')
            assert result.size == source.size
            assert result.getchannel('A').tobytes() == source.getchannel('A').tobytes()
        print('PASS: transparent/translucent frames keep alpha without opaque ornaments')
    else:
        main()
