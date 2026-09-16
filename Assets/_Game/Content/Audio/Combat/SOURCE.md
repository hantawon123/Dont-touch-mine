# Punch swing (PunchSwing.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-cute-swing.cjs` (deterministic filtered noise, resonant sweep and a small sine tone).
- Length: 0.18 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/cute-punch-swing.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-cute-swing.cjs Assets/_Game/Content/Audio/Combat/PunchSwing.wav`.
- Used for punch swings, including missed punches; not hit confirmation.
- Replaces the previously selected Freesound recording; the active swing contains no samples from that recording.

# Punch hit (SoftPunchHit.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-soft-punch.cjs` (deterministic pitch-drop sine tone, harmonic and filtered noise).
- Length: 0.22 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Regenerate: `node Tools/audio/create-soft-punch.cjs Assets/_Game/Content/Audio/Combat/SoftPunchHit.wav`.
- Used only on confirmed player hit notifications, including the hit that causes stun.
