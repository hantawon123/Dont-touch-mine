// Original procedural landing preview; no external samples or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .22;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let phase = 0;
let seed = 205;
let thud = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const attack = 1 - Math.exp(-t / .002);
  // Inverse of the jump spring: a rounded drop that settles into the floor.
  const drop = 1 - Math.exp(-t / .028);
  const frequency = 390 - 230 * drop;
  phase += 2 * Math.PI * frequency / rate;
  const tone = Math.sin(phase) + .14 * Math.sin(phase * 2);
  const envelope = attack * Math.exp(-t / .055);
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  thud += .16 * (noise - thud);
  const contact = .28 * thud * attack * Math.exp(-t / .016);
  const bounceT = t - .07;
  const bounce = bounceT < 0
    ? 0
    : .12 * Math.sin(2 * Math.PI * 180 * bounceT) *
      (1 - Math.exp(-bounceT / .002)) * Math.exp(-bounceT / .025);
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .02));
  samples[frame] = tailFade * (tone * envelope + contact + bounce);
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
  : path.resolve(__dirname, '../../audio-previews/cute-land.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
