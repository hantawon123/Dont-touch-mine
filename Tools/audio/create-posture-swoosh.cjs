// Original procedural posture-swoosh preview; no external samples or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .24;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let seed = 205;
let air = 0;
let cloth = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const u = t / seconds;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  // A soft cloth "스윽": band-limited air with a downward sweep, not a punch.
  const cutoff = .22 + .18 * (1 - u);
  air += cutoff * (noise - air);
  cloth += .08 * (air - cloth);
  const envelope = Math.pow(Math.sin(Math.PI * Math.min(1, u / .92)), 1.35);
  const rustle = .35 * air * Math.sin(2 * Math.PI * (420 - 180 * u) * t);
  samples[frame] = envelope * (air * .85 + cloth * .4 + rustle);
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
  : path.resolve(__dirname, '../../audio-previews/posture-swoosh.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
