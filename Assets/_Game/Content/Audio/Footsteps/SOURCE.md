# Walk footsteps (Footstep_01.wav … Footstep_08.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-footsteps.cjs` (eight seeded variants: heel thud, short grit and a rounded low tone).
- Length: 0.18 seconds each; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/footstep.wav` (variant 01); runtime assets are the eight seeded renders.
- Regenerate: `node Tools/audio/create-footsteps.cjs Assets/_Game/Content/Audio/Footsteps`.
- Played on each authored foot plant while walking, running or crouch-walking. Walk plants are the hip-squash instants at 1/12 and 7/12 of the cycle (Walk_Forward frame 2 and 14 of 24). Run and crouch-walk use their own clip timings. Replaces the previously split Freesound recording; the active footsteps contain no samples from that recording.
