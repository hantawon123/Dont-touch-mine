using Game.Core.Ports;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the Photon credential pair in Unity's player preferences, beside
    /// the chosen region.
    /// </summary>
    /// <remarks>
    /// Preferences for the same reason
    /// <see cref="PlayerPrefsServerRegionStore"/> uses them: two short strings,
    /// where a file would bring a format, a path and a migration story for no
    /// gain.
    /// <para>
    /// Not a secret worth hiding here. The token proves which account this is,
    /// and anyone who can read these preferences is already on the machine that
    /// account plays from. Obscuring it would only suggest a protection that is
    /// not there.
    /// </para>
    /// </remarks>
    public sealed class PlayerPrefsPhotonCredentialStore : IPhotonCredentialStore
    {
        private const string UserIdKey = "game.photon.userId";
        private const string TokenKey = "game.photon.token";

        public bool TryLoad(out string userId, out string photonToken)
        {
            userId = null;
            photonToken = null;

            if (!PlayerPrefs.HasKey(UserIdKey) || !PlayerPrefs.HasKey(TokenKey))
            {
                return false;
            }

            var savedId = PlayerPrefs.GetString(UserIdKey);
            var savedToken = PlayerPrefs.GetString(TokenKey);

            // Both or neither. Half a pair authenticates nobody, and answering
            // true with one of them empty pushes that check onto every caller.
            if (string.IsNullOrWhiteSpace(savedId) || string.IsNullOrWhiteSpace(savedToken))
            {
                return false;
            }

            userId = savedId;
            photonToken = savedToken;
            return true;
        }

        public void Save(string userId, string photonToken)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(photonToken))
            {
                // A server without Photon authentication configured sends no
                // token. Keeping the last good pair means turning it on later
                // does not start by wiping what every client had.
                return;
            }

            PlayerPrefs.SetString(UserIdKey, userId.Trim());
            PlayerPrefs.SetString(TokenKey, photonToken.Trim());

            // Written through immediately for the same reason the region is:
            // Unity flushes on a clean quit, and a crash right after signing in
            // is when losing this would matter most - the next launch would be
            // the offline one this store exists for.
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(UserIdKey);
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.Save();
        }
    }
}
