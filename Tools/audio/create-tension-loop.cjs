// Original procedural tension bed for the final 30 seconds; no external samples
// or audio models. Seamless: 4.000 s is exactly 8 beats at 120 BPM, and every
// sustained partial completes a whole number of cycles inside that window.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const rate = 44100;
const seconds = 4;
const frames = Math.round(rate * seconds);
const samples = new Float64Array(frames);
const beat = .5;
const tick = .25;
// A minor drone. 110.25 Hz beats against 110 Hz once per loop, so the bed keeps
// drifting without ever landing on a seam.
const drone = [
  { frequency: 110, gain: .50 },
  { frequency: 110.25, gain: .45 },
  { frequency: 164.75, gain: .28 },
  { frequency: 220, gain: .12 },
  { frequency: 261.75, gain: .07 },
];
const dronePhases = drone.map(() => 0);
let pulsePhase = 0;
let previousBeat = -1;
let seed = 205;
let air = 0;
for (let frame = 0; frame < frames; frame++) {
  const t = frame / rate;
  let value = 0;
  // Low sustained unease, swelling once across the loop.
  const swell = .85 + .15 * Math.sin(2 * Math.PI * t / seconds);
  for (let index = 0; index < drone.length; index++) {
    dronePhases[index] += 2 * Math.PI * drone[index].frequency / rate;
    value += drone[index].gain * swell * Math.sin(dronePhases[index]);
  }
  // A heartbeat on every beat: felt more than heard.
  const sincePulse = t % beat;
  const beatIndex = Math.floor(t / beat);
  if (beatIndex !== previousBeat) {
    previousBeat = beatIndex;
    pulsePhase = 0;
  }
  pulsePhase += 2 * Math.PI * (58 + 72 * Math.exp(-sincePulse / .03)) / rate;
  const pulse = (1 - Math.exp(-sincePulse / .002)) * Math.exp(-sincePulse / .09);
  value += .85 * pulse * Math.sin(pulsePhase);
  // A dry clock tick on every eighth, accented on the beat.
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  air += .6 * (noise - air);
  const sinceTick = t % tick;
  const onBeat = sincePulse < sinceTick + 1e-9;
  const tickEnv = (1 - Math.exp(-sinceTick / .0008)) * Math.exp(-sinceTick / .012);
  value += (onBeat ? .3 : .17) * (noise - air) * tickEnv;
  samples[frame] = value;
}
const peak = samples.reduce((largest, sample) => Math.max(largest, Math.abs(sample)), 0);
const wav = Buffer.alloc(44 + frames * 2);
wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 2, 28);
wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
wav.write('data', 36); wav.writeUInt32LE(frames * 2, 40);
for (let frame = 0; frame < frames; frame++) {
  const value = Math.round(samples[frame] / peak * .25 * 32767);
  assert.ok(Number.isFinite(value) && Math.abs(value) < 32767);
  wav.writeInt16LE(value, 44 + frame * 2);
}
const output = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.resolve(__dirname, '../../audio-previews/tension-loop.wav');
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, wav);
console.log(`Created ${output}: ${seconds}s mono PCM loop; peak -12.0 dBFS, no clipping.`);
