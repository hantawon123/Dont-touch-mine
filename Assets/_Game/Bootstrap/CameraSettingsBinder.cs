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
        private readonly InterfaceSettingsSystem interfaceSettings;

        public CameraSettingsBinder(
            ControlSettingsSystem settings,
            CameraViewPreference viewPreference = null,
            InterfaceSettingsSystem interfaceSettings = null)
        {
            this.settings = settings;
            this.viewPreference = viewPreference;
            this.interfaceSettings = interfaceSettings;
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
                    camera.BindSettings(settings, viewPreference, interfaceSettings);
        }

        public void Dispose() => SceneManager.sceneLoaded -= Bind;
    }
}
