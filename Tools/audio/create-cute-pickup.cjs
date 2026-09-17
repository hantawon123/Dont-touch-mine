// Original procedural pickup preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .16;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let phase = 0;
let seed = 205;
let puffNoise = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const attack = 1 - Math.exp(-t / .002);
  // A quick rising chirp: the light "lift" of a small item leaving the ground.
  const rise = 1 - Math.exp(-t / .018);
  const wobble = 10 * Math.sin(2 * Math.PI * 34 * t) * Math.exp(-t / .05);
  const frequency = 520 + 360 * rise + wobble;
  phase += 2 * Math.PI * frequency / rate;
  const tone = Math.sin(phase) + .18 * Math.sin(phase * 2);
  const envelope = attack * Math.exp(-t / .052);
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  puffNoise += .3 * (noise - puffNoise);
  // A tiny, soft finger-contact puff right at the grab, gone almost instantly.
  const puff = .22 * puffNoise * Math.exp(-t / .006);
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .015));
  samples[frame] = tailFade * (tone * envelope + puff);
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
  : path.resolve(__dirname, '../../audio-previews/cute-item-pickup.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
