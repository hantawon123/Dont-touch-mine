using Game.Client;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Client.Voice;
using Game.Core.Settings;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class LobbyShortcutGuideViewTests
    {
        [TearDown]
        public void UnbindSettings() => VoiceView.UseSettings(null);

        [Test]
        public void Create_PlacesTheRowOnTheBottomRight()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyShortcutGuideView.Create(canvas.transform);
                var rect = view.GetComponent<RectTransform>();

                Assert.That(view.name, Is.EqualTo(LobbyShortcutGuideView.RootName));
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(
                    rect.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        -LobbyShortcutGuideView.MarginRight,
                        LobbyShortcutGuideView.MarginBottom)));
                Assert.That(
                    LobbyShortcutGuideView.MarginRight,
                    Is.EqualTo(KeySettingGuideView.MarginRight));
                Assert.That(LobbyShortcutGuideView.MarginBottom, Is.EqualTo(34f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Create_BuildsThreeKeyChipsWithSpecifiedType()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyShortcutGuideView.Create(canvas.transform);

                for (var index = 0; index < LobbyShortcutGuideView.KeyLabels.Length; index++)
                {
                    var key = view.transform.Find($"Item{index}/Key") as RectTransform;
                    var keySize = key.GetComponent<LayoutElement>();
                    var keyImage = key.GetComponent<Image>();
                    var keyLabel = key.Find("Label").GetComponent<TMP_Text>();
                    var action = view.transform.Find($"Item{index}/Action").GetComponent<TMP_Text>();

                    Assert.That(keySize.preferredWidth, Is.EqualTo(LobbyShortcutGuideView.KeyBoxSize));
                    Assert.That(keySize.preferredHeight, Is.EqualTo(LobbyShortcutGuideView.KeyBoxSize));
                    Assert.That(keyImage.color, Is.EqualTo(LobbyShortcutGuideView.KeyBoxColor));
                    Assert.That(keyImage.sprite, Is.EqualTo(HomeUiFonts.Rounded(LobbyShortcutGuideView.KeyBoxRadius)));
                    Assert.That(keyLabel.text, Is.EqualTo(LobbyShortcutGuideView.KeyLabels[index]));
                    Assert.That(keyLabel.fontSize, Is.EqualTo(LobbyShortcutGuideView.KeyFontSize));
                    Assert.That(keyLabel.font, Is.EqualTo(HomeUiFonts.ApplyRegular()));
                    Assert.That(action.text, Is.EqualTo(LobbyShortcutGuideView.Actions[index]));
                    Assert.That(action.fontSize, Is.EqualTo(LobbyShortcutGuideView.ActionFontSize));
                    Assert.That(action.font, Is.EqualTo(HomeUiFonts.ApplyMedium()));
                }
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Create_PlacesAMicToggleToTheRightOfSettings()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyShortcutGuideView.Create(canvas.transform);
                var settings = view.transform.Find("Item2");
                var micItem = view.transform.Find(VoiceView.MuteItemName);
                var mic = VoiceView.FindMuteSlot(view.transform);
                var micHint = micItem.Find(VoiceView.KeyHintName).GetComponent<TMP_Text>();

                Assert.That(settings, Is.Not.Null);
                Assert.That(mic, Is.Not.Null);
                Assert.That(micItem.GetSiblingIndex(), Is.GreaterThan(settings.GetSiblingIndex()));
                Assert.That(view.VoiceMuteButton, Is.Not.Null);
                Assert.That(view.VoiceBackground, Is.Not.Null);
                Assert.That(view.VoiceIcon, Is.Not.Null);
                Assert.That(view.VoiceBackground.color, Is.EqualTo(VoiceView.PlateColor));
                Assert.That(
                    view.VoiceBackground.sprite,
                    Is.EqualTo(HomeUiFonts.Rounded(VoiceView.ButtonRadius)));
                Assert.That(
                    view.VoiceMuteButton.GetComponent<LayoutElement>().preferredWidth,
                    Is.EqualTo(VoiceView.ButtonSize));
                Assert.That(
                    view.VoiceMuteButton.GetComponent<LayoutElement>().preferredHeight,
                    Is.EqualTo(VoiceView.ButtonSize));
                Assert.That(view.VoiceMuteButton.transition, Is.EqualTo(Selectable.Transition.None));
                Assert.That(
                    view.VoiceIcon.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(VoiceView.IconSize, VoiceView.IconSize)));
                Assert.That(view.VoiceIcon.sprite, Is.EqualTo(VoiceView.MicOnSprite));
                Assert.That(micHint.text, Is.EqualTo(VoiceView.MuteKeyLabel()));
                Assert.That(micHint.fontSize, Is.EqualTo(VoiceView.KeyHintFontSize));
                var speakerItem = view.transform.Find(VoiceView.SpeakerItemName);
                var speaker = VoiceView.FindSpeakerSlot(view.transform);
                var speakerHint = speakerItem.Find(VoiceView.KeyHintName).GetComponent<TMP_Text>();
                Assert.That(speaker, Is.Not.Null);
                Assert.That(speakerItem.GetSiblingIndex(), Is.GreaterThan(micItem.GetSiblingIndex()));
                Assert.That(view.VoiceSpeakerButton, Is.Not.Null);
                Assert.That(view.VoiceSpeakerIcon.sprite, Is.EqualTo(VoiceView.SpeakerOnSprite));
                Assert.That(speakerHint.text, Is.EqualTo(VoiceView.SpeakerKeyLabel()));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void LobbyHud_WiresTheMicToggleToVoiceView()
        {
            var canvas = new GameObject("LobbyHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var voice = canvas.AddComponent<VoiceView>();
                var hud = canvas.AddComponent<LobbyHudView>();
                var guide = hud.EnsureShortcutGuide();
                var raised = 0;
                voice.MuteToggleRequested += () => raised++;
                guide.VoiceMuteButton.onClick.Invoke();
                var speakerRaised = 0;
                voice.SpeakerToggleRequested += () => speakerRaised++;
                guide.VoiceSpeakerButton.onClick.Invoke();

                Assert.That(guide.VoiceMuteButton, Is.SameAs(
                    canvas.transform.Find(
                        $"{LobbyShortcutGuideView.RootName}/{VoiceView.MuteItemName}/{LobbyShortcutGuideView.VoiceButtonName}")
                        .GetComponent<Button>()));
                Assert.That(raised, Is.EqualTo(1));
                Assert.That(speakerRaised, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void LobbyHud_AttachesTheShortcutGuide()
        {
            var canvas = new GameObject("LobbyHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                var guide = hud.EnsureShortcutGuide();

                Assert.That(guide, Is.Not.Null);
                Assert.That(
                    canvas.transform.Find(LobbyShortcutGuideView.RootName),
                    Is.SameAs(guide.transform));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ShowChrome_RedrawsActionsInTheAppliedLanguage()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyShortcutGuideView.Create(canvas.transform);
                var store = new InMemoryGeneralSettingsStore();
                store.Save(new GeneralSettings("en"));
                using var locale = new UiLocale(new GeneralSettingsSystem(store));
                view.ShowChrome(locale);

                Assert.That(
                    view.transform.Find("Item0/Action").GetComponent<TMP_Text>().text,
                    Is.EqualTo("Character"));
                Assert.That(
                    view.transform.Find("Item1/Action").GetComponent<TMP_Text>().text,
                    Is.EqualTo("Players"));
                Assert.That(
                    view.transform.Find("Item2/Action").GetComponent<TMP_Text>().text,
                    Is.EqualTo("Settings"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void StartCountdown_FollowsTheAppliedLanguage()
        {
            var canvas = new GameObject("LobbyHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                hud.SetStartCountdown(10d);
                var countdown = canvas.transform.Find("Start countdown")
                    .GetComponent<TMP_Text>();

                Assert.That(countdown.text, Is.EqualTo("10초 뒤 게임이 시작됩니다"));

                var store = new InMemoryGeneralSettingsStore();
                store.Save(new GeneralSettings("en"));
                using var locale = new UiLocale(new GeneralSettingsSystem(store));
                hud.ShowChrome(locale);

                Assert.That(countdown.text, Is.EqualTo("Game starts in 10s"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void VoiceKeyHints_FollowAppliedControlBindings()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            var system = new ControlSettingsSystem(new InMemoryControlSettingsStore());
            try
            {
                VoiceView.UseSettings(system);
                var view = LobbyShortcutGuideView.Create(canvas.transform);
                var muteHint = view.transform.Find($"{VoiceView.MuteItemName}/{VoiceView.KeyHintName}")
                    .GetComponent<TMP_Text>();
                var speakerHint = view.transform.Find($"{VoiceView.SpeakerItemName}/{VoiceView.KeyHintName}")
                    .GetComponent<TMP_Text>();

                Assert.That(muteHint.text, Is.EqualTo("B"));
                Assert.That(speakerHint.text, Is.EqualTo("T"));

                system.Apply(system.Current
                    .With(ControlAction.VoiceToggle, "m")
                    .With(ControlAction.ToggleSpeaker, "n"));
                Assert.That(muteHint.text, Is.EqualTo("M"));
                Assert.That(speakerHint.text, Is.EqualTo("N"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
