using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;

namespace Game.Backend
{
    /// <summary>
    /// <see cref="IAccountGateway"/> against the backend's account endpoints.
    /// </summary>
    public sealed class AccountGateway : IAccountGateway
    {
        private const string Accounts = "/api/v1/accounts";
        private const string Me = "/api/v1/accounts/me";

        private readonly BackendClient client;

        public AccountGateway(BackendClient client)
        {
            this.client = client;
        }

        public async UniTask<BackendResult<AccountSnapshot>> SignInAsync(
            CancellationToken cancellation)
        {
            // The device identifier is read from the session rather than passed
            // in, so it never travels through a caller that has no use for it.
            var body = new IssueAccountRequestDto { deviceId = client.Session.DeviceId };

            var answer = await client.CallAsync<AccountResponseDto>(
                HttpMethod.Post, Accounts, body, BackendAuth.None, cancellation);

            if (!answer.Ok)
            {
                return BackendResult<AccountSnapshot>.Failed(answer.Failure);
            }

            // 201 and 200 tell new from returning, and neither is read. What the
            // client actually wants to know — whether to ask for a nickname — is
            // nicknameSet, which is in the body and stays true across launches.
            var snapshot = Map(answer.Value);
            if (!snapshot.Ok)
            {
                return snapshot;
            }

            client.Session.Adopt(snapshot.Value.UserId);
            return snapshot;
        }

        public async UniTask<BackendResult<AccountSnapshot>> RefreshAsync(
            CancellationToken cancellation)
        {
            var answer = await client.CallAsync<AccountResponseDto>(
                HttpMethod.Get, Me, null, BackendAuth.UserId, cancellation);

            return answer.Ok
                ? Map(answer.Value)
                : BackendResult<AccountSnapshot>.Failed(answer.Failure);
        }

        public async UniTask<BackendResult<AccountSnapshot>> RenameAsync(
            string nickname, CancellationToken cancellation)
        {
            var body = new UpdateNicknameRequestDto { nickname = nickname };

            var answer = await client.CallAsync<AccountResponseDto>(
                HttpMethod.Patch, Me, body, BackendAuth.UserId, cancellation);

            return answer.Ok
                ? Map(answer.Value)
                : BackendResult<AccountSnapshot>.Failed(answer.Failure);
        }

        public async UniTask<BackendResult> DeleteAccountAsync(CancellationToken cancellation)
        {
            // The only call that sends the device identifier. Deletion cannot be
            // undone, and a public user id is not proof of anything.
            var answer = await client.CallAsync(
                HttpMethod.Delete, Me, null, BackendAuth.UserIdAndDevice, cancellation);

            if (answer.Ok)
            {
                // The account this session named no longer exists. Left set, the
                // next call would identify as a deleted account and come back
                // ACCOUNT_NOT_FOUND from somewhere far from here.
                client.Session.Clear();
            }

            return answer;
        }

        /// <remarks>
        /// A response missing a user id or a nickname cannot become an
        /// <see cref="AccountSnapshot"/>, whose constructor refuses both. Caught
        /// here as a failure instead of thrown, so a malformed answer looks like
        /// every other failed call to the caller.
        /// </remarks>
        private static BackendResult<AccountSnapshot> Map(AccountResponseDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.userId) || string.IsNullOrWhiteSpace(dto.nickname))
            {
                return BackendResult<AccountSnapshot>.Failed(BackendFailure.Unknown);
            }

            return BackendResult<AccountSnapshot>.Success(
                new AccountSnapshot(dto.userId, dto.nickname, dto.nicknameSet));
        }
    }
}
