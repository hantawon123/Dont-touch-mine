using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>Completed match outbox. Retry the same event IDs; the DB deduplicates them.</summary>
    public sealed class MatchAnalyticsUpload : IDisposable
    {
        private readonly IHttpTransport transport;
        private readonly BackendEndpoint endpoint;
        private readonly string directory;
        private readonly CancellationTokenSource lifetime = new();
        private bool sending;
        public bool IsSending => sending;

        public MatchAnalyticsUpload(IHttpTransport transport, BackendEndpoint endpoint, string directory)
        {
            this.transport = transport;
            this.endpoint = endpoint;
            this.directory = directory;
        }

        public void Store(MatchAnalyticsBuffer buffer)
        {
            try
            {
                Directory.CreateDirectory(directory);
                // Bound disk use. Never overwrite a previous unsent match.
                if (Directory.GetFiles(directory).Length >= 10)
                {
                    Debug.LogWarning("[Analytics] Outbox full; match was not stored.");
                    return;
                }
                File.WriteAllLines(Path.Combine(directory, buffer.MatchId + ".jsonl"), buffer.Snapshot());
                Retry();
            }
            catch (Exception e) { Debug.LogWarning($"[Analytics] Cannot store match: {e.Message}"); }
        }

        public void Retry()
        {
            if (!sending) SendPendingAsync().Forget(e => Debug.LogWarning($"[Analytics] Upload stopped: {e.Message}"));
        }

        private async UniTask SendPendingAsync()
        {
            sending = true;
            try
            {
                if (!Directory.Exists(directory)) return;
                while (true)
                {
                    var path = Directory.GetFiles(directory, "*.jsonl").OrderBy(File.GetLastWriteTimeUtc).FirstOrDefault();
                    if (path == null) return;
                    var lines = File.ReadAllLines(path);
                    var rejected = false;
                    for (var offset = 0; offset < lines.Length; offset += 50)
                    {
                        var body = "[" + string.Join(",", lines.Skip(offset).Take(50)) + "]";
                        var accepted = false;
                        for (var attempt = 0; attempt < 3; attempt++)
                        {
                            var answer = await transport.SendAsync(new HttpCall(HttpMethod.Post,
                                endpoint.Url("/api/v1/events"), body, null, endpoint.TimeoutSeconds), lifetime.Token);
                            if (answer.Outcome == HttpOutcome.Completed && answer.StatusCode == 202)
                            {
                                accepted = true;
                                break;
                            }
                            if (answer.Outcome == HttpOutcome.Cancelled) return;
                            if (answer.StatusCode == 400 || answer.StatusCode == 413)
                            {
                                Debug.LogWarning($"[Analytics] Batch rejected ({answer.StatusCode}); quarantined locally.");
                                File.Move(path, path + ".rejected");
                                rejected = true;
                                break;
                            }
                            await UniTask.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), ignoreTimeScale: true, cancellationToken: lifetime.Token);
                        }
                        if (rejected) break;
                        if (!accepted) return;
                        await UniTask.Delay(200, ignoreTimeScale: true, cancellationToken: lifetime.Token);
                    }
                    if (rejected) continue;
                    File.Delete(path);
                    Debug.Log($"[Analytics] Match accepted by collector: {lines.Length} events.");
                }
            }
            catch (OperationCanceledException) { }
            finally { sending = false; }
        }

        public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
    }
}
