# Warning chime (WarningChime.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-warning-chime.cjs` (two rising bell pips, E5 then A5, the held note breathing at 6.5 Hz).
- Length: 0.95 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -1.5 dBFS, in line with the footstep and combat one-shots.
- Regenerate: `node Tools/audio/create-warning-chime.cjs Assets/_Game/Content/Resources/Audio/WarningChime.wav`.
- Played once when searching enters its last thirty seconds; uses the Effects volume.

# Tension bed (TensionLoop.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-tension-loop.cjs` (A minor drone with a 0.25 Hz beat, a 120 BPM heartbeat and eighth-note clock ticks).
- Length: 4.000 seconds, exactly 8 beats at 120 BPM; every sustained partial completes a whole number of cycles, so the file loops without a seam.
- Mono 44.1 kHz, 16-bit PCM; peak at most -6.0 dBFS. Levels account for the default mixer: a category slider at 50% and master at 50% together take 12 dB off, so a file mastered much quieter than this is inaudible in play. The bed lands about 16 dB under a footstep peak — felt, not listened to.
- Regenerate: `node Tools/audio/create-tension-loop.cjs Assets/_Game/Content/Resources/Audio/TensionLoop.wav`.
- Loops under the last thirty seconds of searching and fades out over 0.6 s; uses the Music volume, so muting music silences the bed without taking the chime with it.

# Hiding timer tick (HidingTimerTick.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-timer-tick.cjs` (analog-clock second: wooden case thock at 920 Hz plus a 3550 Hz escapement click).
- Length: 0.12 seconds; mono 44.1 kHz, 16-bit PCM; peak -3.1 dBFS, no clipping.
- Regenerate: `node Tools/audio/create-timer-tick.cjs Assets/_Game/Content/Resources/Audio/HidingTimerTick.wav`.
- Played once per second during the last `HidingActiveHudView.WarningSeconds` (10) seconds of the hiding phase countdown; uses the Effects volume.

# Searching timer tick (SearchingTimerTick.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-timer-tick.cjs` (same generator and settings as `HidingTimerTick.wav`; a separate render so the two phases can diverge later without one file backing both).
- Length: 0.12 seconds; mono 44.1 kHz, 16-bit PCM; peak -3.1 dBFS, no clipping.
- Regenerate: `node Tools/audio/create-timer-tick.cjs Assets/_Game/Content/Resources/Audio/SearchingTimerTick.wav`.
- Played once per second during the last `MatchTimerView.WarningSeconds` (30) seconds of the searching phase, the same window `MatchUrgencyAudio` uses for the border, chime and tension bed; uses the Effects volume.

# Match end bell (MatchEndBell.wav)

- Original procedural synthesis created for this project; no third-party recordings, samples, or AI audio generation service used.
- Generator: `Tools/audio/create-match-end-bell.cjs` (four even metallic bell strokes at 932 Hz with inharmonic partials — boxing/school-bell "땡땡땡땡").
- Length: 1.42 seconds; mono 44.1 kHz, 16-bit PCM; peak at most -1.5 dBFS.
- Approved preview: `audio-previews/match-end-bell.wav`; runtime asset is byte-identical.
- Regenerate: `node Tools/audio/create-match-end-bell.cjs Assets/_Game/Content/Resources/Audio/MatchEndBell.wav`.
- Played once when a match result is confirmed, including last-player-standing; uses the Effects volume. A republished result does not ring again until the next hiding or waiting phase.

All five clips live under `Resources` because the match HUD is built in the scene rather than from a prefab, so `MatchUrgencyAudio`, `HidingTimerTickAudio`, `SearchingTimerTickAudio` and `MatchEndBellAudio` load them by path.
