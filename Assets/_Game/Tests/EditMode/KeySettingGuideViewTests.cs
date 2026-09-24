using Game.Client;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Core.Match;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class KeySettingGuideViewTests
    {
        [SetUp]
        public void ResetToggle()
        {
            KeySettingGuideView.SetUserVisible(true);
            KeySettingGuideView.UseSettings(null);
        }

        [TearDown]
        public void UnbindSettings() => KeySettingGuideView.UseSettings(null);

        [Test]
        public void FocusFollowsModeAndClearsPreviousEmphasis()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var guide = KeySettingGuideView.Create(canvas.transform);
                guide.SetFocus("앉아서 통과", ControlAction.Crouch);
                var crouch = guide.transform.Find("Row1/Action").GetComponent<TMPro.TMP_Text>();
                Assert.That(crouch.fontStyle, Is.EqualTo(TMPro.FontStyles.Bold));
                Assert.That(guide.transform.Find("Focus").GetComponent<TMPro.TMP_Text>().text, Does.Contain("C"));
                guide.SetFocus("상자 내려놓기", ControlAction.Interact);
                Assert.That(crouch.fontStyle, Is.EqualTo(TMPro.FontStyles.Normal));
                guide.SetMode(KeySettingGuideView.Mode.Carrying);
                Assert.That(guide.transform.Find("Row2/Action").GetComponent<TMPro.TMP_Text>().fontStyle, Is.EqualTo(TMPro.FontStyles.Bold));
                Assert.That(KeySettingGuideView.CarryingLabels[0], Is.EqualTo(KeySettingGuideView.RightClickKeyLabel));
                Assert.That(KeySettingGuideView.CarryingLabels[1], Is.EqualTo(KeySettingGuideView.ClickKeyLabel));
                guide.SetFocus(null);
                Assert.That(guide.transform.Find("Focus").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void ShouldToggle_IgnoresBlockedInput()
        {
            Assert.That(KeySettingGuideView.ShouldToggle(true, false), Is.True);
            Assert.That(KeySettingGuideView.ShouldToggle(true, true), Is.False);
            Assert.That(KeySettingGuideView.ShouldToggle(false, false), Is.False);
        }

        [Test]
        public void SetUserVisible_HidesTheGuideWithoutDisablingIt()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = KeySettingGuideView.Create(canvas.transform);
                KeySettingGuideView.SetUserVisible(false);
                view.SetVisible(true);

                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(view.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Create_PlacesSharedRowsOnTheRightEdge()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var guide = KeySettingGuideView.Create(canvas.transform).GetComponent<RectTransform>();

                Assert.That(guide.name, Is.EqualTo(KeySettingGuideView.RootName));
                Assert.That(guide.anchorMin.x, Is.EqualTo(1f));
                Assert.That(guide.pivot, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(guide.anchoredPosition, Is.EqualTo(new Vector2(-KeySettingGuideView.MarginRight, 0f)));
                Assert.That(guide.sizeDelta, Is.EqualTo(KeySettingGuideView.PanelSize));
                Assert.That(
                    guide.Find("Row0/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.Actions[0]));
                Assert.That(
                    guide.Find("Row5/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.Labels[5]));
                Assert.That(
                    guide.Find("Row6/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.ToggleAction));
                Assert.That(
                    guide.Find("Row6/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.ToggleKeyLabel));
                Assert.That(
                    guide.Find("Row7/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.EmoteAction));
                Assert.That(
                    guide.Find("Row7/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.EmoteKeyLabel));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void LobbyAndPlayground_AttachTheSameGuide()
        {
            var lobby = new GameObject("LobbyHud", typeof(RectTransform), typeof(Canvas));
            var playground = new GameObject("PlaygroundHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var lobbyHud = lobby.AddComponent<LobbyHudView>();
                lobbyHud.EnsureSharedGuide();
                var playgroundHud = playground.AddComponent<NetworkMatchHudView>();
                playgroundHud.SetPhase(MatchPhase.Hiding, "숨기는사람");

                var lobbyGuide = lobby.transform.Find(KeySettingGuideView.RootName);
                var playgroundGuide = playground.transform.Find(KeySettingGuideView.RootName);
                Assert.That(lobbyGuide, Is.Not.Null);
                Assert.That(playgroundGuide, Is.Not.Null);
                Assert.That(lobbyGuide.GetComponent<KeySettingGuideView>(), Is.Not.Null);
                Assert.That(playgroundGuide.GetComponent<KeySettingGuideView>(), Is.Not.Null);
                Assert.That(
                    ((RectTransform)lobbyGuide).anchoredPosition,
                    Is.EqualTo(((RectTransform)playgroundGuide).anchoredPosition));
                Assert.That(lobbyGuide.gameObject.activeSelf, Is.True);
                Assert.That(playgroundGuide.gameObject.activeSelf, Is.True);

                playgroundHud.SetPhase(MatchPhase.Searching, string.Empty);
                Assert.That(playgroundGuide.gameObject.activeSelf, Is.True);
                playgroundHud.SetPhase(MatchPhase.Highlight, string.Empty);
                Assert.That(playgroundGuide.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(lobby);
                Object.DestroyImmediate(playground);
            }
        }

        [Test]
        public void ActionsFor_SwapsOnlyTheHeldItemRows()
        {
            Assert.That(KeySettingGuideView.ActionsFor(false), Is.EqualTo(KeySettingGuideView.Actions));
            Assert.That(KeySettingGuideView.ActionsFor(true)[0], Is.EqualTo(UiLocale.Applied(UiText.Guide.Placement)));
            Assert.That(KeySettingGuideView.ActionsFor(true)[1], Is.EqualTo(UiLocale.Applied(UiText.Guide.Throw)));
            Assert.That(KeySettingGuideView.ActionsFor(true)[2], Is.EqualTo(UiLocale.Applied(UiText.Guide.Drop)));
            Assert.That(
                KeySettingGuideView.LabelsFor(true)[0],
                Is.EqualTo(KeySettingGuideView.RightClickKeyLabel));
            Assert.That(KeySettingGuideView.LabelsFor(true)[1], Is.EqualTo(KeySettingGuideView.ClickKeyLabel));
            Assert.That(KeySettingGuideView.LabelsFor(true)[2], Is.EqualTo("F"));
            Assert.That(
                KeySettingGuideView.ActionsFor(true)[KeySettingGuideView.CarryingActions.Length - 2],
                Is.EqualTo(KeySettingGuideView.ToggleAction));
            Assert.That(
                KeySettingGuideView.ActionsFor(true)[KeySettingGuideView.CarryingActions.Length - 1],
                Is.EqualTo(KeySettingGuideView.EmoteAction));
            Assert.That(
                KeySettingGuideView.LabelsFor(true)[KeySettingGuideView.CarryingLabels.Length - 2],
                Is.EqualTo(KeySettingGuideView.ToggleKeyLabel));
            Assert.That(
                KeySettingGuideView.LabelsFor(true)[KeySettingGuideView.CarryingLabels.Length - 1],
                Is.EqualTo(KeySettingGuideView.EmoteKeyLabel));
            Assert.That(KeySettingGuideView.PanelSizeFor(true), Is.EqualTo(KeySettingGuideView.CarryingPanelSize));
        }

        [Test]
        public void SetCarrying_ReplacesTheTopRowsAndKeepsTheSharedKeys()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = KeySettingGuideView.Create(canvas.transform);
                view.SetCarrying(true);
                var guide = view.GetComponent<RectTransform>();

                Assert.That(view.IsCarrying, Is.True);
                Assert.That(guide.sizeDelta, Is.EqualTo(KeySettingGuideView.CarryingPanelSize));
                Assert.That(
                    guide.Find("Row0/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(UiLocale.Applied(UiText.Guide.Placement)));
                Assert.That(
                    guide.Find("Row1/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(UiLocale.Applied(UiText.Guide.Throw)));
                Assert.That(
                    guide.Find("Row1/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.ClickKeyLabel));
                Assert.That(
                    guide.Find("Row2/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(UiLocale.Applied(UiText.Guide.Drop)));
                Assert.That(
                    guide.Find("Row2/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("F"));
                Assert.That(
                    guide.Find("Row8/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.ToggleAction));
                Assert.That(
                    guide.Find("Row8/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.ToggleKeyLabel));
                Assert.That(
                    guide.Find("Row9/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.EmoteAction));

                view.SetCarrying(false);
                Assert.That(view.IsCarrying, Is.False);
                Assert.That(guide.sizeDelta, Is.EqualTo(KeySettingGuideView.PanelSize));
                Assert.That(
                    guide.Find("Row0/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.Actions[0]));
                Assert.That(guide.Find("Row8").gameObject.activeSelf, Is.False);
                Assert.That(guide.Find("Row9").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetMode_ShowsPlacementRowsWithCompactQE()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = KeySettingGuideView.Create(canvas.transform);
                view.SetMode(KeySettingGuideView.Mode.Placing);
                var guide = view.GetComponent<RectTransform>();

                Assert.That(view.IsPlacing, Is.True);
                Assert.That(guide.sizeDelta, Is.EqualTo(KeySettingGuideView.PlacingPanelSize));
                Assert.That(
                    guide.Find("Row0/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(UiLocale.Applied(UiText.Guide.Place)));
                Assert.That(
                    guide.Find("Row0/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.ClickKeyLabel));
                Assert.That(
                    guide.Find("Row1/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(UiLocale.Applied(UiText.Guide.Rotate)));
                Assert.That(
                    guide.Find("Row1/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.RightClickKeyLabel));
                Assert.That(
                    guide.Find("Row2/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(UiLocale.Applied(UiText.Guide.Twist)));
                Assert.That(
                    guide.Find("Row2/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.RotateYawKeyLabel));
                Assert.That(
                    guide.Find("Row2/Key/Label").GetComponent<TMPro.TMP_Text>().fontSize,
                    Is.EqualTo(KeySettingGuideView.CompactKeyChipFontSize));
                Assert.That(
                    guide.Find("Row3/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(UiLocale.Applied(UiText.Guide.Drop)));
                Assert.That(
                    guide.Find("Row3/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("F"));
                Assert.That(
                    guide.Find("Row9/Action").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(KeySettingGuideView.ToggleAction));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Carrying_DrawsThePlacementKeyAsAnIcon()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = KeySettingGuideView.Create(canvas.transform);
                view.SetCarrying(true);
                var chip = view.transform.Find("Row0/Key");
                var icon = chip.Find("Icon").GetComponent<UnityEngine.UI.Image>();

                Assert.That(icon.gameObject.activeSelf, Is.True);
                Assert.That(chip.Find("Label").gameObject.activeSelf, Is.False);
                Assert.That(
                    icon.sprite,
                    Is.EqualTo(Resources.Load<Sprite>(
                        KeySettingGuideView.PlacementModeIconResource)));
                Assert.That(icon.sprite, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void IconResourceFor_ReadsTheBoundKeyRatherThanTheLabel()
        {
            Assert.That(
                KeySettingGuideView.IconResourceFor(
                    KeySettingGuideView.Mode.Carrying, ControlAction.PlacementMode),
                Is.EqualTo(KeySettingGuideView.PlacementModeIconResource));
            Assert.That(
                KeySettingGuideView.IconResourceFor(
                    KeySettingGuideView.Mode.Placing, ControlAction.PlacementMode),
                Is.EqualTo(KeySettingGuideView.RightClickIconResource));
            Assert.That(
                KeySettingGuideView.IconResourceFor(
                    KeySettingGuideView.Mode.Placing, ControlAction.PrimaryAction),
                Is.EqualTo(KeySettingGuideView.LeftClickIconResource));
            Assert.That(
                KeySettingGuideView.IconResourceFor(
                    KeySettingGuideView.Mode.Carrying, ControlAction.Interact),
                Is.Null);
        }

        [Test]
        public void IconResourceFor_SpellsOutAPlacementKeyReboundToTheKeyboard()
        {
            var system = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            try
            {
                KeySettingGuideView.UseSettings(system);
                system.Apply(system.Current.With(ControlAction.PlacementMode, "r"));

                Assert.That(
                    KeySettingGuideView.IconResourceFor(
                        KeySettingGuideView.Mode.Carrying, ControlAction.PlacementMode),
                    Is.Null);
            }
            finally
            {
                KeySettingGuideView.UseSettings(null);
            }
        }

        [Test]
        public void LabelsFor_UsesAppliedControlBindings()
        {
            var settings = ControlCatalog.Defaults.With(ControlAction.Crouch, "x");
            var labels = KeySettingGuideView.LabelsFor(KeySettingGuideView.Mode.Default, settings);

            Assert.That(labels[0], Is.EqualTo("좌클릭"));
            Assert.That(labels[1], Is.EqualTo("X"));
            Assert.That(labels[5], Is.EqualTo("SPACE"));
            Assert.That(labels[6], Is.EqualTo("L"));
            Assert.That(labels[7], Is.EqualTo("X"));
        }

        [Test]
        public void LabelsFor_UsesReboundKeyGuideToggle()
        {
            var settings = ControlCatalog.Defaults.With(ControlAction.ToggleKeyGuide, "k");
            var labels = KeySettingGuideView.LabelsFor(KeySettingGuideView.Mode.Default, settings);

            Assert.That(labels[6], Is.EqualTo("K"));
        }

        [Test]
        public void UseSettings_RefreshesGuideWhenBindingsChange()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            var system = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            try
            {
                var view = KeySettingGuideView.Create(canvas.transform);
                KeySettingGuideView.UseSettings(system);
                var guide = view.GetComponent<RectTransform>();

                Assert.That(
                    guide.Find("Row1/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("C"));

                system.Apply(system.Current.With(ControlAction.Crouch, "x"));
                Assert.That(
                    guide.Find("Row1/Key/Label").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("X"));
            }
            finally
            {
                KeySettingGuideView.UseSettings(null);
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
