// Original procedural success-chime preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .46;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let seed = 205;
let sparkleNoise = 0;
// A bright "뾰로롱": a fast rising bell arpeggio (C5-E5-G5-C6-E6), each note
// overlapping the last so it reads as one sparkling run, not five separate dings.
const notes = [
  { at: 0, freq: 523.25, amp: .5 },
  { at: .045, freq: 659.25, amp: .55 },
  { at: .09, freq: 783.99, amp: .6 },
  { at: .135, freq: 1046.5, amp: .65 },
  { at: .18, freq: 1318.51, amp: .72 },
];
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  let tone = 0;
  for (const { at, freq, amp } of notes) {
    const lt = t - at;
    if (lt < 0) continue;
    const attack = 1 - Math.exp(-lt / .003);
    const decay = Math.exp(-lt / .11);
    const bell = Math.sin(2 * Math.PI * freq * lt) + .22 * Math.sin(2 * Math.PI * freq * 2 * lt);
    tone += amp * attack * decay * bell;
  }

  // A soft high sparkle riding the last, highest note.
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  sparkleNoise += .5 * (noise - sparkleNoise);
  const sparkleStart = .18;
  const st = Math.max(0, t - sparkleStart);
  const sparkleEnvelope = st > 0 ? (1 - Math.exp(-st / .004)) * Math.exp(-st / .05) : 0;
  const sparkle = .12 * sparkleEnvelope * (noise - sparkleNoise);

  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .03));
  samples[frame] = tailFade * (tone + sparkle);
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
  : path.resolve(__dirname, '../../audio-previews/success-chime.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
