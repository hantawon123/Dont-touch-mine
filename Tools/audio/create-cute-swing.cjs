// Original procedural swing preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .18;
const frames = Math.round(seconds * rate);
const samples = new Float64Array(frames);
let seed = 205;
let phase = 0;
let lowNoise = 0;
let resonator1 = 0;
let resonator2 = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const u = t / seconds;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  lowNoise += .18 * (noise - lowNoise);
  // A fast upward, then downward sweep gives a playful "fwoop".
  const sweep = Math.sin(Math.PI * Math.min(1, u / .85));
  const frequency = 380 + 720 * sweep;
  phase += 2 * Math.PI * frequency / rate;
  const envelope = Math.pow(Math.sin(Math.PI * u), 1.6);
  const resonance = .94;
  const resonated = .07 * lowNoise + 2 * resonance * Math.cos(2 * Math.PI * frequency / rate)
    * resonator1 - resonance * resonance * resonator2;
  resonator2 = resonator1;
  resonator1 = resonated;
  // Mostly soft air, with a small rounded tone: less "weapon", more "toy".
  samples[frame] = envelope * (.62 * lowNoise + .22 * resonated + .055 * Math.sin(phase));
}
const peak = samples.reduce((v, s) => Math.max(v, Math.abs(s)), 0);
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
  : path.resolve(__dirname, '../../audio-previews/cute-punch-swing.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; no clipping.`);
