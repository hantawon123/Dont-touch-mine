using Game.Client.Cameras;
using Game.Core.Settings;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class CameraSettingsBinder : IStartable, System.IDisposable
    {
        private readonly ControlSettingsSystem settings;
        private readonly CameraViewPreference viewPreference;

        public CameraSettingsBinder(
            ControlSettingsSystem settings,
            CameraViewPreference viewPreference = null)
        {
            this.settings = settings;
            this.viewPreference = viewPreference;
        }

        public void Start()
        {
            SceneManager.sceneLoaded += Bind;
            for (var i = 0; i < SceneManager.sceneCount; i++) Bind(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
        }

        private void Bind(Scene scene, LoadSceneMode mode)
        {
            if (!scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<PlayerCameraController>(true))
                    camera.BindSettings(settings, viewPreference);
        }

        public void Dispose() => SceneManager.sceneLoaded -= Bind;
    }
}
