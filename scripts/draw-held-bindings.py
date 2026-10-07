#!/usr/bin/env python3
"""Preserve pinned native held animations/glows that bypass the item hook."""
import importlib.util
import json
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('batch',root/'scripts/draw-weapon-batch.py')
batch=importlib.util.module_from_spec(spec);spec.loader.exec_module(batch)
path=root/'Riakawa/Assets/manifest.json';manifest=json.loads(path.read_text())
sizes_path=root/'assets/texture-sizes.json';sizes=json.loads(sizes_path.read_text())
native=json.loads((root/'artifacts/qa/native-defaults.json').read_text())['items']
# Pinned DrawPlayer_27_HeldItem's direct branches and extra draw calls.
extras={1827:[('Item',1827)],3476:[('Extra',64),('GlowMask',195)],3779:[('Item',3779)],
        5118:[('Item',5118)],3788:[('GlowMask',220)]}
generated=set()
for weapon in manifest['weapons']:
    id=weapon['itemId'];fields=native[str(id)]['fields']
    bindings=list(extras.get(id,[]))
    if fields['glowMask']!=-1:bindings.append(('GlowMask',fields['glowMask']))
    if fields['flame'] and not fields['noUseGraphic']:bindings.append(('ItemFlame',id))
    weapon['heldBindings']=[]
    for slot,index in dict.fromkeys(bindings):
        source=Image.open(root/f'artifacts/qa/reference-textures/{slot}_{index}.png').convert('RGBA')
        sizes[f'{slot}/{index}']=list(source.size)
        name=f"Assets/Weapons/held-{weapon['character'].lower()}-{slot.lower()}-{index}"
        if slot=='Item':name=weapon['texture']
        elif name not in generated:
            accent=batch.PALETTES[weapon['character']]
            colors=[batch.mix(batch.INK,accent,.1),batch.mix(batch.INK,accent,.6),accent,
                    batch.mix(accent,(255,247,222),.55),batch.mix(accent,(255,250,242),.85)]
            art=Image.new('RGBA',source.size)
            art.putdata([(*colors[min(4,int((r*299+g*587+b*114)/1000*5/256))],a)
                         for r,g,b,a in source.get_flattened_data()])
            assert art.getchannel('A').tobytes()==source.getchannel('A').tobytes()
            art.save(root/'Riakawa'/f'{name}.png');generated.add(name)
        weapon['heldBindings'].append(dict(slot=slot,id=index,texture=name))
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
sizes_path.write_text(json.dumps(sizes,indent=2)+'\n')
print(f'PASS: {len(generated)} native-size held animation/glow sheets; alpha unchanged')
