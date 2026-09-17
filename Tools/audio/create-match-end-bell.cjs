// Original procedural match-end bell; no external samples or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = 1.42;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
// Four even boxing/school-bell strokes: 땡 땡 땡 땡. Metallic inharmonics,
// not a pitched melody, so it reads as a round-ending bell rather than a chime.
const fundamental = 932.33;
const strikes = [
  { at: 0, decay: .22, amp: .92 },
  { at: .3, decay: .22, amp: .92 },
  { at: .6, decay: .22, amp: .92 },
  { at: .9, decay: .46, amp: 1 },
];
let seed = 205;
let air = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  air += .4 * (noise - air);
  let value = 0;
  for (const strike of strikes) {
    const lt = t - strike.at;
    if (lt < 0) continue;
    const attack = 1 - Math.exp(-lt / .002);
    const decay = Math.exp(-lt / strike.decay);
    const envelope = strike.amp * attack * decay;
    const bell =
      Math.sin(2 * Math.PI * fundamental * lt) +
      .42 * Math.sin(2 * Math.PI * fundamental * 2.76 * lt) +
      .2 * Math.sin(2 * Math.PI * fundamental * 5.404 * lt) +
      .1 * Math.sin(2 * Math.PI * fundamental * 8.93 * lt);
    value += envelope * bell;
    value += .08 * (noise - air) * attack * Math.exp(-lt / .012);
  }
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .04));
  samples[frame] = tailFade * value;
}
const peak = samples.reduce((largest, sample) => Math.max(largest, Math.abs(sample)), 0);
const wav = Buffer.alloc(44 + frames * 2);
wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 2, 28);
wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
wav.write('data', 36); wav.writeUInt32LE(frames * 2, 40);
for (let frame = 0; frame < frames; frame++) {
  const value = Math.round(samples[frame] / peak * .841 * 32767);
  assert.ok(Number.isFinite(value) && Math.abs(value) < 32767);
  wav.writeInt16LE(value, 44 + frame * 2);
}
const output = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.resolve(__dirname, '../../audio-previews/match-end-bell.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -1.5 dBFS, no clipping.`);
