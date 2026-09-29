#if UNITY_EDITOR
using System.Collections;
using Game.Client.Common;
using Game.Client.Match;
using Game.Client.Settings;
using Game.Core.Match;
using Game.Core.Settings;
using Game.Network.Players;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public sealed class InterfaceRuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator Bubble_SitsAboveNameplate()
        {
            var parent = new UnityEngine.GameObject("ChatRoot");
            var player = new UnityEngine.GameObject("Player");
            try
            {
                var visual = new UnityEngine.GameObject("Visual");
                visual.transform.SetParent(player.transform, false);
                var body = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
                body.transform.SetParent(visual.transform, false);
                body.transform.localPosition = new UnityEngine.Vector3(0f, 0.5f, 0f);

                var nameplate = Game.Client.Players.PlayerNameplateView.Attach(player.transform);
                nameplate.SetNickname("이름");

                var bubbles = MatchChatBubbleView.Create(parent.transform);
                bubbles.BindPlayer("P1", player.transform);
                bubbles.Show(new Game.Core.Lobby.LobbyChatMessage("P1", "이름", "안녕"));
                yield return null;
                yield return null;

                var bubble = player.transform.Find("Match Chat Bubble")
                    .GetComponent<UnityEngine.RectTransform>();
                var halfHeight = bubble.rect.height * 0.5f * UnityEngine.Mathf.Abs(bubble.lossyScale.y);
                var nameRect = nameplate.GetComponent<TextMeshPro>().rectTransform;
                var nameTop = nameRect.position.y + nameRect.rect.height * Mathf.Abs(nameRect.lossyScale.y) * 0.5f;
                Assert.That(bubble.position.y - halfHeight, Is.GreaterThan(nameTop),
                    "The bubble must leave a visible gap above the nickname.");
            }
            finally
            {
                UnityEngine.Object.Destroy(parent);
                UnityEngine.Object.Destroy(player);
            }
            yield return null;
        }

        private NetworkRunner runner;
        [UnityTest]
        public IEnumerator Hud_ChangesScaleWithoutAccumulation_AndKeepsEssentialPresentation()
        {
            var root = new GameObject("HUD test", typeof(Canvas), typeof(CanvasScaler));
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var hud = root.AddComponent<NetworkMatchHudView>();
            var labelRoot = new GameObject("Sample", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelRoot.transform.SetParent(root.transform, false);
            var label = labelRoot.GetComponent<TextMeshProUGUI>(); label.fontSize = 30;
            var settings = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
            var chat = MatchChatView.Create(root.transform);
            var view = root.AddComponent<InterfaceHudView>(); view.Bind(settings, () => 24);
            try
            {
                yield return null;
                settings.Apply(settings.Current.With(InterfaceOption.UiScale, InterfaceCatalog.Large)
                    .With(InterfaceOption.FontScale, InterfaceCatalog.Large).With(InterfaceOption.InGameUi, InterfaceCatalog.Off));
                yield return null;
                Assert.That(label.fontSize, Is.EqualTo(34.5f).Within(0.01f));
                Assert.That(
                    scaler.referenceResolution.x,
                    Is.EqualTo(HudScreenScale.ScaledReference.x / 1.15f).Within(0.01f));
                Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(chat.enabled, Is.False, "hidden chat must not capture Enter input");
                yield return null;
                Assert.That(label.fontSize, Is.EqualTo(34.5f).Within(0.01f), "scale must not compound");
                hud.SetPhase(MatchPhase.Highlight, "");
                yield return null;
                Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                Assert.That(chat.enabled, Is.True);
                settings.Apply(settings.Defaults);
                yield return null;
                Assert.That(label.fontSize, Is.EqualTo(30));
                Assert.That(scaler.referenceResolution, Is.EqualTo(HudScreenScale.ScaledReference));
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator RealNetworkPrefab_SpawnsWithPrivacyState_AndAcceptsOwnerRpc()
        {
            runner = new GameObject("Privacy single runner").AddComponent<NetworkRunner>();
            // Match production's server-side speaker registry without connecting to Voice.
            var voice = runner.gameObject.AddComponent<Photon.Voice.Unity.VoiceConnection>();
            voice.enabled = false;
            var start = runner.StartGame(new StartGameArgs { GameMode = GameMode.Single });
            var deadline = Time.realtimeSinceStartup + 30;
            while (!start.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(start.IsCompleted, Is.True, "single runner startup timed out");
            Assert.That(start.Result.Ok, Is.True, start.Result.ToString());
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);
            var spawned = runner.Spawn(prefab, inputAuthority: runner.LocalPlayer);
            var avatar = spawned.GetComponent<PlayerAvatar>();
            Assert.That(avatar.NicknameVisibility, Is.Zero);
            avatar.RPC_SetNicknameVisibility(2, "account-one,account-two");
            yield return null;
            Assert.That(avatar.NicknameVisibility, Is.EqualTo(2));
            Assert.That(avatar.NicknameViewers.ToString(), Is.EqualTo("account-one,account-two"));
            avatar.RPC_SetNicknameVisibility(9, "invalid");
            Assert.That(avatar.NicknameVisibility, Is.EqualTo(2));
            var interactor = spawned.GetComponent<Game.Client.Interactions.PlayerInteractor>();
            interactor.SetInterfaceHudVisible(false);
            var beforeHighlight = interactor.PresentationHudVisible;
            interactor.SetHudVisible(false);
            interactor.SetHudVisible(beforeHighlight);
            Assert.That(interactor.HudVisible, Is.False);
            interactor.SetInterfaceHudVisible(true);
            Assert.That(interactor.HudVisible, Is.True, "highlight restore must not overwrite the independent interface preference");
            runner.Despawn(spawned);
        }

        [UnityTearDown]
        public IEnumerator ShutdownRunner()
        {
            if (runner == null) yield break;
            var shutdown = runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
            if (runner != null) Object.Destroy(runner.gameObject);
            runner = null;
        }
    }
}
#endif
