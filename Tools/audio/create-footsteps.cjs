// Original procedural walk footsteps: no external recordings or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .18;
const frames = Math.round(rate * seconds);

function render(seed0) {
  const samples = new Float64Array(frames);
  let seed = seed0;
  let thud = 0;
  let grit = 0;
  let phase = 0;
  for (let frame = 0; frame < frames; frame++) {
    const t = frame / rate;
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    const noise = seed / 2147483648 - 1;
    thud += .2 * (noise - thud);
    grit += .45 * (noise - grit);
    const attack = 1 - Math.exp(-t / .0015);
    const heel = attack * Math.exp(-t / .018);
    const body = attack * Math.exp(-t / .045);
    const frequency = 190 + (seed0 % 40) - 80 * (1 - Math.exp(-t / .03));
    phase += 2 * Math.PI * frequency / rate;
    const tone = Math.sin(phase) + .12 * Math.sin(phase * 2);
    const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .02));
    samples[frame] = tailFade * (
      .55 * thud * heel +
      .22 * grit * attack * Math.exp(-t / .012) +
      .28 * tone * body);
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
  return wav;
}

const seeds = [205, 409, 811, 1027, 1601, 2027, 2741, 3253];
const dir = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.resolve(__dirname, '../../Assets/_Game/Content/Audio/Footsteps');
fs.mkdirSync(dir, { recursive: true });
seeds.forEach((seed, index) => {
  const name = `Footstep_${String(index + 1).padStart(2, '0')}.wav`;
  fs.writeFileSync(path.join(dir, name), render(seed));
});
const preview = path.resolve(__dirname, '../../audio-previews/footstep.wav');
fs.mkdirSync(path.dirname(preview), { recursive: true });
fs.writeFileSync(preview, render(seeds[0]));
console.log(`Created 8 mono PCM footsteps in ${dir}: ${seconds}s each; peak -3.1 dBFS, no clipping.`);
