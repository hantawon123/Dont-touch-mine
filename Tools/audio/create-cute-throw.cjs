// Original procedural throw preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .32;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let phase = 0;
let seed = 205;
let airNoise = 0;
// A clear whistling glide ("휘융"): quick rise then a longer, breathy fall,
// mostly tone rather than noise, unlike the punch swing's air-heavy fwoop.
const riseFraction = .28;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  const u = t / seconds;
  const sweep = u <= riseFraction
    ? Math.sin(Math.PI / 2 * (u / riseFraction))
    : Math.cos(Math.PI / 2 * ((u - riseFraction) / (1 - riseFraction)));
  const vibrato = 16 * Math.sin(2 * Math.PI * 7 * t) * Math.exp(-t / .22);
  const frequency = 480 + 900 * sweep + vibrato;
  phase += 2 * Math.PI * frequency / rate;
  const tone = Math.sin(phase) + .15 * Math.sin(phase * 2);
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  airNoise += .15 * (noise - airNoise);
  const ampAttack = 1 - Math.exp(-t / .006);
  const envelope = ampAttack * Math.exp(-t / .14);
  // A brief release tick right as the item leaves the hand.
  const pop = .18 * noise * Math.exp(-t / .003);
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .02));
  samples[frame] = tailFade * (envelope * (.75 * tone + .12 * airNoise) + pop);
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
  : path.resolve(__dirname, '../../audio-previews/cute-item-throw.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
