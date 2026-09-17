Lives under `Resources` because `ShredderInteractable` sits in each map scene rather than a
shared prefab, and `NetworkInteractionSceneBridge` is a plain C# service (no Inspector) that
confirms shredder state for remote players — both load these clips by path. Playback is 3D
with linear rolloff from 2 m to 15 m, the same range as footsteps, so a far shredder is quieter.

# Feed (ShredderFeed.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-shred.cjs` (grinding bed, four to five crunch bites fading as the item disappears, a closing "gulp" tone).
- Length: 0.42 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/item-shred.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-shred.cjs Assets/_Game/Content/Resources/Audio/Shredder/ShredderFeed.wav`.
- Played once the moment a non-matching (map) item is fed into the shredder, alongside Run.

# Run (ShredderRun.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-shredder-run.cjs` (grinding motor hum, gear chug, a few gear ticks).
- Length: 0.5 seconds — matches `ShredderInteractable.EjectionDelayMilliseconds` exactly.
- Mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/shredder-run.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-shredder-run.cjs Assets/_Game/Content/Resources/Audio/Shredder/ShredderRun.wav`.
- Played once alongside Feed, so it fills the whole wait before ejection.

# Eject (ShredderEject.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-shredder-eject.cjs` (metallic clack, a resonant pressurised-air pop, a short departing chirp).
- Length: 0.18 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/shredder-eject.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-shredder-eject.cjs Assets/_Game/Content/Resources/Audio/Shredder/ShredderEject.wav`.
- Played once when a non-matching item is spat back out. Mechanical on purpose — distinct from ItemThrow.wav, which is a player's hand throw.

# Success (ShredderSuccess.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-success-chime.cjs` (five-note rising bell arpeggio, C5-E5-G5-C6-E6, with a sparkle on the last note).
- Length: 0.46 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -3.1 dBFS.
- Approved preview: `audio-previews/success-chime.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-success-chime.cjs Assets/_Game/Content/Resources/Audio/Shredder/ShredderSuccess.wav`.
- Played once when the player's own hidden item is destroyed in the shredder (a match, not a regular map object).
