using Cysharp.Threading.Tasks;
using Game.Client.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Keeps the Loading scene loaded additively. The cover itself is created
    /// as a DontDestroyOnLoad root so a room or lobby Single-load cannot
    /// destroy it before the first Show.
    /// </summary>
    public sealed class LoadingSceneCoordinator : IStartable
    {
        public void Start()
        {
            EnsureSceneAsync().Forget(exception => Debug.LogException(exception));
        }

        private static async UniTask EnsureSceneAsync()
        {
            var scene = SceneManager.GetSceneByName(LoadingScene.Name);
            if (scene.IsValid() && scene.isLoaded)
            {
                return;
            }

            var operation = SceneManager.LoadSceneAsync(LoadingScene.Name, LoadSceneMode.Additive);
            if (operation == null)
            {
                return;
            }

            await operation.ToUniTask();
        }
    }
}
