#!/usr/bin/env python3
"""Link observed native chains/glows/secondary sheets as character-palette drafts."""
import importlib.util
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('batch',root/'scripts/draw-weapon-batch.py')
batch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(batch)
manifest_path = root/'Riakawa/Assets/manifest.json'
manifest = json.loads(manifest_path.read_text())
observations = json.loads((root/'assets/attack-bindings.json').read_text())
by_projectile = {}
for binding in observations['bindings']:
    by_projectile.setdefault(binding['projectileId'],{})[binding['slot'],binding['id']] = binding
items = {(w['character'],w['itemId']):w['texture'] for w in manifest['weapons']}
sizes_path = root/'assets/texture-sizes.json'
sizes = json.loads(sizes_path.read_text())
generated = {}

for weapon in manifest['weapons']:
    character = weapon['character']
    for attack in weapon['attacks']:
        for binding in by_projectile.get(attack['projectileId'],{}).values():
            slot,id = binding['slot'],binding['id']
            if slot == 'Projectile' and id == attack['projectileId']: continue
            # Keep separately drawn prototype bindings such as the whip line.
            existing = next((b for b in attack['bindings'] if b['slot']==slot and b['id']==id),None)
            if existing and not existing['texture'].startswith('Assets/Attacks/binding-'): continue
            key = (character,slot,id)
            source = Image.open(root/f'artifacts/qa/reference-textures/{slot}_{id}.png').convert('RGBA')
            dimensions = list(source.size)
            if f'{slot}/{id}' in sizes: assert sizes[f'{slot}/{id}'] == dimensions
            sizes[f'{slot}/{id}'] = dimensions
            if slot == 'Item' and (character,id) in items:
                path = items[character,id]
            elif key in generated:
                path = generated[key]
            else:
                if not source.getbbox(): continue
                # Preserve alpha and every native source rectangle; tint only.
                accent = batch.PALETTES[character]
                colors = [batch.mix(batch.INK,accent,.1),batch.mix(batch.INK,accent,.6),accent,
                          batch.mix(accent,(255,247,222),.55),batch.mix(accent,(255,250,242),.85)]
                pixels = []
                for r,g,b,a in source.get_flattened_data():
                    light = (r*299+g*587+b*114)/1000
                    index = min(len(colors)-1,int(light*len(colors)/256))
                    color = colors[index]
                    pixels.append((*color[:3],a))
                art = Image.new('RGBA',source.size);art.putdata(pixels)
                assert art.getchannel('A').tobytes() == source.getchannel('A').tobytes()
                path = f'Assets/Attacks/binding-{character.lower()}-{slot.lower()}-{id}'
                art.save(root/'Riakawa'/f'{path}.png')
                generated[key] = path
            if existing: existing['texture'] = path
            else: attack['bindings'].append(dict(slot=slot,id=id,texture=path))

manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
sizes_path.write_text(json.dumps(sizes,indent=2)+'\n')
print(f'PASS: {len(generated)} binding drafts; exact native canvas and alpha retained')
