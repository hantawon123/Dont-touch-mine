using Game.Client.Lobby;
using Game.Client.Players;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerNameplateViewTests
    {
        [TestCase("민수")]
        [TestCase("A very long nickname 가나다")]
        [TestCase("<b>민수</b>\n둘째 줄")]
        public void VoiceLayout_MatchesUncachedMeasurementsAfterTextAndStyleChanges(string nickname)
        {
            var player = new GameObject("Player");
            try
            {
                var view = PlayerNameplateView.Attach(player.transform);
                view.SetNickname(nickname);
                var label = view.GetComponent<TextMeshPro>();
                var icon = view.transform.Find(PlayerNameplateView.VoiceIconName).GetComponent<SpriteRenderer>();
                foreach (var size in new[] { 3f, 5f, 2f })
                {
                    label.fontSize = size;
                    label.characterSpacing = size;
                    foreach (var state in new[] { 0, 1, 2 })
                    {
                        view.SetVoice(state == 2, state == 1);
                        var position = icon.transform.localPosition;
                        var scale = icon.transform.localScale;
                        label.ForceMeshUpdate();
                        var expected = label.GetPreferredValues(nickname);
                        Assert.That(position.x, Is.EqualTo(expected.x * 0.5f + PlayerNameplateView.VoiceIconGap + expected.y * 0.5f).Within(0.0001f));
                        Assert.That(scale.y * icon.sprite.rect.height / icon.sprite.pixelsPerUnit, Is.EqualTo(expected.y).Within(0.0001f));
                    }
                }
                view.SetNickname("");
                view.SetVoice(false, false);
                Assert.That(icon.enabled, Is.False);
                view.SetNickname(nickname);
                view.SetVoice(false, true);
                Assert.That(icon.enabled, Is.True);
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void RefreshPlacement_SitsJustAboveMeshTop()
        {
            var player = new GameObject("Player");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(player.transform, false);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.transform.SetParent(visual.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.5f, 0f);

                var view = PlayerNameplateView.Attach(player.transform);
                view.RefreshPlacement();

                Assert.That(
                    view.transform.position.y,
                    Is.EqualTo(1f + PlayerNameplateView.HeadClearance).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RefreshPlacement_FollowsVisualScale()
        {
            var player = new GameObject("Player");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(player.transform, false);
                visual.transform.localScale = Vector3.one * 2f;

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.transform.SetParent(visual.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.5f, 0f);

                var view = PlayerNameplateView.Attach(player.transform);
                view.RefreshPlacement();

                Assert.That(
                    view.transform.position.y,
                    Is.EqualTo(2f + PlayerNameplateView.HeadClearance).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RefreshPlacement_UsesCharacterControllerWhenMeshIsMissing()
        {
            var player = new GameObject("Player");
            try
            {
                var controller = player.AddComponent<CharacterController>();
                controller.height = 1.2f;
                controller.center = new Vector3(0f, 0.6f, 0f);

                var view = PlayerNameplateView.Attach(player.transform);
                view.RefreshPlacement();

                Assert.That(
                    view.transform.position.y,
                    Is.EqualTo(controller.bounds.max.y + PlayerNameplateView.HeadClearance)
                        .Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SetVoice_IconHeightMatchesNickname()
        {
            var player = new GameObject("Player");
            try
            {
                var view = PlayerNameplateView.Attach(player.transform);
                view.SetNickname("민수");
                view.SetVoice(muted: false, talking: false);

                var label = view.GetComponent<TextMeshPro>();
                var icon = view.transform.Find(PlayerNameplateView.VoiceIconName)
                    .GetComponent<SpriteRenderer>();
                Assert.That(label, Is.Not.Null);
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.sprite, Is.Not.Null);

                label.ForceMeshUpdate();
                var textHeight = label.GetPreferredValues("민수").y;
                var native = icon.sprite.rect.height / icon.sprite.pixelsPerUnit;
                var iconHeight = native * icon.transform.localScale.y;
                Assert.That(iconHeight, Is.EqualTo(textHeight).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SetVoice_ShowsGreenWhileTalking_WhiteIdle_AndGreyWhenMuted()
        {
            var player = new GameObject("Player");
            try
            {
                var view = PlayerNameplateView.Attach(player.transform);
                view.SetNickname("민수");
                view.SetVoice(muted: false, talking: true);
                var icon = view.transform.Find(PlayerNameplateView.VoiceIconName)
                    .GetComponent<SpriteRenderer>();
                Assert.That(icon.enabled, Is.True);
                Assert.That(icon.sprite, Is.EqualTo(LobbyPlayerListSprites.SoundGreen));

                view.SetVoice(muted: false, talking: false);
                Assert.That(icon.sprite, Is.EqualTo(LobbyPlayerListSprites.SoundWhite));

                view.SetVoice(muted: true, talking: true);
                Assert.That(icon.sprite, Is.EqualTo(LobbyPlayerListSprites.SoundMute));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
