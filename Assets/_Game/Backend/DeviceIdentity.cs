using System;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// This installation's device identifier — the credential an account is
    /// issued against.
    /// </summary>
    public static class DeviceIdentity
    {
        /// <remarks>
        /// Prefixed because preferences are shared by everything this company
        /// ships, and an unprefixed "deviceId" would collide.
        /// </remarks>
        private const string Key = "game.backend.deviceId";

        /// <summary>
        /// Overrides the stored identifier for one run: <c>-deviceId &lt;value&gt;</c>.
        /// </summary>
        /// <remarks>
        /// Two clients on one machine share preferences, so without this they
        /// share an account and none of the friend flows can be exercised — you
        /// cannot befriend yourself. Launch the second client with this argument
        /// to test with two accounts.
        /// </remarks>
        private const string OverrideArgument = "-deviceId";

        /// <summary>
        /// The saved identifier, generating and saving one on first run.
        /// </summary>
        /// <remarks>
        /// Generated rather than taken from <c>SystemInfo.deviceUniqueIdentifier</c>.
        /// That value is a hardware identifier whose length varies by platform,
        /// while the server's column stops at 36 characters, and handing a
        /// hardware identifier to a server is a thing to do on purpose, not by
        /// default.
        /// <para>
        /// Losing this value means losing the account: there is no other way back
        /// to it.
        /// </para>
        /// </remarks>
        public static string Current()
        {
            var overridden = FromCommandLine();
            if (overridden != null)
            {
                return overridden;
            }

            var saved = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrWhiteSpace(saved))
            {
                return saved;
            }

            // 32 characters, inside the server's 36 character limit.
            var issued = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(Key, issued);

            // Written through immediately. Unity flushes on a clean quit, and a
            // crash right after the first launch is exactly when losing this
            // would strand the account it just created.
            PlayerPrefs.Save();
            return issued;
        }

        private static string FromCommandLine()
        {
            string[] arguments;
            try
            {
                arguments = Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                // Some platforms refuse to hand these over. Not a reason to fail
                // to start; the saved identifier is the normal path anyway.
                return null;
            }

            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == OverrideArgument
                    && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return arguments[index + 1].Trim();
                }
            }

            return null;
        }
    }
}
