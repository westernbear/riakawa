"""Audit recorded native attack draws; never convert missing evidence into approval."""
import hashlib,json,re
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1]
read=lambda p:json.loads((R/p).read_text())
m=read('Riakawa/Assets/manifest.json')
art={(w['itemId'],w['character']):w for w in m['weapons']}
chars=('Chiikawa','Hachiware','Usagi')
sets={}
for name,count in [('standard',426),('ammo',114),('minions',38)]:
 base=f'artifacts/qa/appearance-{name}'
 s=read(base+'/sweep.json')
 assert not s['running'] and len(s['completed'])==count,(name,len(s['completed']))
 if name=='standard':assert {x['itemId'] for x in s['completed']}=={w['itemId'] for w in m['weapons']}
 if name=='ammo':
  catalog=read('assets/vanilla-catalog.json');pairs={}
  for row in catalog['ammoProjectiles']:pairs.setdefault(row['projectileId'],(row['weaponId'],row['ammoId']))
  assert {(x['itemId'],x['ammoId']) for x in s['completed']}==set(pairs.values())
  missing=[(x['itemId'],x['ammoId']) for x in s['completed'] if not x['observedProjectiles']]
  assert missing==[(506,23)],missing
  retest=read(base+'/retest-sweep.json')
  assert not retest['running'] and any(x['itemId']==506 and 85 in x['observedProjectiles'] for x in retest['completed'])
  state=read(base+'/retest-state.json');owner=next(p for p in state['players'] if p['id']==state['myPlayer'])
  assert owner['heldItem']==506 and any(a['slot']==54 and a['type']==23 for a in owner['ammunition'])
 sets[name]=read(base+'/render-audit.json')
 for e in read(base+'/attacks.json'):
  for c in chars:assert any(a['projectileId']==e['projectileId'] for a in art[e['weaponId'],c]['attacks']),(name,e,c)
observed={}
for file in ['appearance-standard/sweep.json','appearance-ammo/sweep.json','appearance-minions/sweep.json',
             'appearance-ammo/retest-sweep.json','appearance-replay/blaster-mace-retest.json']:
 for row in read('artifacts/qa/'+file)['completed']:observed.setdefault(row['itemId'],set()).update(row['observedProjectiles'])
assert all(observed.get(w['id']) for w in read('assets/vanilla-catalog.json')['weapons'] if w['shoot']>0)
replay=read('artifacts/qa/appearance-replay/render-audit.json');sets['replay']=replay
# Native 1.4.4.9 bugs, retained exactly: negative Phantasm muzzle frames,
# Book Staff's initial random 0..13 against 8 frames, rotated blaster glow.
# A native replay must also exhibit each category; all other bounds failures fail.
def native_quirk(line):
 key,_,rest=line.partition(' source outside ')
 if not rest:return None
 weapon,pid,_=key.split('/')
 match=re.search(r'\{X:(-?\d+) Y:(-?\d+) Width:(\d+) Height:(\d+)\}',rest)
 if not match:return None
 x,y,w,h=map(int,match.groups())
 if (weapon,pid)==('2882','460') and ('glowmask-102 ' in rest or 'GlowMask/102 ' in rest) and (x,y,w,h)==(0,0,14,18):return 'charged-blaster-glow'
 if (weapon,pid)==('3540','630') and ('extra-65 ' in rest or 'Extra/65 ' in rest) and x==0 and w==36 and h==46 and y in range(-276,0,46):return 'phantasm-negative-frame'
 if (weapon,pid)==('3852','712') and ('-712 ' in rest or 'Projectile/712 ' in rest) and x==0 and w==14 and h==18 and y in range(144,252,18):return 'book-staff-initial-frame'
 if (weapon,pid)==('3569','642') and ('-642 ' in rest or 'Projectile/642 ' in rest) and (x,y,w,h)==(0,154,22,30):return 'turret-laser-end'
 return None
native={native_quirk(x) for x in replay['failures'] if '/Original source outside ' in x}
assert {'charged-blaster-glow','phantasm-negative-frame','book-staff-initial-frame','turret-laser-end'}<=native,native
fixed=[(1297,262,'Chain22',0),(1314,271,'Chain18',0),(1325,273,'Chain23',0),
 (2424,383,'Chain34',0),(2611,404,'Chain37',0),(3012,481,'Chain40',0),(3475,616,'GlowMask',193)]
for wid,pid,slot,id in fixed:
 for c in chars:
  a=next(a for a in art[wid,c]['attacks'] if a['projectileId']==pid)
  binding=next(b for b in a['bindings'] if (b['slot'],b['id'])==(slot,id))
  assert any(x.startswith(binding['texture']+' ') for x in replay['rows'].get(f'{wid}/{pid}/{c}',[])),(wid,pid,c)
for c in chars:assert any('batch-'+c.lower()+'-405 ' in x for x in replay['rows'].get('2611/405/'+c,[]))
quirks=[];fixed_old=[]
for name,data in sets.items():
 for fail in data['failures']:
  if native_quirk(fail):quirks.append((name,fail));continue
  if name!='replay' and re.fullmatch(r'unmapped attack 2611/405/(Chiikawa|Hachiware|Usagi)',fail):fixed_old.append(fail);continue
  raise AssertionError((name,fail))
 for key,values in data['rows'].items():
  if key.endswith('/Original'):continue
  wid,pid,c=key.split('/');a=next(a for a in art[int(wid),c]['attacks'] if a['projectileId']==int(pid))
  for value in values:
   if not value.startswith('native:'):continue
   slot,id=value[7:].split(' ',1)[0].split('/');id=int(id)
   ref=R/f'artifacts/qa/reference-textures/{slot}_{id}.png'
   if ref.exists() and Image.open(ref).convert('RGBA').getchannel('A').getextrema()[1]==0:continue
   assert name!='replay' and (int(wid),int(pid),slot,id) in fixed,(name,key,value)
body=read('artifacts/qa/appearance-movement-one.body-checks.json')
for character in chars:
 for state in ['walk','jump','mount','swim']:
  assert any(x['character']==character and x['expected']==state and x['itemAnimation']>0 for x in body),(character,state)
assert any(x['expected']=='jump' and x['gravDir']==-1 and x['itemAnimation']>0 for x in body)
assert any(x['expected']=='invisible' and x['invis'] and x['frame'] is None for x in body)
assert any(x['expected']=='death' for x in body)
remote=read('artifacts/qa/appearance-scenes/remote-chiikawa-melee.json')
assert remote['configCharacter']=='Usagi'
assert any(p['id']!=remote['myPlayer'] and p['character']=='Chiikawa' and p['itemAnimation']>0 for p in remote['players'])
remote_draw=read('artifacts/qa/appearance-movement-two.render-audit.json')
assert not remote_draw['failures']
assert any(x.startswith('Assets/Attacks/batch-chiikawa-173 ') for x in remote_draw['rows']['989/173/Chiikawa'])
report={'status':'pass','standardWeapons':426,'ammoCases':114,'singleCastSummons':38,'attackingLocomotionCases':12,
 'remoteOwnerAppearance':True,
 'renderRows':{k:len(v['rows']) for k,v in sets.items()},'nativeRectangleWarnings':quirks,
 'repairedOldMissingAttacks':fixed_old,'replayedBindings':fixed,
 'scope':'Observed native draw paths and explicit native quirks; all rare AI/environment/equipment combinations are not implied'}
(R/'artifacts/qa/appearance-checks.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS:',{k:v for k,v in report.items() if k not in ['nativeRectangleWarnings','repairedOldMissingAttacks','replayedBindings']})
