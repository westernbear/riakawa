#!/usr/bin/env python3
"""Draft native projectile sheets without changing frame boundaries or alpha geometry.

Fully invisible primary textures need secondary bindings and stay pending. This
does not discover secondary projectiles, ammo variants, glow masks or sound cues.
"""
import importlib.util
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('batch',ROOT/'scripts/draw-weapon-batch.py')
batch=importlib.util.module_from_spec(spec)
spec.loader.exec_module(batch)


def main():
    assert json.loads((ROOT/'assets/art-review.json').read_text())['weaponDirectionApproved']
    manifest_path=ROOT/'Riakawa/Assets/manifest.json'
    manifest=json.loads(manifest_path.read_text())
    catalog=json.loads((ROOT/'assets/vanilla-catalog.json').read_text())
    projectiles={p['id']:p for p in catalog['projectiles']}
    observation_path=ROOT/'assets/attack-observations.json'
    observations=json.loads(observation_path.read_text())['edges'] if observation_path.exists() else []
    children={}
    for edge in observations:
        if edge['parentProjectileId'] > 0:
            children.setdefault(edge['parentProjectileId'],set()).add(edge['projectileId'])
    for weapon in manifest['weapons']:
        existing={a['projectileId'] for a in weapon['attacks']}
        discovered={a['projectileId'] for a in catalog.get('ammoProjectiles',[])+observations
                    if a['weaponId']==weapon['itemId']}
        # Native child AI is shared by projectile type. Carry observed splits
        # to every weapon that fires that ammunition, including later splits.
        reachable=existing|discovered
        pending=list(reachable)
        while pending:
            for child in children.get(pending.pop(),()):
                if child not in reachable:
                    reachable.add(child);pending.append(child)
        discovered=reachable
        assert discovered <= projectiles.keys(), weapon['itemId']
        for id in sorted(discovered-existing):
            weapon['attacks'].append({'projectileId':id,'texture':None,'bindings':[],'sounds':{}})
    output=ROOT/'Riakawa/Assets/Attacks'
    output.mkdir(exist_ok=True)
    # Reuse the separately drawn approved-direction prototype attack for shared ammo.
    known={(w['character'],a['projectileId']):a for w in manifest['weapons'] for a in w['attacks']
           if a.get('texture') and '/batch-' not in a['texture']}
    generated={}
    pending=set()
    preview=Image.new('RGB',(864,720),'#ebe9e3')
    selected=[1,3,6,15,16,19,22,26,27,94,118,132,157,191,208,317,390,494]
    for weapon in manifest['weapons']:
        name=weapon['character']
        for attack in weapon['attacks']:
            id=attack['projectileId'];key=(name,id)
            if key in known:
                attack.update(known[key]);continue
            if key in generated:
                attack.update(generated[key]);continue
            source=Image.open(ROOT/f'artifacts/qa/reference-textures/Projectile_{id}.png').convert('RGBA')
            alpha=source.getchannel('A')
            if alpha.getextrema()[1] == 0:
                # Native AI 48/50/51 and CrystalPulse/Paintball draw these with
                # Dust.NewDust. The dedicated atlas replaces their visible art.
                if id in (255,260,294,295,296,297,305,309,357,433,521,522,524,587,619,620,624,698):
                    attack['visualMode']='particles'
                elif not attack.get('bindings'):
                    pending.add(id)
                continue
            data=projectiles[id]
            # Whip renderers split their native sheets into five segments, not
            # Main.projFrames. Ordinary sprite animation uses integer division.
            frames=5 if data['whip'] else max(1,data['frames'])
            frame_height=source.height//frames
            assert frame_height > 0, id
            image=source.copy()
            for frame in range(frames):
                box=(0,frame*frame_height,source.width,(frame+1)*frame_height)
                part=source.crop(box)
                if not part.getbbox(): continue
                art=batch.redraw(part,{'id':id,'useAmmo':0,'consumable':True,'damageClass':'Projectile'},name)
                art.putalpha(part.getchannel('A'))
                image.paste(art,(0,frame*frame_height))
            assert image.size == source.size and image.getchannel('A').tobytes() == alpha.tobytes()
            path=f'Assets/Attacks/batch-{name.lower()}-{id}'
            image.save(ROOT/'Riakawa'/f'{path}.png')
            bindings=list(attack.get('bindings',[]))
            if data['whip']:
                # Native DrawWhip uses this shared line only inside our scoped hook.
                if not any(b['slot']=='FishingLine' and b['id']==0 for b in bindings):
                    bindings.append({'slot':'FishingLine','id':0,'texture':f'Assets/Weapons/{name.lower()}-4672-line'})
            entry={'texture':path,'bindings':bindings}
            generated[key]=entry
            attack.update(entry)
            if id in selected:
                index=selected.index(id);x=(index%3)*288+list(batch.PALETTES).index(name)*96;y=(index//3)*120
                thumb=image.crop((0,0,image.width,frame_height))
                thumb.thumbnail((86,88),Image.Resampling.NEAREST)
                scale=min(3,86//thumb.width,88//thumb.height)
                thumb=thumb.resize((thumb.width*scale,thumb.height*scale),Image.Resampling.NEAREST)
                preview.paste(thumb,(x+(96-thumb.width)//2,y),thumb)
                ImageDraw.Draw(preview).text((x,y+96),f'{id} {name[:3]}',fill=batch.INK)
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    preview.save(ROOT/'artifacts/samples/pixel-polished/attack-batch-preview.png')
    report={'generatedSheets':len(generated),'invisiblePrimaryNeedsBindings':sorted(pending),
            'coverageReviewed':False,'alphaGeometryPreserved':True,
            'sharedChildren':'transitive closure of observed native parent/child types; inferred across weapons, still requires review',
            'remaining':'unobserved attack/alternate-use branches, secondary bindings, listening and individual art review'}
    (ROOT/'artifacts/attack-batch.json').write_text(json.dumps(report,indent=2)+'\n')
    print(f'PASS: {len(generated)} draft attack sheets; {len(pending)} invisible types need binding review')


if __name__=='__main__':
    main()
