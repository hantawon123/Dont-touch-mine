using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Client.Home;
using Game.Core.Flow;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Puts the update notice on the home screen when a newer build is on the
    /// download page (S15P21D205-1109).
    /// </summary>
    /// <remarks>
    /// Asked every time Home opens, not once at launch. A player who left the
    /// game running across a release comes back here after the match, and that
    /// is the first moment the old build stops finding rooms.
    /// </remarks>
    public sealed class HomeUpdateBridge : IStartable, IDisposable
    {
        /// <summary>
        /// Runs a build without the check, for a teammate testing a develop
        /// build that is by definition not the released one.
        /// </summary>
        public const string SkipArgument = "-skipVersionCheck";

        private readonly IHomeMenuView view;
        private readonly ReleaseInfoGateway releases;
        private readonly string localVersion;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        public HomeUpdateBridge(IHomeMenuView view, ReleaseInfoGateway releases, string localVersion)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.releases = releases ?? throw new ArgumentNullException(nameof(releases));
            this.localVersion = localVersion;
        }

        /// <summary>
        /// This build's revision, or null when the check should not run.
        /// </summary>
        public static string LocalVersion()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, SkipArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return Application.version;
        }

        public void Start()
        {
            CheckAsync(lifetime.Token).Forget();
        }

        public void Dispose()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        /// <summary>For tests: the whole check, awaited.</summary>
        public async UniTask CheckAsync(CancellationToken cancellation)
        {
            // Nothing to ask about. Skipping the request keeps an editor run
            // from sending one on every return to Home.
            if (!ClientVersionCheck.IsRevision(localVersion))
            {
                return;
            }

            string released;
            try
            {
                released = await releases.FetchReleasedRevisionAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            if (ClientVersionCheck.Compare(localVersion, released) == ClientVersionVerdict.Outdated)
            {
                Debug.Log($"[Update] This build {localVersion} is not the released {released}.");
                view.ShowUpdateNotice(releases.DownloadPageUrl);
            }
        }
    }
}
