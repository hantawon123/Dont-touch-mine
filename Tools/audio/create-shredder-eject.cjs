// Original procedural shredder-eject preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .18;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let seed = 205;
let airNoise = 0;
let resonator1 = 0;
let resonator2 = 0;
let phase = 0;
const frequency = 700;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  airNoise += .25 * (noise - airNoise);

  // A small metallic clack (the ejection flap) right at the pop.
  const clack = .3 * noise * Math.exp(-t / .004);

  // A compact, resonant pressurised-air pop — mechanical, not a hand throw.
  const resonance = .93;
  const resonated = .1 * airNoise + 2 * resonance * Math.cos(2 * Math.PI * frequency / rate)
    * resonator1 - resonance * resonance * resonator2;
  resonator2 = resonator1;
  resonator1 = resonated;
  const popEnvelope = Math.exp(-t / .035);

  // A quick departing chirp as the item clears the port.
  const chirpFrequency = 260 + 300 * (1 - Math.exp(-t / .02));
  phase += 2 * Math.PI * chirpFrequency / rate;
  const chirpEnvelope = (1 - Math.exp(-t / .002)) * Math.exp(-t / .05);
  const chirp = chirpEnvelope * .3 * Math.sin(phase);

  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .02));
  samples[frame] = tailFade * (clack + popEnvelope * (.5 * airNoise + .35 * resonated) + chirp);
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
  : path.resolve(__dirname, '../../audio-previews/shredder-eject.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
