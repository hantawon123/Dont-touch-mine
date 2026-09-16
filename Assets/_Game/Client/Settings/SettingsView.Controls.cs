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
    /// The 컨트롤 page: a key on every action, then the two points of view and
    /// what the mouse does in each.
    /// </summary>
    /// <remarks>
    /// Five headed groups, laid out down a running cursor the way the 사운드
    /// page is. Its rows come in three shapes: a key button, a slider, and a
    /// picker.
    /// </remarks>
    public sealed partial class SettingsView
    {
        /// <summary>
        /// What moves the player, in the order the rows are drawn: where they
        /// are, how fast, what posture, and which way they see it from.
        /// </summary>
        private static readonly ControlAction[] MovementRows =
        {
            ControlAction.MoveForward,
            ControlAction.MoveLeft,
            ControlAction.MoveBackward,
            ControlAction.MoveRight,
            ControlAction.Sprint,
            ControlAction.Jump,
            ControlAction.Crouch,
            ControlAction.Prone,
            ControlAction.ToggleView
        };

        /// <summary>What the player does to the world, in the same way.</summary>
        private static readonly ControlAction[] ActionRows =
        {
            ControlAction.PrimaryAction,
            ControlAction.Interact,
            ControlAction.PlacementMode,
            ControlAction.RotateLeft,
            ControlAction.RotateRight,
            ControlAction.RaiseObject,
            ControlAction.LowerObject,
            ControlAction.ToggleKeyGuide
        };

        private readonly Dictionary<ControlAction, KeyButton> keyButtons =
            new Dictionary<ControlAction, KeyButton>();

        private readonly Dictionary<ControlSensitivity, VolumeSlider> sensitivitySliders =
            new Dictionary<ControlSensitivity, VolumeSlider>();

        private readonly Dictionary<ControlToggle, Stepper> reversalSteppers =
            new Dictionary<ControlToggle, Stepper>();

        private GameObject rebindBlocker;

        public event Action<ControlAction> RebindRequested;

        public event Action<ControlSensitivity, int> SensitivityChanged;

        public event Action<ControlToggle, int> ReversalStepRequested;

        public void ShowBinding(ControlAction action, string label)
        {
            if (keyButtons.TryGetValue(action, out var button))
            {
                button.SetKey(label);
            }
        }

        public void ShowRebinding(ControlAction? listening)
        {
            foreach (var pair in keyButtons)
            {
                pair.Value.SetListening(listening.HasValue && listening.Value == pair.Key);
            }

            if (rebindBlocker != null)
            {
                rebindBlocker.SetActive(listening.HasValue);
            }
        }

        /// <summary>
        /// Swallows every click while a plate is waiting for a press.
        /// </summary>
        /// <remarks>
        /// The press being waited for is often a mouse button — 공격/던지기/배치
        /// is on 좌클릭 — and a click that both answers the wait and
        /// works the button under the pointer would be a trap: choosing 좌클릭
        /// over 적용하기 would apply the settings.
        /// <para>
        /// It takes the press rather than merely covering the screen, so the
        /// release that follows belongs to it too. A button needs both halves
        /// of a click to fire, so nothing underneath goes off afterwards
        /// either.
        /// </para>
        /// </remarks>
        private void CreateRebindBlocker(RectTransform canvas)
        {
            var rect = CreateRect("RebindBlocker", canvas);
            Stretch(rect);
            AddImage(rect, Color.clear, raycastTarget: true);
            rect.gameObject.SetActive(false);
            rebindBlocker = rect.gameObject;
        }

        public void ShowSensitivity(ControlSensitivity sensitivity, int percent)
        {
            if (sensitivitySliders.TryGetValue(sensitivity, out var slider))
            {
                slider.Show(percent);
            }
        }

        public void ShowReversal(ControlToggle toggle, string label, bool canStep)
        {
            if (reversalSteppers.TryGetValue(toggle, out var stepper))
            {
                stepper.Show(label, canStep);
            }
        }

        private void CreateControlsPage(RectTransform window)
        {
            var page = CreatePage(window, SettingsTab.Controls, 0f);
            var top = 0f;

            top = AddSection(page, SettingsStyle.Controls.MicrophoneHeading, top);
            top = AddKeyRow(page, ControlAction.MicrophoneTalk, top);
            top = AddKeyRow(page, ControlAction.VoiceToggle, top);
            top = AddKeyRow(page, ControlAction.ToggleSpeaker, top);

            top = AddSection(page, SettingsStyle.Controls.KeyboardMoveHeading, top);
            foreach (var action in MovementRows)
            {
                top = AddKeyRow(page, action, top);
            }

            top = AddSection(page, SettingsStyle.Controls.KeyboardActionHeading, top);
            foreach (var action in ActionRows)
            {
                top = AddKeyRow(page, action, top);
            }

            top = AddSection(page, SettingsStyle.Controls.FirstPersonHeading, top);
            top = AddSensitivityRow(page, ControlSensitivity.FirstPersonMouse, top);
            top = AddReversalRow(page, ControlToggle.FirstPersonInvertX, top);
            top = AddReversalRow(page, ControlToggle.FirstPersonInvertY, top);

            top = AddSection(page, SettingsStyle.Controls.ThirdPersonHeading, top);
            top = AddSensitivityRow(page, ControlSensitivity.ThirdPersonMouse, top);
            top = AddSensitivityRow(page, ControlSensitivity.ThirdPersonCamera, top);
            top = AddReversalRow(page, ControlToggle.ThirdPersonInvertX, top);
            top = AddReversalRow(page, ControlToggle.ThirdPersonInvertY, top);

            // The cursor stops a gap past the last row, which nothing follows.
            page.sizeDelta = new Vector2(
                SettingsStyle.Rows.Size.x, top - SettingsStyle.Rows.Gap);
        }

        private float AddKeyRow(RectTransform page, ControlAction action, float top)
        {
            var row = CreateRowAt(
                page,
                action + "Row",
                top,
                SettingsStyle.Controls.ActionLabel(action),
                SettingsStyle.Section.RowLabelLeft);
            keyButtons[action] = CreateKeyButton(row, action);
            return top + SettingsStyle.Rows.Pitch;
        }

        private float AddSensitivityRow(RectTransform page, ControlSensitivity sensitivity, float top)
        {
            var row = CreateRowAt(
                page,
                sensitivity + "Row",
                top,
                SettingsStyle.Controls.SensitivityLabel(sensitivity),
                SettingsStyle.Section.RowLabelLeft);
            sensitivitySliders[sensitivity] = CreateSlider(
                row,
                ControlCatalog.MinSensitivity,
                ControlCatalog.MaxSensitivity,
                percent => SensitivityChanged?.Invoke(sensitivity, percent));
            return top + SettingsStyle.Rows.Pitch;
        }

        private float AddReversalRow(RectTransform page, ControlToggle toggle, float top)
        {
            var row = CreateRowAt(
                page,
                toggle + "Row",
                top,
                SettingsStyle.Controls.ReversalLabel(toggle),
                SettingsStyle.Section.RowLabelLeft);
            reversalSteppers[toggle] = CreateStepper(
                row, steps => ReversalStepRequested?.Invoke(toggle, steps));
            return top + SettingsStyle.Rows.Pitch;
        }

        /// <summary>
        /// The plate showing which key an action is on, which is also the
        /// button that changes it.
        /// </summary>
        /// <remarks>
        /// Outlined rather than filled, unlike the other buttons on this
        /// screen: eighteen filled plates down one page would read as a wall,
        /// and a key is a label as much as a control. It fills in while it
        /// waits for a press, which is the one moment it is doing something.
        /// </remarks>
        private KeyButton CreateKeyButton(RectTransform row, ControlAction action)
        {
            var rect = CreateRect("KeyButton", row);
            SetAnchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(-SettingsStyle.Rows.RightMargin, 0f);
            rect.sizeDelta = SettingsStyle.KeyButton.Size;

            var fill = AddImage(
                rect,
                SettingsStyle.Palette.KeyIdleFill,
                HomeUiFonts.Rounded(SettingsStyle.KeyButton.Radius),
                raycastTarget: true);

            // On a child of its own, because a graphic is one to an object: a
            // second image on the plate itself is refused and comes back null.
            // A child also draws over the fill, which is where an outline goes.
            var strokeRect = CreateRect("Stroke", rect);
            Stretch(strokeRect);
            var stroke = AddImage(
                strokeRect,
                SettingsStyle.Palette.KeyStroke,
                HomeUiFonts.Outline(
                    SettingsStyle.KeyButton.Radius, SettingsStyle.KeyButton.StrokeThickness));

            var label = CreateText(
                "Label",
                rect,
                string.Empty,
                SettingsStyle.KeyButton.FontSize,
                SettingsStyle.Palette.KeyLabel,
                TextAlignmentOptions.Center,
                regularFont);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(SettingsStyle.KeyButton.TextPadding, 0f);
            label.rectTransform.offsetMax = new Vector2(-SettingsStyle.KeyButton.TextPadding, 0f);

            // A key's name can be a word rather than a letter — 좌클릭, SPACE,
            // BACKSPACE — so it shrinks to fit rather than spilling out.
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMax = SettingsStyle.KeyButton.FontSize;
            label.fontSizeMin = SettingsStyle.KeyButton.MinFontSize;
            label.overflowMode = TextOverflowModes.Ellipsis;

            var hover = rect.gameObject.AddComponent<HomeHoverHighlight>();

            var captured = action;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => RebindRequested?.Invoke(captured));
            buttons.Add(button);

            var keyButton = new KeyButton
            {
                Fill = fill,
                Stroke = stroke,
                Label = label,
                Hover = hover
            };
            keyButton.SetKey(ControlCatalog.UnboundLabel);
            return keyButton;
        }

        /// <summary>
        /// One action's key plate, so a row can be redrawn without the view
        /// keeping four fields for each of eighteen rows.
        /// </summary>
        private sealed class KeyButton
        {
            public Image Fill;
            public Image Stroke;
            public TMP_Text Label;
            public HomeHoverHighlight Hover;

            /// <summary>
            /// The key's name, kept so that a plate which stops waiting can go
            /// back to showing it.
            /// </summary>
            private string keyLabel = ControlCatalog.UnboundLabel;

            private bool listening;

            public void SetKey(string label)
            {
                keyLabel = label;
                Apply();
            }

            public void SetListening(bool value)
            {
                listening = value;
                Apply();
            }

            private void Apply()
            {
                if (Label != null)
                {
                    // The key it is on stays on it while it waits. Swapping the
                    // name for a word takes away the one thing the row is for,
                    // and the plate turning the accent colour already says it
                    // is listening — as does the screen, which stops answering.
                    Label.text = keyLabel;
                    Label.color = listening
                        ? SettingsStyle.Palette.KeyListeningLabel
                        : SettingsStyle.Palette.KeyLabel;
                }

                if (Stroke != null)
                {
                    Stroke.color = listening
                        ? SettingsStyle.Palette.KeyListeningStroke
                        : SettingsStyle.Palette.KeyStroke;
                }

                if (Hover != null && Fill != null)
                {
                    Hover.Bind(
                        Fill,
                        null,
                        listening
                            ? SettingsStyle.Palette.KeyListeningFill
                            : SettingsStyle.Palette.KeyIdleFill,
                        listening
                            ? SettingsStyle.Palette.KeyListeningFill
                            : SettingsStyle.Palette.KeyHoverFill);
                }
            }
        }
    }
}
