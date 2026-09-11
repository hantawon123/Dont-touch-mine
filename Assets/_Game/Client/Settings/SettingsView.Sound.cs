using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// The 사운드 page: two headed groups of rows, most of them sliders.
    /// </summary>
    /// <remarks>
    /// Laid out down a running cursor rather than by row index like the other
    /// pages, because a heading is not a row: it is shorter, has no plate, and
    /// asks for a gap above it that the first one does without.
    /// </remarks>
    public sealed partial class SettingsView
    {
        private static readonly SoundVolume[] SpeakerVolumes =
        {
            SoundVolume.Master,
            SoundVolume.Music,
            SoundVolume.Ambience,
            SoundVolume.Effects
        };

        private readonly Dictionary<SoundVolume, VolumeSlider> volumeSliders =
            new Dictionary<SoundVolume, VolumeSlider>();

        private Stepper deviceStepper;
        private Stepper inputModeStepper;
        private Button testButton;
        private Image testFill;
        private TMP_Text testLabel;
        private HomeHoverHighlight testHover;

        public event Action<SoundVolume, int> VolumeChanged;

        public event Action<int> MicrophoneDeviceStepRequested;

        public event Action<int> InputModeStepRequested;

        public event Action MicrophoneTestToggled;

        public void ShowVolume(SoundVolume volume, int percent)
        {
            if (volumeSliders.TryGetValue(volume, out var slider))
            {
                slider.Show(percent);
            }
        }

        public void ShowMicrophoneDevice(string label, bool canStep) =>
            deviceStepper?.Show(label, canStep);

        public void ShowInputMode(string label, bool canStep) =>
            inputModeStepper?.Show(label, canStep);

        /// <summary>
        /// Paints the test button for whether a test is running. Same plate
        /// either way; the running state borrows the accent so a test left
        /// going is not missed.
        /// </summary>
        public void ShowMicrophoneTest(bool running)
        {
            if (testLabel != null)
            {
                testLabel.text = running
                    ? SettingsStyle.MicrophoneTest.RunningLabel
                    : SettingsStyle.MicrophoneTest.IdleLabel;
                testLabel.color = running
                    ? SettingsStyle.Palette.TestRunningLabel
                    : SettingsStyle.Palette.TestIdleLabel;
            }

            if (testHover != null && testFill != null)
            {
                testHover.Bind(
                    testFill,
                    null,
                    running ? SettingsStyle.Palette.TestRunningFill : SettingsStyle.Palette.TestIdleFill,
                    running ? SettingsStyle.Palette.TestRunningHoverFill : SettingsStyle.Palette.TestIdleHoverFill);
            }
        }

        private void CreateSoundPage(RectTransform window)
        {
            var page = CreatePage(window, SettingsTab.Sound, 0f);
            var top = 0f;

            top = AddSection(page, SettingsStyle.Sound.SpeakerHeading, top);
            foreach (var volume in SpeakerVolumes)
            {
                top = AddVolumeRow(page, volume, top);
            }

            top = AddSection(page, SettingsStyle.Sound.MicrophoneHeading, top);

            var device = CreateRowAt(
                page, "DeviceRow", top, SettingsStyle.Sound.DeviceLabel, SettingsStyle.Section.RowLabelLeft);
            deviceStepper = CreateStepper(
                device, steps => MicrophoneDeviceStepRequested?.Invoke(steps), wrapValue: true);
            top += SettingsStyle.Rows.Pitch;

            var mode = CreateRowAt(
                page, "InputModeRow", top, SettingsStyle.Sound.InputModeLabel, SettingsStyle.Section.RowLabelLeft);
            inputModeStepper = CreateStepper(mode, steps => InputModeStepRequested?.Invoke(steps));
            top += SettingsStyle.Rows.Pitch;

            top = AddVolumeRow(page, SoundVolume.Microphone, top);

            var test = CreateRowAt(
                page, "MicrophoneTestRow", top, SettingsStyle.Sound.TestLabel, SettingsStyle.Section.RowLabelLeft);
            CreateTestButton(test);
            top += SettingsStyle.Rows.Size.y;

            page.sizeDelta = new Vector2(SettingsStyle.Rows.Size.x, top);
        }

        /// <summary>
        /// A heading over the rows that follow. Returns where the first of
        /// them starts.
        /// </summary>
        private float AddSection(RectTransform page, string heading, float top)
        {
            // Every heading but the first sits a gap below whatever came before
            // it; the first is flush with the top of the page.
            if (top > 0f)
            {
                top += SettingsStyle.Section.Gap;
            }

            var rect = CreateRect(heading + "Section", page);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(SettingsStyle.Rows.Size.x, SettingsStyle.Section.Height);

            var text = CreateText(
                "Label",
                rect,
                heading,
                SettingsStyle.Section.FontSize,
                SettingsStyle.Palette.SectionLabel,
                TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(SettingsStyle.Section.LabelLeft, 0f);

            return top + SettingsStyle.Section.Height + SettingsStyle.Section.Gap;
        }

        /// <summary>A row with a slider. Returns where the next row starts.</summary>
        private float AddVolumeRow(RectTransform page, SoundVolume volume, float top)
        {
            var row = CreateRowAt(
                page,
                volume + "VolumeRow",
                top,
                SettingsStyle.Sound.VolumeLabel(volume),
                SettingsStyle.Section.RowLabelLeft);
            volumeSliders[volume] = CreateSlider(
                row,
                SoundCatalog.MinVolume,
                SoundCatalog.MaxVolume,
                percent => VolumeChanged?.Invoke(volume, percent));
            return top + SettingsStyle.Rows.Pitch;
        }

        /// <summary>
        /// The slider and the figure beside it, ranged against the row's right
        /// edge with the figure outermost.
        /// </summary>
        /// <remarks>
        /// The figure is given a fixed width rather than fitted to its digits,
        /// so the track does not creep as 9% becomes 10% becomes 100%.
        /// <para>
        /// The strip that takes the pointer is taller than the 7 point track
        /// it draws: a track that thin cannot be grabbed. The handle is what
        /// Unity's slider moves, and its area is inset by half its width so the
        /// handle's centre — not its edge — reaches both ends of the track.
        /// </para>
        /// </remarks>
        /// <param name="changed">
        /// Told wherever the handle was dragged to, in whole steps between
        /// <paramref name="min"/> and <paramref name="max"/>.
        /// </param>
        private VolumeSlider CreateSlider(
            RectTransform row, int min, int max, Action<int> changed)
        {
            var percent = CreateText(
                "Percent",
                row,
                string.Empty,
                SettingsStyle.Slider.PercentFontSize,
                SettingsStyle.Palette.Value,
                TextAlignmentOptions.MidlineRight,
                regularFont);
            var percentRect = percent.rectTransform;
            SetAnchor(percentRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            percentRect.anchoredPosition = new Vector2(-SettingsStyle.Rows.RightMargin, 0f);
            percentRect.sizeDelta = new Vector2(SettingsStyle.Slider.PercentWidth, SettingsStyle.Rows.Size.y);

            var root = CreateRect("Slider", row);
            SetAnchor(root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            root.anchoredPosition = new Vector2(
                -(SettingsStyle.Rows.RightMargin
                  + SettingsStyle.Slider.PercentWidth
                  + SettingsStyle.Slider.PercentGap),
                0f);
            root.sizeDelta = new Vector2(SettingsStyle.Slider.TrackSize.x, SettingsStyle.Slider.HitHeight);
            AddImage(root, Color.clear, raycastTarget: true);

            var track = CreateRect("Track", root);
            SetAnchor(track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(0f, SettingsStyle.Slider.FillHeight);
            var trackBackground = CreateRect("Background", track);
            SetAnchor(trackBackground, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            trackBackground.anchoredPosition = Vector2.zero;
            trackBackground.sizeDelta = new Vector2(0f, SettingsStyle.Slider.TrackSize.y);
            AddImage(trackBackground, SettingsStyle.Palette.SliderTrack,
                HomeUiFonts.Rounded(SettingsStyle.Slider.TrackRadius));

            var fillArea = CreateRect("FillArea", track);
            SetAnchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            // Use exactly the handle centre's travel range for the fill endpoint.
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta = new Vector2(-SettingsStyle.Slider.HandleDiameter, SettingsStyle.Slider.FillHeight);

            var fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            // Extend only the fixed left edge; the right edge stays at the handle centre.
            fill.offsetMin = new Vector2(
                -SettingsStyle.Slider.HandleDiameter * 0.5f - SettingsStyle.Slider.FillLeftOverhang, 0f);
            fill.offsetMax = Vector2.zero;
            // Slider changes only the clipping window. Keep the coloured rail
            // at its full width so small values cannot squeeze its corners.
            fill.gameObject.AddComponent<RectMask2D>();
            var fillGraphic = CreateRect("Graphic", fill);
            SetAnchor(fillGraphic, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            fillGraphic.anchoredPosition = Vector2.zero;
            fillGraphic.sizeDelta = new Vector2(
                SettingsStyle.Slider.TrackSize.x + SettingsStyle.Slider.FillLeftOverhang,
                SettingsStyle.Slider.FillHeight);
            AddImage(
                fillGraphic,
                SettingsStyle.Palette.SliderFill,
                HomeUiFonts.Rounded(SettingsStyle.Slider.FillRadius));

            // As tall as the handle and inset by half of it on each side, so the
            // handle's centre — not its edge — reaches both ends of the track.
            // The height matters: Unity's slider stretches the handle across
            // this rect on the axis it is not sliding along, so a container any
            // taller would draw the circle as an ellipse.
            var handleArea = CreateRect("HandleArea", root);
            SetAnchor(handleArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            handleArea.anchoredPosition = Vector2.zero;
            handleArea.sizeDelta = new Vector2(
                -SettingsStyle.Slider.HandleDiameter, SettingsStyle.Slider.HandleDiameter);

            var handle = CreateRect("Handle", handleArea);
            SetAnchor(handle, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
            handle.anchoredPosition = Vector2.zero;

            // Nothing added to the stretched height, which is already the
            // diameter; the width is its own, because that is the axis the
            // slider drives to a point.
            handle.sizeDelta = new Vector2(SettingsStyle.Slider.HandleDiameter, 0f);
            var handleImage = AddImage(handle, SettingsStyle.Palette.SliderHandle, raycastTarget: true);
            handleImage.sprite = HomeUiFonts.CircleSprite;
            handleImage.type = Image.Type.Simple;

            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            slider.onValueChanged.AddListener(value => changed(Mathf.RoundToInt(value)));

            return new VolumeSlider { Slider = slider, Percent = percent };
        }

        private void CreateTestButton(RectTransform row)
        {
            var rect = CreateRect("MicrophoneTestButton", row);
            SetAnchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(-SettingsStyle.Rows.RightMargin, 0f);
            rect.sizeDelta = SettingsStyle.FeedbackRow.ButtonSize;

            testFill = AddImage(
                rect,
                SettingsStyle.Palette.TestIdleFill,
                HomeUiFonts.Rounded(SettingsStyle.FeedbackRow.ButtonRadius),
                raycastTarget: true);

            testLabel = CreateText(
                "Label",
                rect,
                SettingsStyle.MicrophoneTest.IdleLabel,
                SettingsStyle.FeedbackRow.ButtonFontSize,
                SettingsStyle.Palette.TestIdleLabel,
                TextAlignmentOptions.Center,
                regularFont);
            Stretch(testLabel.rectTransform);

            testHover = rect.gameObject.AddComponent<HomeHoverHighlight>();

            testButton = rect.gameObject.AddComponent<Button>();
            testButton.targetGraphic = testFill;
            testButton.transition = Selectable.Transition.None;
            testButton.onClick.AddListener(() => MicrophoneTestToggled?.Invoke());
            buttons.Add(testButton);

            ShowMicrophoneTest(false);
        }

        /// <summary>
        /// One row's slider and the figure beside it, so a row can be redrawn
        /// without the view keeping a field for each.
        /// </summary>
        private sealed class VolumeSlider
        {
            public UnityEngine.UI.Slider Slider;
            public TMP_Text Percent;

            /// <summary>
            /// Moves the handle without raising the slider's own event: the
            /// value came from the presenter, and echoing it back would set it
            /// again.
            /// </summary>
            public void Show(int percent)
            {
                if (Slider != null)
                {
                    Slider.SetValueWithoutNotify(percent);
                }

                if (Percent != null)
                {
                    Percent.text = string.Format(SettingsStyle.Slider.PercentFormat, percent);
                }
            }
        }
    }
}
