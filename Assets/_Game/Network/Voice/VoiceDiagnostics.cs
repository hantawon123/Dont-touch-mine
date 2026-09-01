using System.Collections.Generic;
using System.Text;
using Photon.Realtime;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

namespace Game.Network.Voice
{
    /// <summary>
    /// Prints the state of the whole voice chain once every few seconds, so that
    /// a call that fails intermittently leaves a record of the moment it failed.
    /// </summary>
    /// <remarks>
    /// Always prints while a session is up, including when it finds nothing.
    /// An earlier probe only spoke when it had a playing speaker to report, and
    /// the run where nothing played produced no lines at all — indistinguishable
    /// from the probe never having started. Silence has to mean something, so
    /// here it means the session is over.
    /// <para>
    /// The line is built to separate the links of the chain: whether the room
    /// was joined, whether this machine is sending, whether frames arrive,
    /// whether a speaker took them, and how far away the speaker is. Distance is
    /// in there because the audio is positional and rolls off to exact silence
    /// past its maximum, which sounds identical to a stream that never arrived.
    /// </para>
    /// </remarks>
    public sealed class VoiceDiagnostics : MonoBehaviour
    {
        private const float ReportSeconds = 5f;

        /// <remarks>
        /// Speakers are looked up on an interval and read every frame. The
        /// lookup scans the scene, which is too heavy to run per frame and too
        /// slow to matter for something that only changes when a player spawns.
        /// </remarks>
        private const float RescanSeconds = 0.5f;

        private readonly List<VoiceNetworkObject> voices = new();
        private readonly StringBuilder line = new();

        private FusionVoiceClient client;
        private AudioListener listener;

        /// <summary>Frames sent as of the last report, to turn a total into a rate.</summary>
        private int lastFramesSent;

        private float lastReport;
        private float nextRescan;
        private float nextReport;
        private bool announced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var host = new GameObject(nameof(VoiceDiagnostics));
            DontDestroyOnLoad(host);
            host.AddComponent<VoiceDiagnostics>();
#endif
        }

        private void Awake()
        {
            nextReport = Time.unscaledTime + ReportSeconds;
            lastReport = Time.unscaledTime;
        }

        private void Update()
        {
            var now = Time.unscaledTime;

            if (now >= nextRescan)
            {
                nextRescan = now + RescanSeconds;
                Rescan();
            }

            if (now < nextReport)
            {
                return;
            }

            var elapsed = now - lastReport;
            nextReport = now + ReportSeconds;
            lastReport = now;
            Report(elapsed);
        }

        private void Rescan()
        {
            if (client == null)
            {
                client = FindFirstObjectByType<FusionVoiceClient>();
                lastFramesSent = 0;
            }

            if (listener == null)
            {
                listener = FindFirstObjectByType<AudioListener>();
            }

            voices.Clear();
            foreach (var candidate in
                     FindObjectsByType<VoiceNetworkObject>(FindObjectsSortMode.None))
            {
                if (candidate.Object == null)
                {
                    continue;
                }

                voices.Add(candidate);
            }
        }

        private void Report(float elapsed)
        {
            if (client == null)
            {
                // Said once, so that the quiet that follows reads as "no session"
                // rather than as a tool that stopped working.
                if (announced)
                {
                    announced = false;
                    Debug.Log("[Voice] session ended");
                }

                return;
            }

            announced = true;

            var mine = 0;
            var remote = 0;
            foreach (var voice in voices)
            {
                if (voice.IsLocal)
                {
                    mine++;
                }
                else
                {
                    remote++;
                }
            }

            Debug.Log(BuildSummary(elapsed, mine, remote));

            foreach (var voice in voices)
            {
                if (voice.IsLocal)
                {
                    continue;
                }

                Debug.Log(BuildSpeakerLine(voice));
            }
        }

        private string BuildSummary(float elapsed, int mine, int remote)
        {
            var voiceClient = client.VoiceClient;
            var sent = voiceClient == null ? 0 : voiceClient.FramesSent;
            var sentRate = elapsed > 0f ? (sent - lastFramesSent) / elapsed : 0f;
            lastFramesSent = sent;

            var talking = false;
            foreach (var voice in voices)
            {
                if (voice.IsLocal && voice.RecorderInUse != null &&
                    voice.RecorderInUse.IsCurrentlyTransmitting)
                {
                    talking = true;
                    break;
                }
            }

            line.Clear();
            line.Append("[Voice] state=").Append(client.ClientState);
            line.Append(" tx=").Append(talking ? 'Y' : 'N');
            line.Append(" sent=").Append(sentRate.ToString("F0")).Append("/s");
            line.Append(" recv=").Append(client.FramesReceivedPerSecond.ToString("F0")).Append("/s");
            line.Append(" lost=").Append(client.FramesLostPercent.ToString("F1")).Append('%');

            if (voiceClient != null)
            {
                line.Append(" rtt=").Append(voiceClient.RoundTripTime);
                line.Append('±').Append(voiceClient.RoundTripTimeVariance).Append("ms");
            }

            line.Append(" | avatars mine=").Append(mine).Append(" remote=").Append(remote);
            return line.ToString();
        }

        /// <remarks>
        /// Distance is measured to the listener rather than to the local avatar,
        /// because the listener is what Unity actually attenuates against, and
        /// during a highlight replay the two are not in the same place.
        /// </remarks>
        private string BuildSpeakerLine(VoiceNetworkObject voice)
        {
            line.Clear();
            line.Append("  spk#").Append(voice.Object.Id);

            var speaker = voice.SpeakerInUse;
            if (speaker == null)
            {
                return line.Append(" MISSING - no Speaker on this avatar").ToString();
            }

            line.Append(" linked=").Append(speaker.IsLinked ? 'Y' : 'N');
            line.Append(" playing=").Append(speaker.IsPlaying ? 'Y' : 'N');
            line.Append(" lag=").Append(speaker.Lag).Append("ms");

            var source = speaker.GetComponent<AudioSource>();
            if (source == null)
            {
                return line.Append(" NO AudioSource").ToString();
            }

            if (listener != null)
            {
                var distance = Vector3.Distance(
                    listener.transform.position, source.transform.position);
                line.Append(" dist=").Append(distance.ToString("F1"));
                line.Append('/').Append(source.maxDistance.ToString("F0")).Append('m');

                if (distance > source.maxDistance)
                {
                    line.Append(" OUT OF RANGE");
                }
            }

            return line.ToString();
        }
    }
}
