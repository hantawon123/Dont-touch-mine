using System;
using System.Collections.Generic;
using Game.Core.Lobby;
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

        void SetVisible(bool visible);
        void SetDraft(PlaySettingsDraft draft);
        PlaySettingsDraft ReadDraft();
    }

    public sealed class PlaySettingsView : MonoBehaviour, IPlaySettingsView
    {
        private const int FixedMaxPlayers = 6;
        private const int FixedDestructionLimit = 5;
        private const float MapSlotSize = 90f;
        private const float MapSlotSpacing = 12f;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Button copyRoomCodeButton;

        [SerializeField]
        private Button inviteButton;

        [SerializeField]
        private Button copyPasswordButton;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Text titleText;

        [SerializeField]
        private Text roomCodeText;

        [SerializeField]
        private Text passwordMaskedText;

        [SerializeField]
        private Text maxPlayersText;

        [SerializeField]
        private Button maxPlayersMinusButton;

        [SerializeField]
        private Button maxPlayersPlusButton;

        [SerializeField]
        private Text destructionLimitText;

        [SerializeField]
        private Button destructionMinusButton;

        [SerializeField]
        private Button destructionPlusButton;

        [SerializeField]
        private Text mapNameText;

        [SerializeField]
        private Button mapPrevButton;

        [SerializeField]
        private Button mapNextButton;

        [SerializeField]
        private ScrollRect mapScroll;

        [SerializeField]
        private RectTransform mapContent;

        private string title = string.Empty;
        private string roomCode = string.Empty;
        private bool passwordEnabled;
        private string password = string.Empty;
        private int selectedMapIndex;
        private IReadOnlyList<LobbyMapOption> maps = LobbyMapCatalog.SampleMaps;
        private readonly List<Image> mapSlotImages = new();
        private readonly List<Button> mapSlotButtons = new();
        private readonly Dictionary<Button, UnityEngine.Events.UnityAction> boundActions = new();

        public event Action OpenRequested;
        public event Action CloseRequested;
        public event Action CopyRoomCodeRequested;
        public event Action InviteRequested;
        public event Action CopyPasswordRequested;

        private void OnEnable()
        {
            Bind(openButton, () => OpenRequested?.Invoke());
            Bind(closeButton, () => CloseRequested?.Invoke());
            Bind(copyRoomCodeButton, () => CopyRoomCodeRequested?.Invoke());
            Bind(inviteButton, () => InviteRequested?.Invoke());
            Bind(copyPasswordButton, () => CopyPasswordRequested?.Invoke());
            Bind(maxPlayersMinusButton, RefreshFixedCounters);
            Bind(maxPlayersPlusButton, RefreshFixedCounters);
            Bind(destructionMinusButton, RefreshFixedCounters);
            Bind(destructionPlusButton, RefreshFixedCounters);
            Bind(mapPrevButton, () => ScrollMaps(-1));
            Bind(mapNextButton, () => ScrollMaps(1));
            BindMapSlots();
        }

        private void OnDisable()
        {
            Unbind(openButton);
            Unbind(closeButton);
            Unbind(copyRoomCodeButton);
            Unbind(inviteButton);
            Unbind(copyPasswordButton);
            Unbind(maxPlayersMinusButton);
            Unbind(maxPlayersPlusButton);
            Unbind(destructionMinusButton);
            Unbind(destructionPlusButton);
            Unbind(mapPrevButton);
            Unbind(mapNextButton);
            UnbindMapSlots();
        }

        public void SetVisible(bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        public void SetDraft(PlaySettingsDraft draft)
        {
            title = draft.Title;
            roomCode = draft.RoomCode;
            passwordEnabled = draft.PasswordEnabled;
            password = draft.Password ?? string.Empty;
            selectedMapIndex = LobbyMapCatalog.IndexOf(draft.MapId);

            if (titleText != null)
            {
                titleText.text = title;
            }

            if (roomCodeText != null)
            {
                roomCodeText.text = roomCode;
            }

            EnsureMapSlotsBuilt();
            RefreshPasswordMask();
            RefreshFixedCounters();
            RefreshMapSelection(scrollIntoView: true);
        }

        public PlaySettingsDraft ReadDraft()
        {
            var map = maps[Mathf.Clamp(selectedMapIndex, 0, maps.Count - 1)];
            return new PlaySettingsDraft(
                title,
                roomCode,
                passwordEnabled,
                password,
                FixedMaxPlayers,
                FixedDestructionLimit,
                map.Id);
        }

        private void ScrollMaps(int direction)
        {
            if (mapScroll == null || mapContent == null)
            {
                return;
            }

            var step = (MapSlotSize + MapSlotSpacing) / Mathf.Max(1f, mapContent.rect.width);
            mapScroll.horizontalNormalizedPosition = Mathf.Clamp01(
                mapScroll.horizontalNormalizedPosition + (direction * step));
        }

        private void SelectMap(int index)
        {
            if (index < 0 || index >= maps.Count)
            {
                return;
            }

            selectedMapIndex = index;
            RefreshMapSelection(scrollIntoView: true);
        }

        private void RefreshMapSelection(bool scrollIntoView)
        {
            if (maps.Count == 0)
            {
                return;
            }

            selectedMapIndex = Mathf.Clamp(selectedMapIndex, 0, maps.Count - 1);
            var selected = maps[selectedMapIndex];

            if (mapNameText != null)
            {
                mapNameText.text = selected.DisplayName;
            }

            for (var i = 0; i < mapSlotImages.Count; i++)
            {
                var image = mapSlotImages[i];
                if (image == null)
                {
                    continue;
                }

                var selectedSlot = i == selectedMapIndex;
                image.color = selectedSlot
                    ? new Color(0.92f, 0.92f, 0.95f, 1f)
                    : new Color(0.45f, 0.45f, 0.5f, 1f);

                var outline = image.transform.Find("Selection");
                if (outline != null)
                {
                    outline.gameObject.SetActive(selectedSlot);
                }
            }

            if (scrollIntoView)
            {
                ScrollSelectedIntoView();
            }
        }

        private void ScrollSelectedIntoView()
        {
            if (mapScroll == null || mapContent == null || maps.Count <= 1)
            {
                return;
            }

            var viewport = mapScroll.viewport != null
                ? mapScroll.viewport.rect.width
                : mapScroll.GetComponent<RectTransform>().rect.width;
            var contentWidth = mapContent.rect.width;
            if (contentWidth <= viewport)
            {
                mapScroll.horizontalNormalizedPosition = 0f;
                return;
            }

            var slotCenter = selectedMapIndex * (MapSlotSize + MapSlotSpacing) + (MapSlotSize * 0.5f);
            var target = (slotCenter - (viewport * 0.5f)) / (contentWidth - viewport);
            mapScroll.horizontalNormalizedPosition = Mathf.Clamp01(target);
        }

        private void EnsureMapSlotsBuilt()
        {
            if (mapContent == null)
            {
                return;
            }

            if (mapSlotButtons.Count == maps.Count && mapSlotImages.Count == maps.Count)
            {
                return;
            }

            UnbindMapSlots();
            for (var i = mapContent.childCount - 1; i >= 0; i--)
            {
                var child = mapContent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            mapSlotImages.Clear();
            mapSlotButtons.Clear();

            var width = (maps.Count * MapSlotSize) + (Mathf.Max(0, maps.Count - 1) * MapSlotSpacing);
            mapContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            mapContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, MapSlotSize);

            for (var i = 0; i < maps.Count; i++)
            {
                var slot = CreateMapSlot(mapContent, i);
                mapSlotImages.Add(slot.GetComponent<Image>());
                mapSlotButtons.Add(slot.GetComponent<Button>());
            }

            BindMapSlots();
        }

        private static RectTransform CreateMapSlot(RectTransform parent, int index)
        {
            var go = new GameObject($"MapSlot{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(MapSlotSize, MapSlotSize);
            rect.anchoredPosition = new Vector2(index * (MapSlotSize + MapSlotSpacing), 0f);
            go.GetComponent<Image>().color = new Color(0.45f, 0.45f, 0.5f, 1f);

            var selectionGo = new GameObject("Selection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            selectionGo.transform.SetParent(go.transform, false);
            var selectionRect = selectionGo.GetComponent<RectTransform>();
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.offsetMin = new Vector2(-4f, -4f);
            selectionRect.offsetMax = new Vector2(4f, 4f);
            selectionRect.SetAsFirstSibling();
            var selectionImage = selectionGo.GetComponent<Image>();
            selectionImage.color = new Color(0.95f, 0.95f, 1f, 1f);
            selectionImage.raycastTarget = false;
            selectionGo.SetActive(false);
            return rect;
        }

        private void BindMapSlots()
        {
            for (var i = 0; i < mapSlotButtons.Count; i++)
            {
                var index = i;
                Bind(mapSlotButtons[i], () => SelectMap(index));
            }
        }

        private void UnbindMapSlots()
        {
            for (var i = 0; i < mapSlotButtons.Count; i++)
            {
                Unbind(mapSlotButtons[i]);
            }
        }

        private void RefreshPasswordMask()
        {
            if (passwordMaskedText == null)
            {
                return;
            }

            if (!passwordEnabled || string.IsNullOrEmpty(password))
            {
                passwordMaskedText.text = "없음";
                return;
            }

            passwordMaskedText.text = new string('*', Mathf.Clamp(password.Length, 4, 12));
        }

        private void RefreshFixedCounters()
        {
            if (maxPlayersText != null)
            {
                maxPlayersText.text = FixedMaxPlayers.ToString();
            }

            if (destructionLimitText != null)
            {
                destructionLimitText.text = FixedDestructionLimit.ToString();
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
    }
}
