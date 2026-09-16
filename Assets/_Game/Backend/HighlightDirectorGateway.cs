using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;

namespace Game.Backend
{
    public sealed class HighlightDirectorGateway : IHighlightDirectorGateway
    {
        private readonly BackendClient client;
        public HighlightDirectorGateway(BackendClient client) => this.client=client;
        [Serializable] private sealed class Request { public HighlightDirectorCandidate[] candidates; }
        public async UniTask<HighlightDirectorReply> DirectAsync(HighlightDirectorCandidate[] candidates,CancellationToken cancellation)
        {
            if(!client.Session.SignedIn || candidates == null || candidates.Length == 0) return null;
            var reply=await client.CallAsync<HighlightDirectorReply>(HttpMethod.Post,"/api/v1/highlights/director",
                new Request { candidates=candidates },BackendAuth.UserId,cancellation);
            return reply.Ok && reply.Value?.IsUsable(candidates.Length) == true ? reply.Value : null;
        }
    }
}
