// Deterministic, original procedural drafts. No game/anime audio is sampled.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { sfxr } = require('../.tools/sfx/node_modules/jsfxr');
const root = path.resolve(__dirname, '..');
const read = name => JSON.parse(fs.readFileSync(path.join(root, name), 'utf8'));
const manifest = read('Riakawa/Assets/manifest.json');
const catalog = read('assets/vanilla-catalog.json');
const items = new Map(catalog.weapons.map(w => [w.id, w]));
const tracePath = path.join(root, 'assets/attack-sounds.json');
const trace = fs.existsSync(tracePath) ? JSON.parse(fs.readFileSync(tracePath, 'utf8')).cues : [];
const base = read('artifacts/samples/sfx/soft-swish.json').sound;
const made = new Map();
let seed;
const random = Math.random;
Math.random = () => { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296; };

function generate(character, key, item, impact = false) {
  const chord = item.id === 4715 && !impact ? key.match(/Item_(13[3-8])$/)?.[1] : null;
  const family = chord ? `chord${chord}` : impact ? 'impact' : item.damageClass.includes('SummonMelee') ? 'whip' :
    item.damageClass.includes('Summon') ? 'summon' : item.damageClass.includes('Magic') ? 'magic' :
    item.useAmmo === 40 ? 'bow' : item.damageClass.includes('Ranged') ? 'shot' : 'swish';
  const identity = `${character}/${family}`;
  if (made.has(identity)) {
    const cue = made.get(identity);
    if (!cue.sourceCues.includes(key)) cue.sourceCues.push(key);
    return cue.asset;
  }
  const digest = crypto.createHash('sha256').update(identity).digest('hex').slice(0, 12);
  seed = parseInt(digest.slice(0, 8), 16);
  // wave, frequency, slide, sustain, decay: short soft noise, plucks and chimes.
  const params = { swish:[3,.4,-.12,.025,.23], whip:[3,.5,-.2,.015,.24],
    bow:[2,.26,-.08,.025,.29], shot:[3,.44,-.18,.015,.2],
    magic:[2,.48,.015,.05,.4], summon:[2,.36,.1,.07,.43], impact:[3,.2,-.1,.04,.3] }[chord ? 'magic' : family];
  const [wave, freq, slide, sustain, decay] = params;
  const tune = {Chiikawa:.95,Hachiware:1.04,Usagi:1.15}[character];
  const sound = {...base, wave_type:wave, p_base_freq:freq*tune*(.97+Math.random()*.06),
    p_freq_limit:0, p_freq_ramp:slide, p_env_attack:.05, p_env_sustain:sustain,
    p_env_decay:decay, p_lpf_freq:wave===3?.6:.85, p_hpf_freq:0,
    p_duty:0, p_duty_ramp:0, p_arp_mod:family==='magic'?.15:0,
    p_arp_speed:family==='magic'?.7:0, sound_vol:.24};
  let data = Buffer.from(sfxr.toWave(sound).dataURI.split(',')[1], 'base64');
  let notes = null;
  if (chord) {
    // C, D, Em, G, Bm, Am: retain all six instrument choices using new triads.
    const [rootNote,minor] = {133:[261.6256,false],134:[293.6648,false],135:[329.6276,true],
      136:[391.9954,false],137:[246.9417,true],138:[220,true]}[chord];
    notes = [0,minor?3:4,7].map(n => rootNote*2**(n/12)*(character==='Usagi'?2:1));
    const parts = notes.map(hz => Buffer.from(sfxr.toWave({...sound, wave_type:character==='Hachiware'?1:2,
      p_base_freq:Math.sqrt(hz*100/(8*44100)-.001),p_freq_ramp:0,p_arp_mod:0,p_arp_speed:0,
      p_lpf_freq:.55}).dataURI.split(',')[1],'base64'));
    assert(parts.every(p => p.length===parts[0].length));
    data = Buffer.from(parts[0]);
    for (let i=44;i<data.length;i+=2) data.writeInt16LE(Math.round(parts.reduce((n,p)=>n+p.readInt16LE(i),0)/3),i);
  }
  assert.equal(data.toString('ascii',0,4),'RIFF');
  assert(data.length>44);
  const asset = `Assets/Sounds/Weapons/${character.toLowerCase()}-${family}-${digest}`;
  fs.mkdirSync(path.join(root, 'Riakawa/Assets/Sounds/Weapons'), {recursive:true});
  fs.writeFileSync(path.join(root, 'Riakawa', asset+'.wav'),data);
  made.set(identity,{asset,character,sourceCues:[key],family,sound,notes,status:'draft'});
  return asset;
}
try {
  for (const weapon of manifest.weapons) {
    const item = items.get(weapon.itemId);
    if (item.sound) weapon.useSound = generate(weapon.character,item.sound,item);
    weapon.sounds ??= {};
    for (const cue of trace.filter(s => s.weaponId===weapon.itemId && s.projectileId===0)) {
      if (cue.sound!==item.sound && /^Terraria\/Sounds\/(Item_|Custom\/)/.test(cue.sound))
        weapon.sounds[cue.sound] = generate(weapon.character,cue.sound,item);
    }
    for (const attack of weapon.attacks) {
      for (const cue of trace.filter(s => s.projectileId===attack.projectileId)) {
        // These two NPC-named clips are also emitted by native friendly attacks.
        // Actual NPC strikes/effects suppress attack ownership in VisualHooks.
        if (!/^Terraria\/Sounds\/(Item_|Custom\/|Dig$|Tink$|NPC_Killed_(17|19)$)/.test(cue.sound)) continue;
        attack.sounds[cue.sound] = generate(weapon.character,cue.sound,item,true);
      }
    }
  }
} finally { Math.random = random; }
const retained = new Set([...made.values()].map(c => path.basename(c.asset)+'.wav'));
for (const file of fs.readdirSync(path.join(root,'Riakawa/Assets/Sounds/Weapons'))) {
  if (/^(chiikawa|hachiware|usagi)-[a-z0-9]+-[a-f0-9]{12}\.wav$/.test(file) && !retained.has(file))
    fs.unlinkSync(path.join(root,'Riakawa/Assets/Sounds/Weapons',file));
}
fs.writeFileSync(path.join(root,'Riakawa/Assets/manifest.json'),JSON.stringify(manifest,null,2)+'\n');
fs.writeFileSync(path.join(root,'assets/weapon-sounds.json'),JSON.stringify({tool:'jsfxr',version:'1.4.1',
  status:'draft',listeningReviewed:false,cues:[...made.values()]},null,2)+'\n');
console.log(`Wrote ${made.size} sound drafts; ${manifest.weapons.filter(w=>w.useSound).length} weapon use mappings`);
