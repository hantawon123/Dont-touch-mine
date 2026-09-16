using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Common
{
    /// <summary>
    /// Spreads a Unity scene load across frames so the loading cover can keep
    /// drawing while bytes arrive, then activates on a later frame.
    /// </summary>
    public static class SceneLoadSlicer
    {
        public const float ActivationGate = 0.9f;

        public static bool IsReadyToActivate(float progress) =>
            progress >= ActivationGate;

        public static async UniTask YieldFrame()
        {
            await UniTask.Yield();
            await UniTask.NextFrame();
        }

        public static async UniTask ActivateWhenReady(AsyncOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            operation.allowSceneActivation = false;
            await UniTask.WaitUntil(() =>
                operation.isDone || IsReadyToActivate(operation.progress));
            await UniTask.NextFrame();
            operation.allowSceneActivation = true;
            await UniTask.WaitUntil(() => operation.isDone);
            await UniTask.NextFrame();
        }

        public static async UniTask LoadAdditiveAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                // Unity returns null rather than throwing for a scene it cannot
                // load — not in the build, or misnamed. Returning quietly here
                // is how a screen that never appeared left no trace.
                Debug.LogError($"[SceneTiming] Could not start loading scene '{sceneName}'.");
                return;
            }

            operation.priority = 100;
            await ActivateWhenReady(operation);
        }

        public static async UniTask LoadSingleAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            var previous = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
            try
            {
                var operation = SceneManager.LoadSceneAsync(sceneName);
                if (operation == null)
                {
                    return;
                }

                operation.priority = 100;
                await ActivateWhenReady(operation);
            }
            finally
            {
                Application.backgroundLoadingPriority = previous;
            }
        }
    }
}
