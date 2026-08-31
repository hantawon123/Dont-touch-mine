using System.Collections.Generic;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Network.Voice
{
    /// <summary>
    /// Temporary. Reports how much of the jitter buffer is actually spent, and
    /// shortens it mid-call so the point where it breaks can be heard.
    /// </summary>
    /// <remarks>
    /// Delete this file once the depth is settled. It installs itself and is
    /// referenced from nowhere, so deleting it is the entire removal.
    /// <para>
    /// The number that decides the depth is the lowest lag seen, not the mean.
    /// Lag is what has arrived and is not yet played, so its floor is the
    /// cushion that was never needed — delay every player pays for and no
    /// packet ever spends. A floor that hugs zero means the opposite, that the
    /// buffer is draining faster than it fills and the next late frame is a
    /// dropout.
    /// </para>
    /// <para>
    /// Sampled every frame rather than on a timer. Frames arrive every 20 ms
    /// and the dips that matter are one frame wide, so a reading once a second
    /// reports the plateau and misses the whole event.
    /// </para>
    /// </remarks>
    public sealed class VoiceLagProbe : MonoBehaviour
    {
        /// <summary>Buffer depths F9 walks through, in ms.</summary>
        private static readonly int[] Steps = { 80, 70, 60, 50, 40 };

        private const float WindowSeconds = 5f;

        /// <remarks>
        /// Speakers are looked up on an interval and read every frame. The
        /// lookup scans the scene, which is too heavy to run per frame and too
        /// slow to matter for something that only changes when a player spawns.
        /// </remarks>
        private const float RescanSeconds = 0.5f;

        private sealed class Stats
        {
            public int Min = int.MaxValue;
            public int Max;
            public long Sum;
            public int Count;
            public int Under40;
            public int Under20;

            /// <summary>The lowest lag since the current depth was chosen.</summary>
            public int Floor = int.MaxValue;
        }

        private readonly Dictionary<Speaker, Stats> stats = new();
        private readonly List<Speaker> speakers = new();
        private readonly List<Speaker> departed = new();

        private int step;
        private float nextRescan;
        private float nextReport;

        /// <remarks>
        /// Installed from code so that nothing else has to know it exists. Kept
        /// out of release builds because it is a measuring tool that reads keys
        /// and writes to the log.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var host = new GameObject(nameof(VoiceLagProbe));
            DontDestroyOnLoad(host);
            host.AddComponent<VoiceLagProbe>();
#endif
        }

        private void Awake()
        {
            nextReport = Time.unscaledTime + WindowSeconds;
        }

        private void Update()
        {
            var now = Time.unscaledTime;

            if (now >= nextRescan)
            {
                nextRescan = now + RescanSeconds;
                Rescan();
            }

            ReadStepKeys();
            Sample();

            if (now >= nextReport)
            {
                nextReport = now + WindowSeconds;
                Report();
            }
        }

        /// <summary>
        /// Collects the speakers carrying the other players' voices.
        /// </summary>
        /// <remarks>
        /// The local avatar is skipped because it has nothing to play: a player
        /// does not receive their own stream, so its speaker never fills and its
        /// lag would read as a permanent zero.
        /// </remarks>
        private void Rescan()
        {
            speakers.Clear();
            foreach (var candidate in
                     FindObjectsByType<VoiceNetworkObject>(FindObjectsSortMode.None))
            {
                if (candidate.Object == null || candidate.IsLocal ||
                    candidate.SpeakerInUse == null)
                {
                    continue;
                }

                speakers.Add(candidate.SpeakerInUse);
            }

            // Avatars spawn at the depth baked into the prefab, so a player who
            // arrives mid-test has to be told what the rest are running.
            ApplyStep();
        }

        private void ReadStepKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                step = (step + 1) % Steps.Length;
            }
            else if (keyboard.f10Key.wasPressedThisFrame)
            {
                step = 0;
            }
            else
            {
                return;
            }

            ApplyStep();

            // Readings taken at the old depth say nothing about the new one.
            stats.Clear();
            Debug.Log($"[VoiceLag] --- buffer -> {Steps[step]}ms ---");
        }

        /// <remarks>
        /// Changing the depth restarts playback, so the stream audibly catches
        /// once on every step. That break belongs to the tool, not the network.
        /// </remarks>
        private void ApplyStep()
        {
            var target = Steps[step];
            foreach (var speaker in speakers)
            {
                if (speaker == null)
                {
                    continue;
                }

                var config = speaker.PlayDelayConfig;
                if (config.Low == target && config.High == target)
                {
                    continue;
                }

                config.Low = target;
                config.High = target;
                speaker.PlayDelayConfig = config;
            }
        }

        /// <remarks>
        /// Only while a speaker is playing. A buffer nobody is filling drains to
        /// nothing, and counting that as a low reading would report every
        /// silence as a near dropout.
        /// </remarks>
        private void Sample()
        {
            foreach (var speaker in speakers)
            {
                if (speaker == null || !speaker.IsPlaying)
                {
                    continue;
                }

                if (!stats.TryGetValue(speaker, out var entry))
                {
                    entry = new Stats();
                    stats[speaker] = entry;
                }

                var lag = speaker.Lag;
                entry.Sum += lag;
                entry.Count++;

                if (lag < entry.Min)
                {
                    entry.Min = lag;
                }

                if (lag > entry.Max)
                {
                    entry.Max = lag;
                }

                if (lag < entry.Floor)
                {
                    entry.Floor = lag;
                }

                if (lag < 40)
                {
                    entry.Under40++;
                }

                if (lag < 20)
                {
                    entry.Under20++;
                }
            }
        }

        private void Report()
        {
            departed.Clear();
            foreach (var pair in stats)
            {
                if (pair.Key == null)
                {
                    departed.Add(pair.Key);
                    continue;
                }

                var entry = pair.Value;
                if (entry.Count == 0)
                {
                    // Silent through the whole window. Kept rather than dropped
                    // so the floor survives the pauses in a conversation.
                    continue;
                }

                Debug.Log(
                    $"[VoiceLag] spk={pair.Key.GetInstanceID()} target={Steps[step]}ms " +
                    $"min={entry.Min} max={entry.Max} avg={entry.Sum / entry.Count} " +
                    $"n={entry.Count} under40={entry.Under40} under20={entry.Under20} " +
                    $"floor={entry.Floor}");

                entry.Min = int.MaxValue;
                entry.Max = 0;
                entry.Sum = 0;
                entry.Count = 0;
                entry.Under40 = 0;
                entry.Under20 = 0;
            }

            foreach (var speaker in departed)
            {
                stats.Remove(speaker);
            }
        }
    }
}
