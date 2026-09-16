// Original procedural preview. No recordings, samples, or AI audio service used.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .22;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let phase = 0;
let seed = 205;
let filteredNoise = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const attack = 1 - Math.exp(-t / .0015);
  // A rounded pitch drop provides a small plush/toy-like "pon".
  const frequency = 155 + 275 * Math.exp(-t / .022);
  phase += 2 * Math.PI * frequency / rate;
  const body = Math.sin(phase) * Math.exp(-t / .047);
  const overtone = .14 * Math.sin(phase * 2) * Math.exp(-t / .025);
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  filteredNoise += .16 * (noise - filteredNoise);
  // A soft, very short contact sound, rather than a sharp slap.
  const contact = .48 * filteredNoise * Math.exp(-t / .009);
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .02));
  samples[frame] = attack * (body + overtone + contact) * tailFade;
}
const peak = samples.reduce((largest, v) => Math.max(largest, Math.abs(v)), 0);
const wav = Buffer.alloc(44 + frames * 2);
wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 2, 28);
wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
wav.write('data', 36); wav.writeUInt32LE(frames * 2, 40);
for (let frame = 0; frame < frames; frame++) {
  const value = Math.round(samples[frame] / peak * .7 * 32767);
  assert.ok(value > -32768 && value < 32767);
  wav.writeInt16LE(value, 44 + frame * 2);
}
const output = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.resolve(__dirname, '../../audio-previews/soft-punch-pon.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s, mono PCM, peak -3.1 dBFS, no clipping.`);
