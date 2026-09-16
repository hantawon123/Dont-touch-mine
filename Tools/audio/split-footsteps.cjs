// Convert the selected IENBA recording to eight mono one-shot assets.
// Usage: node Tools/audio/split-footsteps.cjs <original WAV>
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const input = fs.readFileSync(process.argv[2]);
assert.equal(input.toString('ascii', 0, 4), 'RIFF');
assert.equal(input.toString('ascii', 8, 12), 'WAVE');
let format, data;
for (let offset = 12; offset + 8 <= input.length;) {
  const size = input.readUInt32LE(offset + 4);
  const id = input.toString('ascii', offset, offset + 4);
  if (id === 'fmt ') format = input.subarray(offset + 8, offset + 8 + size);
  if (id === 'data') data = input.subarray(offset + 8, offset + 8 + size);
  offset += 8 + size + (size % 2);
}
assert.equal(format.readUInt16LE(0), 1);
assert.equal(format.readUInt16LE(2), 2);
assert.equal(format.readUInt32LE(4), 44100);
assert.equal(format.readUInt16LE(14), 16);
assert.ok(data.length >= 3.6 * 44100 * 4);
const output = path.resolve(__dirname, '../../Assets/_Game/Content/Audio/Footsteps');
fs.mkdirSync(output, { recursive: true });
const starts = [0, .43, .92, 1.38, 1.86, 2.31, 2.76, 3.25];
starts.forEach((start, index) => {
  const frames = Math.round(.28 * 44100);
  const wav = Buffer.alloc(44 + frames * 2);
  wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(44100, 24); wav.writeUInt32LE(88200, 28);
  wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(frames * 2, 40);
  let energy = 0;
  for (let frame = 0; frame < frames; frame++) {
    const offset = (Math.round(start * 44100) + frame) * 4;
    const sample = (data.readInt16LE(offset) + data.readInt16LE(offset + 2)) / 2;
    // Preserve recording levels and soften cut boundaries (1 ms in, 10 ms out).
    const fade = Math.min(1, frame / 44, (frames - 1 - frame) / 441);
    const value = Math.round(sample * fade);
    wav.writeInt16LE(value, 44 + frame * 2); energy += value * value;
  }
  assert.ok(energy > 0);
  fs.writeFileSync(path.join(output, `Footstep_${String(index + 1).padStart(2, '0')}.wav`), wav);
});
console.log('Generated 8 mono PCM footsteps (0.28 seconds each).');
