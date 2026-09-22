using UnityEngine;

namespace Game.Client.Tutorial
{
    public interface ITutorialCompletionStore
    {
        bool IsCurrentVersionCompleted { get; }
        void MarkCurrentVersionCompleted();
    }

    /// <summary>
    /// Keeps tutorial completion on this PC. Incrementing <see cref="CurrentVersion"/>
    /// makes players run a materially changed tutorial once again.
    /// </summary>
    public sealed class PlayerPrefsTutorialCompletionStore : ITutorialCompletionStore
    {
        public const int CurrentVersion = 1;
        internal const string CompletedVersionKey = "tutorial.completed-version";

        public bool IsCurrentVersionCompleted =>
            PlayerPrefs.GetInt(CompletedVersionKey, 0) >= CurrentVersion;

        public void MarkCurrentVersionCompleted()
        {
            PlayerPrefs.SetInt(CompletedVersionKey, CurrentVersion);
            PlayerPrefs.Save();
        }
    }
}
