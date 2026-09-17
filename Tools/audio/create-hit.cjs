// Original procedural item-hit preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .26;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let phase = 0;
let seed = 205;
let clackNoise = 0;
// A harmless comedic "bonk": a hollow knock, not a punch-weight "pon".
// A bright clack marks the object's edge, then a light boing wobble as it
// bounces off — the player takes no damage, so nothing here should read heavy.
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const attack = 1 - Math.exp(-t / .0015);
  const frequency = 220 + 180 * Math.exp(-t / .016);
  phase += 2 * Math.PI * frequency / rate;
  const body = Math.sin(phase) * Math.exp(-t / .055);
  const overtone = .16 * Math.sin(phase * 2.4) * Math.exp(-t / .03);
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  clackNoise += .35 * (noise - clackNoise);
  const clack = .4 * clackNoise * Math.exp(-t / .005);
  // A short, decaying "boing" wobble starting right after the knock.
  const boingStart = .028;
  const boingT = Math.max(0, t - boingStart);
  const boingAttack = boingT > 0 ? 1 - Math.exp(-boingT / .003) : 0;
  const boingFrequency = 95 + 40 * Math.exp(-boingT / .05);
  const boing = .22 * boingAttack * Math.sin(2 * Math.PI * boingFrequency * boingT) * Math.exp(-boingT / .09);
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .02));
  samples[frame] = attack * tailFade * (body + overtone + clack + boing);
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
  : path.resolve(__dirname, '../../audio-previews/item-hit.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
