#!/usr/bin/env python3
"""Build Momonga assets using the existing native attack/binding catalog."""
import copy
import importlib.util
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('batch', ROOT/'scripts/draw-weapon-batch.py')
batch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(batch)
characters = batch.special.characters


def main():
    path = ROOT/'Riakawa/Assets/manifest.json'
    manifest = json.loads(path.read_text())
    catalog = json.loads((ROOT/'assets/vanilla-catalog.json').read_text())
    weapons = {w['id']:w for w in catalog['weapons']}
    projectiles = {p['id']:p for p in catalog['projectiles']}
    player = copy.deepcopy(next(p for p in manifest['players'] if p['character']=='Chiikawa'))
    player.update(character='Momonga',status='draft',texture='Assets/Players/momonga',head=[1,9,35,29],voices={})
    sheet = Image.new('RGBA',(160,504))
    for animation,sequence in player['animations'].items():
        for frame in range(4):
            sheet.alpha_composite(characters.sprite('momonga',animation,frame),(40*frame,56*sequence['row']))
    sheet.save(ROOT/'Riakawa/Assets/Players/momonga.png')
    manifest['players'] = [p for p in manifest['players'] if p['character']!='Momonga']+[player]
    entries = copy.deepcopy([w for w in manifest['weapons'] if w['character']=='Chiikawa'])
    generated = set()

    def texture(old,slot,index):
        if not old:
            return old
        name = old.replace('chiikawa','momonga')
        if name in generated:
            return name
        source = Image.open(ROOT/f'artifacts/qa/reference-textures/{slot}_{index}.png').convert('RGBA')
        if slot=='Item' and name.startswith('Assets/Weapons/'):
            image = batch.redraw(source,weapons.get(index,{'id':index,'useAmmo':0,'consumable':True,'damageClass':'Item'}),'Momonga')
        elif slot=='Projectile' and index==266:
            image = Image.new('RGBA',source.size)
            for frame in range(6):
                pose=characters.sprite('momonga','walk',frame%4)
                pose=pose.crop(pose.getbbox()); pose.thumbnail((36,27),Image.Resampling.NEAREST)
                image.alpha_composite(pose,((source.width-pose.width)//2,frame*30+28-pose.height))
        elif slot=='Projectile':
            data=projectiles[index]
            count=5 if data['whip'] else max(1,data['frames'])
            height=source.height//count
            image=source.copy()
            for frame in range(count):
                part=source.crop((0,frame*height,source.width,(frame+1)*height))
                art=batch.redraw(part,{'id':index,'useAmmo':0,'consumable':True,'damageClass':'Projectile'},'Momonga')
                art.putalpha(part.getchannel('A')); image.paste(art,(0,frame*height))
        else:
            accent=batch.PALETTES['Momonga']
            colors=[batch.mix(batch.INK,accent,.1),batch.mix(batch.INK,accent,.6),accent,
                    batch.mix(accent,(255,247,222),.55),batch.mix(accent,(255,250,242),.85)]
            image=Image.new('RGBA',source.size)
            image.putdata([(*colors[min(4,int((r*299+g*587+b*114)/1000*5/256))],a)
                           for r,g,b,a in source.get_flattened_data()])
        assert image.size==source.size and image.getbbox(),(slot,index)
        if (slot!='Item' or '/binding-' in name) and not (slot=='Projectile' and index==266):
            assert image.getchannel('A').tobytes()==source.getchannel('A').tobytes(),(slot,index)
        image.save(ROOT/'Riakawa'/f'{name}.png'); generated.add(name)
        return name

    # Item art can also be the attack texture, so create it before bindings.
    for entry in entries:
        entry.update(character='Momonga',status='draft',coverageReviewed=False,useSound=None,sounds={})
        entry['texture']=texture(entry['texture'],'Item',entry['itemId'])
    for entry in entries:
        for binding in entry.get('heldBindings',[]):
            binding['texture']=texture(binding['texture'],binding['slot'],binding['id'])
        for attack in entry['attacks']:
            attack['texture']=texture(attack['texture'],'Projectile',attack['projectileId'])
            attack['sounds']={}
            for binding in attack['bindings']:
                binding['texture']=texture(binding['texture'],binding['slot'],binding['id'])
    manifest['weapons']=[w for w in manifest['weapons'] if w['character']!='Momonga']+entries
    path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    out=ROOT/'artifacts/samples/momonga';out.mkdir(parents=True,exist_ok=True)
    sheet.resize((480,1512),Image.Resampling.NEAREST).save(out/'animations.png')
    selected=[3507,39,1309,4672,3541,4956,989,2611,1155,3473,4953,3852]
    preview=Image.new('RGB',(800,620),'#ebe9e3');d=ImageDraw.Draw(preview)
    for i,character in enumerate(('chiikawa','hachiware','usagi','momonga')):
        pose=characters.sprite(character).resize((160,224),Image.Resampling.NEAREST)
        preview.paste(pose,(20+i*200,0),pose);d.text((30+i*200,224),character,fill=batch.INK)
    for i,id in enumerate(selected):
        entry=next(w for w in entries if w['itemId']==id)
        art=Image.open(ROOT/'Riakawa'/f"{entry['texture']}.png")
        art.thumbnail((90,85),Image.Resampling.NEAREST)
        scale=min(3,100//art.width,100//art.height)
        art=art.resize((art.width*scale,art.height*scale),Image.Resampling.NEAREST)
        x=20+(i%6)*130;y=280+(i//6)*160
        preview.paste(art,(x,y),art);d.text((x,y+110),str(id),fill=batch.INK)
    preview.save(out/'preview.png')
    print(f'PASS: Momonga player sheet, {len(entries)} weapon entries, {len(generated)} textures')


if __name__=='__main__':
    main()
