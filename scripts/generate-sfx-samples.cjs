// npm install --prefix .tools/sfx jsfxr@1.4.1
const fs = require('node:fs');
const path = require('node:path');
const { sfxr } = require('../.tools/sfx/node_modules/jsfxr');
const out = path.resolve(__dirname, '../artifacts/samples/sfx');
fs.mkdirSync(out, { recursive: true });
let seed = 420;
const originalRandom = Math.random;
Math.random = () => { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296; };
try {
  for (const [name, preset] of [['soft-swish', 'laserShoot'], ['little-impact', 'hitHurt'], ['magic-sparkle', 'pickupCoin']]) {
    const sound = sfxr.generate(preset);
    sound.sound_vol = 0.16;
    sound.sample_rate = 44100;
    sound.sample_size = 16;
    if (name === 'soft-swish') { sound.wave_type = 3; sound.p_lpf_freq = 0.55; sound.p_env_sustain = 0.04; sound.p_env_decay = 0.17; }
    if (name === 'little-impact') { sound.p_env_sustain = 0.03; sound.p_env_decay = 0.13; }
    const data = Buffer.from(sfxr.toWave(sound).dataURI.split(',')[1], 'base64');
    if (data.toString('ascii', 0, 4) !== 'RIFF' || data.length <= 44) throw Error('Invalid WAV');
    fs.writeFileSync(path.join(out, name + '.wav'), data);
    fs.writeFileSync(path.join(out, name + '.json'), JSON.stringify({tool:'jsfxr',version:'1.4.1',status:'draft',sound},null,2));
    console.log('SFX_READY', name, data.length);
  }
} finally { Math.random = originalRandom; }
