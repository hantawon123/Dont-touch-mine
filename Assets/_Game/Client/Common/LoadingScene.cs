using System;
using UnityEngine.SceneManagement;

namespace Game.Client.Common
{
    public static class LoadingScene
    {
        public const string Name = "Loading";
        public const string Path = "Assets/_Game/Content/Scenes/Loading.unity";

        public static bool IsLoading(Scene scene) =>
            scene.IsValid() && IsLoading(scene.name);

        public static bool IsLoading(string sceneName) =>
            string.Equals(sceneName, Name, StringComparison.Ordinal);
    }
}
