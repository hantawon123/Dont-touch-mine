using System;
using System.Collections;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Lobby;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public interface IPlaySettingsView
    {
        event Action OpenRequested;
        event Action CloseRequested;
        event Action CopyRoomCodeRequested;
        event Action InviteRequested;
        event Action CopyPasswordRequested;
        event Action StartRequested;
        event Action ApplyRequested;

        bool HasUnappliedChanges { get; }

        void SetVisible(bool visible);
        void SetEditable(bool editable);
        void SetDraft(PlaySettingsDraft draft);
        PlaySettingsDraft ReadDraft();
        void SetUnappliedWarningVisible(bool visible);

        /// <summary>
        /// Asks to be closed as if the panel's own close button was pressed.
        /// through the presenter rather than hiding the panel directly: Esc has
        /// to back out of this panel, and the presenter tracks whether it is
        /// open. Hiding from outside would leave that flag saying open.
        /// </summary>
        void RequestClose();

        /// <summary>
        /// Asks to be opened as if the panel's own open button was pressed, so
        /// the presenter fills the draft and decides editability before the
        /// panel shows. Used by objects in the room that lead to this screen.
        /// </summary>
        void RequestOpen();
    }

    public sealed partial class PlaySettingsView : MonoBehaviour, IPlaySettingsView
    {
        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private GameObject panel;

        private Button copyRoomCodeButton;
        private Button roomCodeHitButton;
        private Image copyIconImage;
        private Text copyFeedbackText;
        private GameObject copyFeedbackRoot;
        private GameObject copyFeedbackShift;
        private Coroutine copyFeedbackRoutine;
        private bool copyCooldownActive;
        private Text titleText;
        private InputField titleInput;
        private Text roomCodeText;
        private Text maxPlayersText;
        private Button maxPlayersMinusButton;
        private Button maxPlayersPlusButton;
        private Text destructionLimitText;
        private Button destructionMinusButton;
        private Button destructionPlusButton;

        private GameObject overlayRoot;
        private TextMeshProUGUI gameStartLabel;
        private Button gameStartButton;

        private static readonly float[] SprintOptions = { 0.5f, 1f, 1.5f, 2f, 3f };
        private readonly List<Text> ruleValues = new();
        private readonly List<Button> ruleMinus = new();
        private readonly List<Button> rulePlus = new();
        private Slider hidingSlider;
        private Text hidingValue;
        private Slider searchingSlider;
        private Text searchingValue;
        private Image applyFill;
        private Text applyLabel;
        private Text applyWarning;
        private PlaySettingsDraft appliedDraft;

        private string title = string.Empty;
        private string roomCode = string.Empty;
        private bool passwordEnabled;
        private string password = string.Empty;
        private int maxPlayers = RoomSettings.MaxPlayerCount;
        private int destructionLimit = PlaySettingsDraft.DefaultDestructionLimit;
        private int selectedMapIndex;
        private bool editable;
        private MatchRuleSettings matchRules = MatchRuleSettings.Default;
        private IReadOnlyList<PlaySettingsMapOption> mapOptions = PlaySettingsMapCatalog.All;
        private readonly Dictionary<Button, UnityEngine.Events.UnityAction> boundActions = new();

        public event Action OpenRequested;
        public event Action CloseRequested;
        public event Action CopyRoomCodeRequested;
        public event Action InviteRequested;
        public event Action CopyPasswordRequested;
        public event Action StartRequested;
        public event Action ApplyRequested;

        public bool HasUnappliedChanges =>
            editable && !ReadDraft().Equals(appliedDraft);

        private void OnEnable()
        {
            EnsureOverlay();
            EnsureLayout();
            EnsureCloseButton();
            BindRuleControls();
            BindDurationSliders();
            if (titleInput != null) titleInput.onValueChanged.AddListener(OnTitleChanged);
            Bind(openButton, () => OpenRequested?.Invoke());
            Bind(closeButton, RequestClose);
            Bind(gameStartButton, RequestStart);
            Bind(copyRoomCodeButton, RequestCopyRoomCode);
            Bind(roomCodeHitButton, RequestCopyRoomCode);
            Bind(applyButton, RequestApply);
            Bind(revertButton, RequestRevert);
            Bind(maxPlayersMinusButton, () => SetMaxPlayers(maxPlayers - 1));
            Bind(maxPlayersPlusButton, () => SetMaxPlayers(maxPlayers + 1));
            Bind(destructionMinusButton, () => SetDestructionLimit(destructionLimit - 1));
            Bind(destructionPlusButton, () => SetDestructionLimit(destructionLimit + 1));
            Bind(mapPrevButton, () => StepMapSelection(-1));
            Bind(mapNextButton, () => StepMapSelection(1));
            Bind(categoryPrevButton, () => SelectCategory(-1));
            Bind(categoryNextButton, () => SelectCategory(1));
        }

        private void OnDisable()
        {
            if (titleInput != null) titleInput.onValueChanged.RemoveListener(OnTitleChanged);
            UnbindDurationSliders();
            foreach (var button in ruleMinus) Unbind(button);
            foreach (var button in rulePlus) Unbind(button);
            Unbind(applyButton);
            Unbind(revertButton);
            Unbind(openButton);
            Unbind(closeButton);
            Unbind(gameStartButton);
            Unbind(copyRoomCodeButton);
            Unbind(roomCodeHitButton);
            Unbind(maxPlayersMinusButton);
            Unbind(maxPlayersPlusButton);
            Unbind(destructionMinusButton);
            Unbind(destructionPlusButton);
            Unbind(mapPrevButton);
            Unbind(mapNextButton);
            UnbindMapSlots();
            Unbind(categoryPrevButton);
            Unbind(categoryNextButton);
            StopCopyFeedback(resetVisuals: true);
        }

        public void SetVisible(bool visible)
        {
            EnsureOverlay();
            EnsureLayout();
            EnsureCloseButton();
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(visible);
            }

            if (panel != null)
            {
                panel.SetActive(visible);
            }

            RefreshGameStartVisible();
            if (visible)
            {
                BringOverlayForward();
                RebuildSettingsScrollLayout();
                EnsureMapUiReady();

                RefreshCounters();
                RefreshCategory();
                RefreshMapSelection(scrollIntoView: false);
                RefreshTitleCounter();
                RefreshRoomCode();
            }
            else
            {
                StopCopyFeedback(resetVisuals: true);
            }
        }

        private void RefreshRoomCode()
        {
            if (roomCodeText == null && settingsContent != null)
            {
                CacheRoomCodeRefs(settingsContent);
            }

            if (roomCodeText != null)
            {
                roomCodeText.text = roomCode;
            }
        }

        public void RequestOpen() => OpenRequested?.Invoke();

        public void RequestClose()
        {
            if (HasUnappliedChanges)
            {
                SetUnappliedWarningVisible(true);
                return;
            }

            if (!editable || RoomSettings.IsValidTitle(ReadDraft().Title)) CloseRequested?.Invoke();
        }

        private void RequestApply()
        {
            if (!editable || !HasUnappliedChanges)
            {
                return;
            }

            if (!RoomSettings.IsValidTitle(ReadDraft().Title))
            {
                return;
            }

            ApplyRequested?.Invoke();
        }

        private void RequestStart()
        {
            if (HasUnappliedChanges)
            {
                SetUnappliedWarningVisible(true);
                return;
            }

            if (!editable || RoomSettings.IsValidTitle(ReadDraft().Title)) StartRequested?.Invoke();
        }

        public void SetUnappliedWarningVisible(bool visible)
        {
            if (applyWarning != null)
            {
                applyWarning.gameObject.SetActive(visible);
            }
        }

        private void RefreshApplyChrome()
        {
            var enabled = editable && HasUnappliedChanges;
            if (applyButton != null)
            {
                applyButton.gameObject.SetActive(editable);
                applyButton.interactable = enabled;
            }

            if (applyFill != null)
            {
                applyFill.color = enabled
                    ? PlaySettingsStyle.Palette.ApplyFill
                    : PlaySettingsStyle.Palette.ApplyOffFill;
            }

            if (applyLabel != null)
            {
                applyLabel.color = enabled
                    ? PlaySettingsStyle.Palette.ApplyOnLabel
                    : PlaySettingsStyle.Palette.ApplyOffLabel;
            }

            if (!enabled)
            {
                SetUnappliedWarningVisible(false);
            }

            RefreshRevertChrome();
        }

        private void RequestRevert()
        {
            if (!editable || !HasUnappliedChanges)
            {
                return;
            }

            SetDraft(appliedDraft);
        }

        private void RefreshRevertChrome()
        {
            if (revertButton == null)
            {
                return;
            }

            revertButton.gameObject.SetActive(editable);
            revertButton.interactable = editable && HasUnappliedChanges;
            if (!revertButton.interactable)
            {
                resetHovered = false;
            }

            PaintResetHover();
        }

        private void RequestCopyRoomCode()
        {
            if (copyCooldownActive || string.IsNullOrWhiteSpace(roomCode))
            {
                return;
            }

            CopyRoomCodeRequested?.Invoke();
            StartCopyFeedback();
        }

        private void StartCopyFeedback()
        {
            StopCopyFeedback(resetVisuals: false);
            copyCooldownActive = true;
            SetCopyControlsInteractable(false);
            if (copyFeedbackRoot != null)
            {
                copyFeedbackRoot.SetActive(true);
            }

            if (copyFeedbackShift != null)
            {
                copyFeedbackShift.SetActive(true);
            }

            if (copyIconImage != null)
            {
                copyIconImage.sprite = LoadCopyCheckIcon() ?? LoadCopyIcon();
            }

            if (isActiveAndEnabled)
            {
                copyFeedbackRoutine = StartCoroutine(CopyFeedbackRoutine());
            }
            else
            {
                StopCopyFeedback(resetVisuals: true);
            }
        }

        private IEnumerator CopyFeedbackRoutine()
        {
            yield return new WaitForSecondsRealtime(PlaySettingsStyle.Layout.CopyFeedbackDuration);
            copyFeedbackRoutine = null;
            StopCopyFeedback(resetVisuals: true);
        }

        private void StopCopyFeedback(bool resetVisuals)
        {
            if (copyFeedbackRoutine != null)
            {
                StopCoroutine(copyFeedbackRoutine);
                copyFeedbackRoutine = null;
            }

            copyCooldownActive = false;
            if (!resetVisuals)
            {
                return;
            }

            if (copyFeedbackRoot != null)
            {
                copyFeedbackRoot.SetActive(false);
            }

            if (copyFeedbackShift != null)
            {
                copyFeedbackShift.SetActive(false);
            }

            if (copyIconImage != null)
            {
                copyIconImage.sprite = LoadCopyIcon();
            }

            SetCopyControlsInteractable(true);
        }

        private void SetCopyControlsInteractable(bool interactable)
        {
            if (copyRoomCodeButton != null)
            {
                copyRoomCodeButton.interactable = interactable;
            }

            if (roomCodeHitButton != null)
            {
                roomCodeHitButton.interactable = interactable;
            }
        }

        public void SetEditable(bool value)
        {
            editable = value;
            if (titleInput != null) titleInput.interactable = value;
            foreach (var button in mapSlotButtons)
            {
                if (button != null)
                {
                    button.interactable = value;
                }
            }

            RefreshCounters();
            RefreshCategory();
            RefreshMapSelection(scrollIntoView: false);
            RefreshApplyChrome();
            RefreshGameStartVisible();
        }

        public void SetDraft(PlaySettingsDraft draft)
        {
            EnsureLayout();
            title = draft.Title;
            if (titleInput != null) titleInput.SetTextWithoutNotify(title);
            RefreshTitleCounter();
            roomCode = draft.RoomCode;
            passwordEnabled = draft.PasswordEnabled;
            password = draft.Password ?? string.Empty;
            matchRules = draft.MatchRules;
            selectedCategoryIndex = PlaySettingsCategoryCatalog.IndexOf(matchRules.CategoryId);
            if (selectedCategoryIndex < 0)
            {
                // The picker only lists ready categories. An id it cannot name
                // is still the room's rule — rewriting it here would drop
                // food/fruit (and anything else not on the chip yet) the
                // moment a host changed player cap.
                selectedCategoryIndex = PlaySettingsCategoryCatalog.DefaultIndex;
            }

            maxPlayers = Mathf.Clamp(
                draft.MaxPlayers,
                RoomSettings.MinPlayerCount,
                RoomSettings.MaxPlayerCount);
            destructionLimit = draft.DestructionLimit ==
                               PlaySettingsDraft.UnlimitedDestructionLimit
                ? PlaySettingsDraft.UnlimitedDestructionLimit
                : Mathf.Clamp(
                    draft.DestructionLimit,
                    PlaySettingsDraft.MinDestructionLimit,
                    PlaySettingsDraft.MaxDestructionLimit);
            selectedMapIndex = PlaySettingsMapCatalog.IndexOf(draft.MapId);
            if (selectedMapIndex < 0)
            {
                selectedMapIndex = PlaySettingsMapCatalog.DefaultIndex;
            }

            if (titleText != null)
            {
                titleText.text = title;
            }

            RefreshRoomCode();

            EnsureMapUiReady();
            SetEditable(editable);
            RefreshCounters();
            RefreshCategory();
            RefreshMapSelection(scrollIntoView: true);
            appliedDraft = ReadDraft();
            SetUnappliedWarningVisible(false);
            RefreshApplyChrome();
        }

        public PlaySettingsDraft ReadDraft()
        {
            var map = mapOptions[Mathf.Clamp(selectedMapIndex, 0, mapOptions.Count - 1)];
            return new PlaySettingsDraft(
                title,
                roomCode,
                passwordEnabled,
                password,
                maxPlayers,
                destructionLimit,
                map.Id,
                matchRules);
        }

        private void OnTitleChanged(string value)
        {
            if (!editable) return;
            title = value;
            RefreshTitleCounter();
            RefreshApplyChrome();
        }

        private void RefreshTitleCounter()
        {
            if (titleCounterText != null)
            {
                titleCounterText.text = $"{title.Length}/{RoomSettings.MaxTitleLength}";
            }
        }

        private void SelectCategory(int direction)
        {
            var options = PlaySettingsCategoryCatalog.All;
            if (!editable || options.Count <= 1)
            {
                return;
            }

            selectedCategoryIndex = (selectedCategoryIndex + direction + options.Count) % options.Count;
            NormalizeCategoryRules();
            RefreshCategory();
            RefreshApplyChrome();
        }

        private void NormalizeCategoryRules()
        {
            var categoryId = PlaySettingsCategoryCatalog.GetOption(selectedCategoryIndex).Id;
            if (MatchRuleSettings.TryCreateSeconds(
                    matchRules.HidingDurationSeconds,
                    matchRules.SearchingDurationSeconds,
                    matchRules.SprintMultiplier,
                    matchRules.StunHitCount,
                    categoryId,
                    out var updated,
                    out _))
            {
                matchRules = updated;
            }
        }

        private void RefreshCategory()
        {
            if (categoryText == null && settingsContent != null)
            {
                CacheMapAreaRefs(settingsContent);
            }

            if (categoryText != null)
            {
                categoryText.text = PlaySettingsCategoryCatalog.GetOption(selectedCategoryIndex).Label;
            }

            var hasMultipleOptions = PlaySettingsCategoryCatalog.All.Count > 1;
            if (categoryPrevButton != null)
            {
                categoryPrevButton.interactable = editable && hasMultipleOptions;
            }

            if (categoryNextButton != null)
            {
                categoryNextButton.interactable = editable && hasMultipleOptions;
            }
        }

        private void SetMaxPlayers(int value)
        {
            if (!editable) return;
            maxPlayers = Mathf.Clamp(
                value,
                RoomSettings.MinPlayerCount,
                RoomSettings.MaxPlayerCount);
            RefreshCounters();
            RefreshApplyChrome();
        }

        private void SetDestructionLimit(int value)
        {
            if (!editable) return;
            destructionLimit = destructionLimit ==
                               PlaySettingsDraft.UnlimitedDestructionLimit
                ? PlaySettingsDraft.MaxDestructionLimit
                : value > PlaySettingsDraft.MaxDestructionLimit
                    ? PlaySettingsDraft.UnlimitedDestructionLimit
                    : Mathf.Clamp(
                        value,
                        PlaySettingsDraft.MinDestructionLimit,
                        PlaySettingsDraft.MaxDestructionLimit);
            RefreshCounters();
            RefreshApplyChrome();
        }

        private void BindRuleControls()
        {
            for (var i = 0; i < ruleValues.Count; i++)
            {
                var index = i + 2;
                Bind(ruleMinus[i], () => ChangeRule(index, -1));
                Bind(rulePlus[i], () => ChangeRule(index, 1));
            }
        }

        private void BindDurationSliders()
        {
            if (hidingSlider != null)
            {
                hidingSlider.onValueChanged.RemoveListener(OnHidingSliderChanged);
                hidingSlider.onValueChanged.AddListener(OnHidingSliderChanged);
            }

            if (searchingSlider != null)
            {
                searchingSlider.onValueChanged.RemoveListener(OnSearchingSliderChanged);
                searchingSlider.onValueChanged.AddListener(OnSearchingSliderChanged);
            }
        }

        private void UnbindDurationSliders()
        {
            if (hidingSlider != null)
            {
                hidingSlider.onValueChanged.RemoveListener(OnHidingSliderChanged);
            }

            if (searchingSlider != null)
            {
                searchingSlider.onValueChanged.RemoveListener(OnSearchingSliderChanged);
            }
        }

        private void OnHidingSliderChanged(float value)
        {
            SetDurationSeconds(
                SnapDuration(
                    Mathf.RoundToInt(value),
                    MatchRuleSettings.MinHidingDurationSeconds,
                    MatchRuleSettings.MaxHidingDurationSeconds,
                    MatchRuleSettings.HidingDurationStepSeconds),
                matchRules.SearchingDurationSeconds);
        }

        private void OnSearchingSliderChanged(float value)
        {
            SetDurationSeconds(
                matchRules.HidingDurationSeconds,
                SnapDuration(
                    Mathf.RoundToInt(value),
                    MatchRuleSettings.MinSearchingDurationSeconds,
                    MatchRuleSettings.MaxSearchingDurationSeconds,
                    MatchRuleSettings.SearchingDurationStepSeconds));
        }

        private void SetDurationSeconds(int hidingSeconds, int searchingSeconds)
        {
            if (!editable)
            {
                RefreshRuleControls();
                return;
            }

            ApplyMatchRules(
                hidingSeconds,
                searchingSeconds,
                matchRules.SprintMultiplier,
                matchRules.StunHitCount);
        }

        private void ChangeRule(int index, int direction)
        {
            if (!editable) return;
            var hiding = matchRules.HidingDurationSeconds;
            var searching = matchRules.SearchingDurationSeconds;
            var speed = matchRules.SprintMultiplier;
            var hp = matchRules.StunHitCount;
            switch (index)
            {
                case 0:
                    hiding = SnapDuration(
                        hiding + (direction * MatchRuleSettings.HidingDurationStepSeconds),
                        MatchRuleSettings.MinHidingDurationSeconds,
                        MatchRuleSettings.MaxHidingDurationSeconds,
                        MatchRuleSettings.HidingDurationStepSeconds);
                    break;
                case 1:
                    searching = SnapDuration(
                        searching + (direction * MatchRuleSettings.SearchingDurationStepSeconds),
                        MatchRuleSettings.MinSearchingDurationSeconds,
                        MatchRuleSettings.MaxSearchingDurationSeconds,
                        MatchRuleSettings.SearchingDurationStepSeconds);
                    break;
                case 2:
                    var next = Array.IndexOf(SprintOptions, speed) + direction;
                    if (next < 0 || next >= SprintOptions.Length) return;
                    speed = SprintOptions[next]; break;
                case 3: hp += direction; break;
                default: return;
            }

            ApplyMatchRules(hiding, searching, speed, hp);
        }

        private void ApplyMatchRules(int hiding, int searching, float speed, int hp)
        {
            if (MatchRuleSettings.TryCreateSeconds(
                    hiding,
                    searching,
                    speed,
                    hp,
                    matchRules.CategoryId,
                    out var updated,
                    out _))
            {
                matchRules = updated;
            }

            RefreshRuleControls();
            RefreshApplyChrome();
        }

        private void RefreshRuleControls()
        {
            ShowDurationSlider(
                hidingSlider,
                hidingValue,
                matchRules.HidingDurationSeconds,
                FormatHidingDuration(matchRules.HidingDurationSeconds));
            ShowDurationSlider(
                searchingSlider,
                searchingValue,
                matchRules.SearchingDurationSeconds,
                FormatSearchingDuration(matchRules.SearchingDurationSeconds));

            if (ruleValues.Count == 0)
            {
                return;
            }

            var values = new[]
            {
                Array.IndexOf(SprintOptions, matchRules.SprintMultiplier),
                matchRules.StunHitCount
            };
            var min = new[] { 0, MatchRuleSettings.MinStunHitCount };
            var max = new[] { SprintOptions.Length - 1, MatchRuleSettings.MaxStunHitCount };
            var labels = new[]
            {
                matchRules.SprintMultiplier + "배",
                values[1] + "회"
            };
            for (var i = 0; i < ruleValues.Count; i++)
            {
                ruleValues[i].text = labels[i];
                ruleMinus[i].interactable = editable && values[i] > min[i];
                rulePlus[i].interactable = editable && values[i] < max[i];
            }
        }

        private void ShowDurationSlider(Slider slider, Text value, int seconds, string label)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(seconds);
                slider.interactable = editable;
            }

            if (value != null)
            {
                value.text = label;
            }
        }

        private static int SnapDuration(int value, int min, int max, int step)
        {
            var snapped = min + (Mathf.RoundToInt((value - min) / (float)step) * step);
            return Mathf.Clamp(snapped, min, max);
        }

        internal static string FormatHidingDuration(int seconds) => seconds + "초";

        internal static string FormatSearchingDuration(int seconds)
        {
            var minutes = seconds / 60;
            var remain = seconds % 60;
            return remain == 0 ? minutes + "분" : minutes + "분 " + remain + "초";
        }

        private void RefreshCounters()
        {
            RefreshRuleControls();
            if (maxPlayersText != null)
            {
                maxPlayersText.text = maxPlayers + "명";
            }

            if (destructionLimitText != null)
            {
                destructionLimitText.text = destructionLimit ==
                                            PlaySettingsDraft.UnlimitedDestructionLimit
                    ? "무한"
                    : destructionLimit + "회";
            }

            if (maxPlayersMinusButton != null)
            {
                maxPlayersMinusButton.interactable = editable && maxPlayers > RoomSettings.MinPlayerCount;
            }

            if (maxPlayersPlusButton != null)
            {
                maxPlayersPlusButton.interactable = editable && maxPlayers < RoomSettings.MaxPlayerCount;
            }

            if (destructionMinusButton != null)
            {
                destructionMinusButton.interactable =
                    editable && (destructionLimit == PlaySettingsDraft.UnlimitedDestructionLimit ||
                    destructionLimit > PlaySettingsDraft.MinDestructionLimit);
            }

            if (destructionPlusButton != null)
            {
                destructionPlusButton.interactable =
                    editable && destructionLimit != PlaySettingsDraft.UnlimitedDestructionLimit;
            }
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            Unbind(button);
            button.onClick.AddListener(action);
            boundActions[button] = action;
        }

        private void Unbind(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (boundActions.TryGetValue(button, out var action))
            {
                button.onClick.RemoveListener(action);
                boundActions.Remove(button);
            }
        }

        private void EnsureOverlay()
        {
            if (panel == null)
            {
                return;
            }

            var panelTransform = (RectTransform)panel.transform;
            if (TryAdoptOverlay(panelTransform))
            {
                var dedupeRoot = panelTransform.parent != null
                    ? panelTransform.parent.parent as RectTransform
                    : null;
                RemoveDuplicateOverlays(dedupeRoot, overlayRoot);
                var adoptedOverlay = (RectTransform)overlayRoot.transform;
                HideLegacyBackButtons(adoptedOverlay);
                if (dedupeRoot != null)
                {
                    EnsureGameStartLabel(dedupeRoot, adoptedOverlay);
                    BringOverlayForward();
                }

                return;
            }

            if (overlayRoot != null)
            {
                return;
            }

            var hudRoot = panelTransform.parent as RectTransform;
            if (hudRoot == null)
            {
                return;
            }

            var existingOverlay = hudRoot.Find("PlaySettingsOverlay") as RectTransform;
            if (existingOverlay != null)
            {
                overlayRoot = existingOverlay.gameObject;
                EnsureOverlayScrim(existingOverlay);
                panelTransform.SetParent(existingOverlay, false);
                RemoveDuplicateOverlays(hudRoot, overlayRoot);
                HideLegacyBackButtons(existingOverlay);
                EnsureGameStartLabel(hudRoot, existingOverlay);
                overlayRoot.SetActive(panel.activeSelf);
                RefreshGameStartVisible();
                return;
            }

            RemoveDuplicateOverlays(hudRoot, null);

            overlayRoot = new GameObject("PlaySettingsOverlay", typeof(RectTransform));
            var createdOverlay = (RectTransform)overlayRoot.transform;
            createdOverlay.SetParent(hudRoot, false);
            createdOverlay.SetSiblingIndex(panelTransform.GetSiblingIndex());
            StretchRect(createdOverlay);

            EnsureOverlayScrim(createdOverlay);

            panelTransform.SetParent(createdOverlay, false);
            HideLegacyBackButtons(createdOverlay);
            EnsureGameStartLabel(hudRoot, createdOverlay);

            overlayRoot.SetActive(panel.activeSelf);
            RefreshGameStartVisible();
        }

        private bool TryAdoptOverlay(RectTransform panelTransform)
        {
            var parent = panelTransform.parent as RectTransform;
            if (parent != null && parent.name == "PlaySettingsOverlay")
            {
                overlayRoot = parent.gameObject;
                EnsureOverlayScrim(parent);
                return true;
            }

            return false;
        }

        private static void RemoveLegacyBackdrop(RectTransform overlayTransform)
        {
            var legacy = overlayTransform.Find("Backdrop");
            if (legacy == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(legacy.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            }
        }

        private static void EnsureOverlayScrim(RectTransform overlayTransform)
        {
            RemoveLegacyBackdrop(overlayTransform);
            EnsureOverlayCanvas(overlayTransform);

            var dimTransform = overlayTransform.Find("Dim") as RectTransform;
            if (dimTransform == null)
            {
                var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
                dimTransform = dimGo.GetComponent<RectTransform>();
                dimTransform.SetParent(overlayTransform, false);
                StretchRect(dimTransform);
            }

            dimTransform.SetSiblingIndex(0);
            var dimImage = dimTransform.GetComponent<Image>();
            dimImage.color = PlaySettingsStyle.Overlay.Scrim;
            dimImage.raycastTarget = true;
        }

        private static void EnsureOverlayCanvas(RectTransform overlayTransform)
        {
            if (overlayTransform == null)
            {
                return;
            }

            var canvas = overlayTransform.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = overlayTransform.gameObject.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = PlaySettingsStyle.Overlay.SortingOrder;
            Game.Client.Common.HudScreenScale.Ensure(overlayTransform.gameObject);

            if (overlayTransform.GetComponent<GraphicRaycaster>() == null)
            {
                overlayTransform.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void BringOverlayForward()
        {
            if (overlayRoot == null)
            {
                return;
            }

            var overlayTransform = (RectTransform)overlayRoot.transform;
            EnsureOverlayCanvas(overlayTransform);
            overlayTransform.SetAsLastSibling();
            if (closeButton != null)
            {
                closeButton.transform.SetAsLastSibling();
            }

            if (gameStartButton != null)
            {
                gameStartButton.transform.SetAsLastSibling();
            }
        }

        private static void RemoveDuplicateOverlays(RectTransform hudRoot, GameObject keep)
        {
            if (hudRoot == null)
            {
                return;
            }

            for (var i = hudRoot.childCount - 1; i >= 0; i--)
            {
                var child = hudRoot.GetChild(i);
                if (child.name != "PlaySettingsOverlay" || child.gameObject == keep)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private void HideLegacyBackButtons(RectTransform overlay)
        {
            HideNamed(overlay, "BackButton");
            if (overlay != null && overlay.parent != null)
            {
                HideNamed(overlay.parent, "BackButton");
            }

            if (closeButton != null && closeButton.gameObject.name == "BackButton")
            {
                closeButton.gameObject.SetActive(false);
            }
        }

        private void EnsureCloseButton()
        {
            if (panel == null)
            {
                return;
            }

            var panelRect = (RectTransform)panel.transform;
            var rect = panelRect.Find("CloseButton") as RectTransform;
            if (rect == null && overlayRoot != null)
            {
                rect = overlayRoot.transform.Find("CloseButton") as RectTransform;
            }

            if (rect == null)
            {
                var go = new GameObject(
                    "CloseButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                rect = go.GetComponent<RectTransform>();
            }

            rect.SetParent(panelRect, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(
                -PlaySettingsStyle.Overlay.CloseOffset.x,
                -PlaySettingsStyle.Overlay.CloseOffset.y);
            rect.sizeDelta = new Vector2(
                PlaySettingsStyle.Overlay.CloseSize, PlaySettingsStyle.Overlay.CloseSize);

            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = SettingsStyle.LoadCloseIcon();
            image.color = PlaySettingsStyle.Palette.Text;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = true;

            closeButton = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = image;
            closeButton.transition = Selectable.Transition.None;
            rect.SetAsLastSibling();
        }

        private static void HideNamed(Transform parent, string name)
        {
            if (parent == null)
            {
                return;
            }

            var child = parent.Find(name);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void EnsureGameStartLabel(RectTransform hudRoot, RectTransform overlay)
        {
            if (overlay == null)
            {
                return;
            }

            RemoveLegacyGameStartLabel(overlay, hudRoot);

            if (gameStartButton == null)
            {
                var existing = overlay.Find("GameStartButton") ?? hudRoot?.Find("GameStartButton");
                if (existing != null)
                {
                    gameStartButton = existing.GetComponent<Button>();
                    gameStartLabel = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            if (gameStartButton == null)
            {
                CreateGameStartButton(overlay);
            }
            else
            {
                gameStartButton.transform.SetParent(overlay, false);
                StyleGameStartButton((RectTransform)gameStartButton.transform);
            }

            Bind(gameStartButton, RequestStart);
        }

        private static void RemoveLegacyGameStartLabel(RectTransform overlay, RectTransform hudRoot)
        {
            RemoveNamedChild(overlay, "GameStartLabel");
            if (hudRoot != null && hudRoot != overlay)
            {
                RemoveNamedChild(hudRoot, "GameStartLabel");
            }
        }

        private static void RemoveNamedChild(Transform root, string name)
        {
            if (root == null)
            {
                return;
            }

            var child = root.Find(name);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private void CreateGameStartButton(RectTransform overlay)
        {
            var plate = new GameObject("GameStartButton", typeof(RectTransform), typeof(Image));
            var rect = plate.GetComponent<RectTransform>();
            rect.SetParent(overlay, false);

            var fill = plate.GetComponent<Image>();
            fill.color = Color.white;
            fill.raycastTarget = true;
            plate.AddComponent<UiLinearGradient>();

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            StretchRect(labelRect);
            gameStartLabel = labelGo.AddComponent<TextMeshProUGUI>();
            gameStartLabel.raycastTarget = false;

            gameStartButton = plate.AddComponent<Button>();
            gameStartButton.targetGraphic = fill;
            gameStartButton.transition = Selectable.Transition.None;
            plate.AddComponent<HomeLabelPop>();

            StyleGameStartButton(rect);
            plate.SetActive(false);
        }

        private void StyleGameStartButton(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = PlaySettingsStyle.Overlay.GameStartPosition;
            rect.sizeDelta = PlaySettingsStyle.Overlay.GameStartSize;

            var fill = rect.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = HomeUiFonts.Rounded(PlaySettingsStyle.Overlay.GameStartRadius);
                fill.type = Image.Type.Sliced;
                fill.color = Color.white;
            }

            var gradient = rect.GetComponent<UiLinearGradient>();
            if (gradient != null)
            {
                gradient.Bind(
                    SettingsStyle.Palette.LeaveGameStart,
                    SettingsStyle.Palette.LeaveGameEnd,
                    alongVertical: false);
            }

            if (gameStartLabel == null)
            {
                gameStartLabel = rect.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (gameStartLabel != null)
            {
                var font = HomeUiFonts.Apply();
                if (font != null)
                {
                    gameStartLabel.font = font;
                    if (font.material != null)
                    {
                        gameStartLabel.fontSharedMaterial = font.material;
                    }
                }

                gameStartLabel.fontSize = PlaySettingsStyle.FontSize.GameStart;
                gameStartLabel.text = "게임 시작";
                gameStartLabel.alignment = TextAlignmentOptions.Center;
                gameStartLabel.color = SettingsStyle.Palette.ApplyOnLabel;
                gameStartLabel.textWrappingMode = TextWrappingModes.NoWrap;
                gameStartLabel.overflowMode = TextOverflowModes.Overflow;
            }

            if (gameStartButton != null)
            {
                gameStartButton.targetGraphic = fill;
                gameStartButton.transition = Selectable.Transition.None;
            }

            var pop = rect.GetComponent<HomeLabelPop>() ?? rect.gameObject.AddComponent<HomeLabelPop>();
            pop.Bind(
                rect,
                PlaySettingsStyle.Overlay.GameStartHoverScale,
                PlaySettingsStyle.Overlay.GameStartHoverSeconds);
        }

        private void RefreshGameStartVisible()
        {
            var shown = editable &&
                        ((overlayRoot != null && overlayRoot.activeSelf) ||
                         (panel != null && panel.activeSelf));
            SetGameStartLabelVisible(shown);
        }

        private void SetGameStartLabelVisible(bool visible)
        {
            var root = gameStartButton != null ? gameStartButton.gameObject
                : gameStartLabel != null ? gameStartLabel.gameObject : null;
            if (root == null)
            {
                return;
            }

            root.SetActive(visible);
            if (visible)
            {
                root.transform.SetAsLastSibling();
            }
        }

        private static void StretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
