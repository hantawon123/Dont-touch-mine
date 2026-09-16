using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Settings;

namespace Game.Core.Ports
{
    [Serializable]
    public sealed class AccountSettingsReply
    {
        public bool accepted;
        public long revision;
        public AccountSettingsSnapshot settings;
    }
    public interface IAccountSettingsGateway
    {
        string UserId { get; }
        UniTask<BackendResult<AccountSettingsReply>> GetAsync(CancellationToken cancellation);
        UniTask<BackendResult<AccountSettingsReply>> SaveAsync(long revision, AccountSettingsSnapshot settings,
            CancellationToken cancellation);
    }
}
