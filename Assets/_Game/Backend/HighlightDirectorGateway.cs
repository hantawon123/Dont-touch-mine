using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using UnityEngine;

namespace Game.Backend
{
    public sealed class HighlightDirectorGateway : IHighlightDirectorGateway
    {
        private readonly BackendClient client;
        public HighlightDirectorGateway(BackendClient client) => this.client=client;
        [Serializable] private sealed class Request { public HighlightDirectorCandidate[] candidates; }
        public async UniTask<HighlightDirectorReply> DirectAsync(HighlightDirectorCandidate[] candidates,CancellationToken cancellation)
        {
            if(!client.Session.SignedIn || candidates == null || candidates.Length == 0)
            {
                Debug.LogWarning("[Highlight] AI skipped: backend account or candidates are unavailable.");
                return null;
            }
            var reply=await client.CallAsync<HighlightDirectorReply>(HttpMethod.Post,"/api/v1/highlights/director",
                new Request { candidates=candidates },BackendAuth.UserId,cancellation);
            if(!reply.Ok)
            {
                Debug.LogWarning($"[Highlight] AI request failed: {reply.Failure}.");
                return null;
            }
            if(reply.Value?.IsUsable(candidates.Length) != true)
            {
                Debug.LogWarning($"[Highlight] AI response rejected: picks={reply.Value?.picks?.Length ?? 0}, candidates={candidates.Length}.");
                return null;
            }
            if(reply.Value.picks.Length > HighlightDirectorReply.MaxPickCount)
                Debug.Log("[Highlight] AI returned the legacy 3-pick response; using the first 2 picks.");
            else Debug.Log($"[Highlight] AI selected {reply.Value.picks.Length} highlight(s).");
            return reply.Value;
        }
    }
}
