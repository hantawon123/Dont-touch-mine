# Item pickup (ItemPickup.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-cute-pickup.cjs` (quick rising chirp with a bright second harmonic and a brief finger-contact puff).
- Length: 0.16 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/cute-item-pickup.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-cute-pickup.cjs Assets/_Game/Content/Audio/Items/ItemPickup.wav`.
- Played once when a carryable item is picked up (local and confirmed-network pickups).

# Item place (ItemPlace.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-soft-place.cjs` (warm descending pitch, soft filtered-noise contact and two small settle bounces).
- Length: 0.24 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/soft-item-place.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-soft-place.cjs Assets/_Game/Content/Audio/Items/ItemPlace.wav`.
- Played once on both a plain drop and a confirmed precise placement — the same sound covers both.

# Item throw (ItemThrow.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-cute-throw.cjs` (a release tick followed by a tonal whistling glide — quick rise, longer fade-out — "휘융").
- Length: 0.32 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/cute-item-throw.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-cute-throw.cjs Assets/_Game/Content/Audio/Items/ItemThrow.wav`.
- Played once when a carried item is thrown (local throw only; see PlayerAnimationDriver.PlayThrow).
