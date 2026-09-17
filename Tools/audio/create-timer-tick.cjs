// Original procedural clock-tick preview: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .12;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
let seed = 205;
let clickResonator1 = 0, clickResonator2 = 0;
const clickResonance = .88;
let bodyPhase = 0;
let wood = 0;
// One analog-clock second. A wooden case thock plus a bright escapement click,
// triggered once per second by the hiding/searching countdown — not a UI beep,
// and not a baked tick-tock pair (that would double at the one-second interval).
const bodyFrequency = 920;
const clickFrequency = 3550;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  wood += .22 * (noise - wood);

  const clickAngle = 2 * Math.PI * clickFrequency / rate;
  const clicked = noise + 2 * clickResonance * Math.cos(clickAngle) * clickResonator1
    - clickResonance * clickResonance * clickResonator2;
  clickResonator2 = clickResonator1;
  clickResonator1 = clicked;
  const clickAttack = 1 - Math.exp(-t / .0005);
  const click = clicked * clickAttack * Math.exp(-t / .007);

  bodyPhase += 2 * Math.PI * bodyFrequency / rate;
  const bodyTone = Math.sin(bodyPhase) + .18 * Math.sin(bodyPhase * 2);
  const bodyAttack = 1 - Math.exp(-t / .0012);
  const body = (bodyTone + .12 * wood) * bodyAttack * Math.exp(-t / .032);

  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .012));
  samples[frame] = tailFade * (body * .72 + click * .55);
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
  : path.resolve(__dirname, '../../audio-previews/timer-tick.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -3.1 dBFS, no clipping.`);
