"""Check the fourth character's complete assets and the shared particle regression."""
import hashlib
import io
import json
import subprocess
import wave
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'Riakawa/Assets/manifest.json').read_text())
characters=['Chiikawa','Hachiware','Usagi','Momonga']
assert [p['character'] for p in manifest['players']]==characters
art=next(p for p in manifest['players'] if p['character']=='Momonga')
sheet=Image.open(ROOT/'Riakawa'/f"{art['texture']}.png")
assert sheet.size==(160,504) and sheet.mode=='RGBA'
for animation,row in art['animations'].items():
    for frame in range(row['frames']):
        cell=sheet.crop((frame*40,row['row']*56,frame*40+40,row['row']*56+56))
        assert cell.getbbox() and cell.getbbox()[3]<=50,(animation,frame)
assert set(art['voices'])=={'emote','hurt','death'}
voices={}
for cue,path in art['voices'].items():
    with wave.open(str(ROOT/'Riakawa'/f'{path}.wav')) as f:
        assert (f.getnchannels(),f.getsampwidth(),f.getframerate())==(1,2,44100)
        duration=f.getnframes()/f.getframerate()
        assert 0<duration<{'emote':5,'hurt':1.5,'death':3.5}[cue]
        voices[cue]=duration
weapons={(w['itemId'],w['character']):w for w in manifest['weapons']}
catalog=json.loads((ROOT/'assets/vanilla-catalog.json').read_text())
assert len(weapons)==len(catalog['weapons'])*4==1704
textures=set()
for weapon in catalog['weapons']:
    id=weapon['id'];base=weapons[id,'Chiikawa'];new=weapons[id,'Momonga']
    def coverage(w):
        return ([(b['slot'],b['id']) for b in w.get('heldBindings',[])],
            [(a['projectileId'],bool(a['texture']),a.get('visualMode'),[(b['slot'],b['id']) for b in a['bindings']],sorted(a['sounds'])) for a in w['attacks']])
    assert coverage(base)==coverage(new),id
    hashes={hashlib.sha256((ROOT/'Riakawa'/f"{weapons[id,c]['texture']}.png").read_bytes()).hexdigest() for c in characters}
    assert len(hashes)==4,id
    textures.add(new['texture'])
    for b in new.get('heldBindings',[]):textures.add(b['texture'])
    for a in new['attacks']:
        if a['texture']:textures.add(a['texture'])
        for b in a['bindings']:textures.add(b['texture'])
    assert not new.get('useSound') or '/momonga-' in new['useSound']
assert all('momonga' in path for path in textures)
atlas=Image.open(ROOT/'Riakawa/Assets/Particles/atlas.png').convert('RGBA')
assert atlas.size==(1000,132) and atlas.crop((0,0,1000,120)).getbbox() is None
for character in range(4):
    for frame in range(3):assert atlas.crop((character*30+frame*10,122,character*30+frame*10+8,130)).getbbox()
assert atlas.crop((120,120,130,132)).getbbox() is None,'Reduced effects must not use Momonga pixels'
old=Image.open(io.BytesIO(subprocess.check_output(['git','show','v0.1.0:Riakawa/Assets/Particles/atlas.png']))).convert('RGBA')
assert atlas.crop((0,0,90,132)).tobytes()==old.crop((0,0,90,132)).tobytes()
for character in characters[:3]:
    path=f'Riakawa/Assets/Players/{character.lower()}.png'
    assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show','v0.1.0:'+path])
report={'status':'pass','character':'Momonga','animations':len(art['animations']),'frames':36,
    'weaponVariants':426,'textures':len(textures),'voicesSeconds':voices,'allAttackBindingsMatchExistingCatalog':True,
    'fourDistinctWeaponDesigns':True,'existingPlayerSheetsAndParticleCellsUnchanged':True,'reducedEffectCellEmpty':True}
out=ROOT/'artifacts/qa/momonga';out.mkdir(parents=True,exist_ok=True)
(out/'assets.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS:',report)
