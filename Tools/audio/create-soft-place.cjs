// Original procedural placement preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .24;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let phase = 0;
let seed = 205;
let filteredNoise = 0;
function settleBounce(t, startAt, frequency, amplitude) {
  const localT = t - startAt;
  if (localT < 0) return 0;
  const attack = 1 - Math.exp(-localT / .002);
  const decay = Math.exp(-localT / .02);
  return amplitude * Math.sin(2 * Math.PI * frequency * localT) * attack * decay;
}
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const attack = 1 - Math.exp(-t / .002);
  // A warm, rounded pitch drop: an item settling gently into place, not a hit.
  const frequency = 210 + 190 * Math.exp(-t / .032);
  phase += 2 * Math.PI * frequency / rate;
  const body = Math.sin(phase) * Math.exp(-t / .07);
  const overtone = .12 * Math.sin(phase * 2) * Math.exp(-t / .04);
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  filteredNoise += .1 * (noise - filteredNoise);
  const contact = .34 * filteredNoise * Math.exp(-t / .014);
  // Two small settle bounces after the main contact, as the item finds its rest.
  const settle1 = settleBounce(t, .09, 130, .1);
  const settle2 = settleBounce(t, .15, 150, .06);
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .02));
  samples[frame] = attack * tailFade * (body + overtone + contact + settle1 + settle2);
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
  : path.resolve(__dirname, '../../audio-previews/soft-item-place.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
