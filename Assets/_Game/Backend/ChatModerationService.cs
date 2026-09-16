using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// Filters chat from memory and posts the originals without waiting (S15P21D205-1028).
    /// </summary>
    /// <remarks>
    /// Runs on the dedicated server only, because that is where every message already passes
    /// through on its way to the other players.
    /// <para>
    /// <b>Nothing here blocks a message.</b> The judgement comes from a list fetched once at
    /// startup, and the record is queued and sent later. That is the whole reason the list is
    /// fetched rather than the backend being asked per message: chat must not slow down or stop
    /// when the backend does.
    /// </para>
    /// <para>
    /// There is no outbox on disk. A batch that cannot be sent is dropped after its retries,
    /// because a file would go down with the container the way the match analytics outbox did
    /// (S15P21D205-1021) while pretending to be safe. Sending every couple of seconds keeps what
    /// is at risk down to a couple of seconds.
    /// </para>
    /// </remarks>
    public sealed class ChatModerationService : IChatModeration, IDisposable
    {
        /// <summary>Matches the backend's per-request cap.</summary>
        private const int MaxBatch = 200;

        /// <summary>
        /// Short enough that a crash loses only a few lines, long enough that a busy room is
        /// still one request rather than one per message.
        /// </summary>
        private const int FlushIntervalMs = 2500;

        /// <summary>
        /// A queue this long already means the backend is gone. Holding more would trade a
        /// server's memory for records nobody is going to read.
        /// </summary>
        private const int MaxQueued = 2000;

        private readonly IHttpTransport transport;
        private readonly BackendEndpoint endpoint;
        private readonly string key;
        private readonly ChatBlocklist blocklist = new();
        private readonly Queue<ChatLogRecord> pending = new();
        private readonly CancellationTokenSource lifetime = new();

        private int dropped;
        private int reportedDrops;
        private bool flushing;

        public ChatModerationService(IHttpTransport transport, BackendEndpoint endpoint, string key)
        {
            this.transport = transport;
            this.endpoint = endpoint;
            this.key = key;
        }

        public bool IsFiltering => blocklist.IsLoaded;

        /// <summary>
        /// Fetches the word list. Call once before the room opens.
        /// </summary>
        /// <remarks>
        /// <b>A failure is not fatal.</b> The room opens without filtering and says so loudly.
        /// Refusing to start would stop the game outright over a list, and the reports still
        /// cover what gets said.
        /// <para>
        /// Nothing needs to refresh it. A room server quits two minutes after its room empties
        /// and the release host starts another, so a list changed on the backend reaches every
        /// room opened after that by itself.
        /// </para>
        /// </remarks>
        public async UniTask LoadAsync(CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[Chat] No internal key was given; chat runs unfiltered and unrecorded.");
                return;
            }

            try
            {
                var answer = await transport.SendAsync(
                    new HttpCall(HttpMethod.Get, endpoint.Url("/internal/chat/blocklist"), null,
                        new[] { new HttpHeader("X-Internal-Key", key) }, endpoint.TimeoutSeconds),
                    cancellation);

                if (answer.Outcome != HttpOutcome.Completed || answer.StatusCode != 200)
                {
                    // 404 is what a wrong or missing key looks like: the path denies it exists.
                    Debug.LogWarning($"[Chat] Word list refused ({answer.StatusCode}); chat runs unfiltered.");
                    return;
                }

                var parsed = JsonUtility.FromJson<BlocklistDto>(answer.Body);
                blocklist.Load(parsed?.blocked, parsed?.allowed);
                Debug.Log($"[Chat] Word list loaded: {parsed?.blocked?.Length ?? 0} entries.");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chat] Word list unavailable ({e.Message}); chat runs unfiltered.");
            }
        }

        public string Mask(string message) => blocklist.Mask(message);

        public void Record(ChatLogRecord record)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            if (pending.Count >= MaxQueued)
            {
                dropped++;
                return;
            }

            pending.Enqueue(record);
            if (!flushing) FlushLoopAsync().Forget(e => Debug.LogWarning($"[Chat] Upload stopped: {e.Message}"));
        }

        private async UniTask FlushLoopAsync()
        {
            flushing = true;
            try
            {
                while (pending.Count > 0)
                {
                    await UniTask.Delay(FlushIntervalMs, DelayType.Realtime, cancellationToken: lifetime.Token);
                    await SendOnceAsync();
                    ReportDrops();
                }
            }
            catch (OperationCanceledException) { }
            finally { flushing = false; }
        }

        private async UniTask SendOnceAsync()
        {
            var take = Math.Min(pending.Count, MaxBatch);
            var batch = new BatchDto { messages = new EntryDto[take] };
            for (var index = 0; index < take; index++) batch.messages[index] = Entry(pending.Dequeue());

            var answer = await transport.SendAsync(
                new HttpCall(HttpMethod.Post, endpoint.Url("/internal/chat"), JsonUtility.ToJson(batch),
                    new[] { new HttpHeader("X-Internal-Key", key) }, endpoint.TimeoutSeconds),
                lifetime.Token);

            // Dropped rather than requeued. Requeuing a batch the backend rejected (400) would
            // send it forever, and a backend that is down will refuse the next one too.
            if (answer.Outcome != HttpOutcome.Completed || answer.StatusCode != 202) dropped += take;
        }

        /// <summary>
        /// Says how much was lost. Fire-and-forget fails in silence otherwise, and a server that
        /// has recorded nothing for an hour looks exactly like a quiet one.
        /// </summary>
        private void ReportDrops()
        {
            if (dropped == reportedDrops) return;
            Debug.LogWarning($"[Chat] {dropped} chat lines were not recorded.");
            reportedDrops = dropped;
        }

        private static EntryDto Entry(ChatLogRecord record) => new()
        {
            roomCode = record.RoomCode,
            scope = record.Scope == ChatScope.Lobby ? "LOBBY" : "MATCH",
            userPublicId = record.UserPublicId,
            senderRef = record.SenderRef,
            message = record.Message,
            sentAt = record.SaidAt.UtcDateTime.ToString("yyyyMMddHHmmss")
        };

        public void Dispose()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        // Public fields and no validation because JsonUtility writes fields, like BackendDtos.
        [Serializable]
        private sealed class BlocklistDto
        {
            public string[] blocked;
            public string[] allowed;
        }

        [Serializable]
        private sealed class BatchDto
        {
            public EntryDto[] messages;
        }

        [Serializable]
        private sealed class EntryDto
        {
            public string roomCode;
            public string scope;
            public string userPublicId;
            public string senderRef;
            public string message;
            public string sentAt;
        }
    }
}
