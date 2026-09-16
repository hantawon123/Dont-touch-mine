// Original procedural 30-second warning chime; no external samples or audio models.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = .95;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
// Two rising pips, E5 then A5, on the same soft bell tone the other effects use.
// The held note breathes at 6.5 Hz so the sound moves the way the warning timer
// text pulses. Urgency comes from the rise and the tremolo, not from harshness.
const notes = [
  { onset: .00, frequency: 659.25, decay: .13, gain: .80, tremolo: 0 },
  { onset: .13, frequency: 880.00, decay: .42, gain: 1, tremolo: .30 },
];
const phases = notes.map(() => 0);
let seed = 205;
let air = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  let value = 0;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  air += .45 * (noise - air);
  for (let index = 0; index < notes.length; index++) {
    const note = notes[index];
    const local = t - note.onset;
    if (local < 0) continue;
    phases[index] += 2 * Math.PI * note.frequency / rate;
    const phase = phases[index];
    const tone = Math.sin(phase) + .28 * Math.sin(phase * 2) + .09 * Math.sin(phase * 4);
    const attack = 1 - Math.exp(-local / .004);
    const depth = note.tremolo * (1 - Math.exp(-local / .09)) * Math.exp(-local / .5);
    const breath = 1 - depth * (.5 - .5 * Math.cos(2 * Math.PI * 6.5 * local));
    value += note.gain * tone * attack * breath * Math.exp(-local / note.decay);
    // A touch of air on each attack keeps the pips present over the match noise.
    value += .07 * (noise - air) * attack * Math.exp(-local / .012);
  }
  const tailFade = Math.min(1, (frames - 1 - frame) / (rate * .03));
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
  : path.resolve(__dirname, '../../audio-previews/warning-chime.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM; peak -1.5 dBFS, no clipping.`);
