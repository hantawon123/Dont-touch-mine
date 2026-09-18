// Original procedural shredder preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .42;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let seed = 205;
let growl = 0;
let gulpPhase = 0;
// A toy-machine chomp: a chugging grind bed, a few crunch bites that fade
// as the item disappears, and a small closing "gulp" thunk. Comedic, not
// gruesome — matches a plush-toy cast, not a real shredder.
const crunches = [
  { at: .015, amp: 1 },
  { at: .085, amp: .82 },
  { at: .155, amp: .68 },
  { at: .225, amp: .55 },
  { at: .30, amp: .42 },
];
const gulpStart = .335;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  growl += .12 * (noise - growl);

  const chug = Math.abs(Math.sin(2 * Math.PI * 17 * t));
  const grindAttack = 1 - Math.exp(-t / .02);
  const grindDecay = Math.exp(-t / .5);
  const grindSample = growl * grindAttack * grindDecay * (.35 + .35 * chug);

  let crunchSample = 0;
  for (const { at, amp } of crunches) {
    const dt = t - at;
    if (dt < 0) continue;
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    const bite = seed / 2147483648 - 1;
    crunchSample += amp * bite * Math.exp(-dt / .012);
  }
  crunchSample *= .3;

  const gt = Math.max(0, t - gulpStart);
  const gulpFrequency = 70 + 170 * Math.exp(-gt / .05);
  gulpPhase += 2 * Math.PI * gulpFrequency / rate;
  const gulpAttack = gt > 0 ? 1 - Math.exp(-gt / .004) : 0;
  const gulpSample = gulpAttack * Math.exp(-gt / .06) * .4 * Math.sin(gulpPhase);

  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .025));
  samples[frame] = tailFade * (grindSample + crunchSample + gulpSample);
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
  : path.resolve(__dirname, '../../audio-previews/item-shred.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
