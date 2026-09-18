// Original procedural shredder-run preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
// Matches ShredderInteractable.EjectionDelayMilliseconds (500ms): the item is
// inside the machine for exactly this long before it's spat back out.
const seconds = .5;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let seed = 205;
let growl = 0;
let humPhase = 0;
const ticks = [.08, .19, .27, .38];
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  growl += .15 * (noise - growl);

  const chug = Math.abs(Math.sin(2 * Math.PI * 16 * t));
  const grind = growl * (.4 + .35 * chug);

  humPhase += 2 * Math.PI * 110 / rate;
  const hum = .12 * Math.sin(humPhase);

  let tick = 0;
  for (const at of ticks) {
    const dt = t - at;
    if (dt < 0) continue;
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    const bite = seed / 2147483648 - 1;
    tick += bite * Math.exp(-dt / .006);
  }

  const attack = 1 - Math.exp(-t / .03);
  const releaseFade = Math.min(1, (frames - 1 - frame) / (rate * .05));
  samples[frame] = attack * releaseFade * (grind + hum + .22 * tick);
}
const peak = samples.reduce((largest, sample) => Math.max(largest, Math.abs(sample)), 0);
const wav = Buffer.alloc(44 + frames * 2);
wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 2, 28);
wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
wav.write('data', 36); wav.writeUInt32LE(frames * 2, 40);
for (let frame = 0; frame < frames; frame++) {
  const value = Math.round(samples[frame] / peak * .7 * 32767);
  assert.ok(Number.isFinite(value) && Math.abs(value) < 32767);
  wav.writeInt16LE(value, 44 + frame * 2);
}
const output = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.resolve(__dirname, '../../audio-previews/shredder-run.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
