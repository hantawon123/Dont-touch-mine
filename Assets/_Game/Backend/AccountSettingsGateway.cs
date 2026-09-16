using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;
using Game.Core.Settings;

namespace Game.Backend
{
    public sealed class AccountSettingsGateway : IAccountSettingsGateway
    {
        private const string Path = "/api/v1/accounts/me/settings";
        private readonly BackendClient client;
        public AccountSettingsGateway(BackendClient client) => this.client = client;
        public string UserId => client.Session.UserId;
        public UniTask<BackendResult<AccountSettingsReply>> GetAsync(CancellationToken cancellation) =>
            client.CallAsync<AccountSettingsReply>(HttpMethod.Get, Path, null, BackendAuth.UserId, cancellation);
        public UniTask<BackendResult<AccountSettingsReply>> SaveAsync(long revision, AccountSettingsSnapshot settings,
            CancellationToken cancellation) => client.CallAsync<AccountSettingsReply>(HttpMethod.Put, Path,
                new SaveRequest { expectedRevision = revision, settings = settings }, BackendAuth.UserId, cancellation);
        [Serializable]
        private sealed class SaveRequest { public long expectedRevision; public AccountSettingsSnapshot settings; }
    }
}
