#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Game.Bootstrap;
using Game.Client.Cameras;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Flow;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class SettingsRuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator RealSettingsView_ReopensWithSavedValues_AndClosesInsideLobby()
        {
            var root = new GameObject("Settings smoke");
            var view = root.AddComponent<SettingsView>();
            var controls = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            var flow = new AppFlowSystem();
            flow.TryTransitionTo(AppFlowState.Lobby);
            using var presenter = new SettingsPresenter(view,
                new GeneralSettingsSystem(new InMemoryGeneralSettingsStore()),
                new GraphicsSettingsSystem(new InMemoryGraphicsSettingsStore()),
                new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore()),
                new SoundSettingsSystem(new InMemorySoundSettingsStore()),
                new NullMicrophoneTest(), controls, new FakeKeyCapture(),
                new NotificationSettingsSystem(new InMemoryNotificationSettingsStore()),
                new NoNavigation(), flow, () => root.SetActive(false));
            try
            {
                presenter.Start();
                yield return null;
                root.SetActive(false);
                controls.Apply(controls.Current.With(ControlSensitivity.FirstPersonMouse, 80));
                root.SetActive(true);
                yield return null;
                Assert.That(presenter.ControlDraft.Get(ControlSensitivity.FirstPersonMouse), Is.EqualTo(80));
                var closed = false;
                view.Closed += () => closed = true;
                view.RequestBack();
                Assert.That(closed, Is.True);
                Assert.That(root.activeSelf, Is.False);
                Assert.That(flow.CurrentState, Is.EqualTo(AppFlowState.Lobby));
            }
            finally
            {
                presenter.Dispose();
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator RealCameraRig_BindsSharedSettings_AndIgnoresViewKeyWhileModalOpen()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab);
            var rig = root.GetComponent<PlayerCameraController>();
            var settings = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using var binder = new CameraSettingsBinder(settings);
            try
            {
                binder.Start();
                yield return null;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.That(typeof(PlayerCameraController).GetField("controls", flags).GetValue(rig), Is.SameAs(settings));
                rig.SetCursorCaptureEnabled(false);
                var before = typeof(PlayerCameraController).GetField("isFirstPerson", flags).GetValue(rig);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
                InputSystem.Update();
                root.SendMessage("Update");
                Assert.That(typeof(PlayerCameraController).GetField("isFirstPerson", flags).GetValue(rig), Is.EqualTo(before));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                Object.Destroy(root);
            }
        }

        private sealed class NoNavigation : IHomeApplicationHost
        {
            public void Quit() => Assert.Fail("Unexpected navigation");
            public void OpenHome() => Assert.Fail("Lobby must remain loaded");
            public void OpenRoomBrowser() => Assert.Fail("Unexpected navigation");
            public void OpenCharacterCloset() => Assert.Fail("Unexpected navigation");
            public void OpenSettings() => Assert.Fail("Unexpected navigation");
            public void OpenTutorial() => Assert.Fail("Unexpected navigation");
            public void OpenLobby() => Assert.Fail("Unexpected navigation");
            public void JoinRoom(string roomCode) => Assert.Fail("Unexpected navigation");
            public void CreateRoom(string title, bool isPublic, int maxPlayers) => Assert.Fail("Unexpected navigation");
        }
    }
}
#endif
