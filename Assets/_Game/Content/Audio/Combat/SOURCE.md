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

# Jump (Jump.wav)

- Original procedural synthesis; no third-party recordings, samples or AI audio generation service used.
- Generator: `Tools/audio/create-cute-jump.cjs` (rising sine tone, harmonic, pitch wobble and filtered noise).
- Length: 0.28 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/cute-jump.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-cute-jump.cjs Assets/_Game/Content/Audio/Combat/Jump.wav`.
- Played once on upward airborne movement after grounding; not on falling, initial airborne spawning, or repeated airborne inputs.

# Land (Land.wav)

- Original procedural synthesis; no third-party recordings, samples or AI audio generation service used.
- Generator: `Tools/audio/create-cute-land.cjs` (descending sine tone, harmonic, filtered-noise contact and a small settle bounce).
- Length: 0.22 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/cute-land.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-cute-land.cjs Assets/_Game/Content/Audio/Combat/Land.wav`.
- Played once when landing after a jump that already played the takeoff cue. Walking off a ledge, spawning in mid-air, crouch/prone landings, and stunned landings do not play it.
- Playback is delayed `LandImpactSeconds` (4 frames at 30 fps) after physics grounded, matching the Land clip's hip squash / foot plant. Volume is 80% of the other combat one-shots.

# Stun (StunTwang.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-stun-twang.cjs` (five descending springy notes with a pitch slide and a short wobble — cartoon dizzy "띠용용용용").
- Length: 0.92 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/stun-twang.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-stun-twang.cjs Assets/_Game/Content/Audio/Combat/StunTwang.wav`.
- Played once when a player actually enters stun. Initial stunned snapshots, repeated stun flags, and hits that do not stun do not play it. The stunning blow still also plays the punch hit cue.

# Posture swoosh (PostureSwoosh.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-posture-swoosh.cjs` (band-limited air and a short downward rustle — cloth "스윽").
- Length: 0.24 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/posture-swoosh.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-posture-swoosh.cjs Assets/_Game/Content/Audio/Combat/PostureSwoosh.wav`.
- Played once on crouch/stand and prone/stand (and crouch↔prone) transitions that already have a posture animation. Spawning in a pose, stunned pose tracking, and airborne collider bounce do not play it.
