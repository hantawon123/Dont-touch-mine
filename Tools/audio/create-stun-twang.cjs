// Original procedural stun "띠용용용용" preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .92;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
// Cartoon dizzy spring: one bright "띠용" then four echoing "용"s, each lower
// and quieter, with a downward pitch slide so it reads as 띠용용용용.
const notes = [
  { at: 0, freq: 1174.66, slide: -260, amp: .9, decay: .16 },
  { at: .15, freq: 987.77, slide: -210, amp: .72, decay: .17 },
  { at: .3, freq: 830.61, slide: -170, amp: .56, decay: .18 },
  { at: .45, freq: 698.46, slide: -140, amp: .42, decay: .2 },
  { at: .6, freq: 587.33, slide: -110, amp: .3, decay: .22 },
];
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  let tone = 0;
  for (const note of notes) {
    const lt = t - note.at;
    if (lt < 0) continue;
    const attack = 1 - Math.exp(-lt / .004);
    const decay = Math.exp(-lt / note.decay);
    const freq = note.freq + note.slide * (1 - Math.exp(-lt / .05));
    const wobble = 1 + .035 * Math.sin(2 * Math.PI * 18 * lt) * Math.exp(-lt / .08);
    const phase = 2 * Math.PI * freq * wobble * lt;
    const bell = Math.sin(phase) + .2 * Math.sin(phase * 2);
    tone += note.amp * attack * decay * bell;
  }
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .04));
  samples[frame] = tailFade * tone;
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
  : path.resolve(__dirname, '../../audio-previews/stun-twang.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
