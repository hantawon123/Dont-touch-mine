using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Modal the host fills in before opening a room.
    /// </summary>
    /// <remarks>
    /// Owns only its own widgets: the create button reports a
    /// <see cref="RoomCreateRequest"/> and never opens a room itself.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoomCreateModalView : MonoBehaviour, IRoomCreateModalView
    {
        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_InputField titleInputField;

        [SerializeField]
        private Button lockOnButton;

        [SerializeField]
        private Button lockOffButton;

        [SerializeField]
        private Image lockOnBackground;

        [SerializeField]
        private Image lockOffBackground;

        [SerializeField]
        private GameObject passwordGroup;

        [SerializeField]
        private TMP_InputField passwordInputField;

        [SerializeField]
        private Button decreasePlayerCountButton;

        [SerializeField]
        private Button increasePlayerCountButton;

        [SerializeField]
        private TMP_Text playerCountText;

        [SerializeField]
        private RoomMapOptionView[] mapSlots;

        [SerializeField]
        private Button mapNextButton;

        [SerializeField]
        private Button createButton;

        [SerializeField]
        private Color selectedSegmentColor = Color.white;

        [SerializeField]
        private Color unselectedSegmentColor = new Color(1f, 1f, 1f, 0f);

        /// <summary>Any UTF-8 text, whitespace included; duplicates are fine.</summary>
        private const int TitleMaxLength = 20;

        /// <summary>Digits only, so one character is the shortest valid password.</summary>
        private const int PasswordMaxLength = 10;

        private readonly List<string> mapIds = new List<string>();

        private bool isLocked;
        private bool isBusy;
        private int maxPlayers = RoomSettings.MaxPlayerCount;
        private int mapPageIndex;
        private string selectedMapId;

        public event Action CloseRequested;
        public event Action<RoomCreateRequest> CreateRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            titleInputField.onValueChanged.AddListener(OnTitleTextChanged);
            lockOnButton.onClick.AddListener(OnLockOnButtonClicked);
            lockOffButton.onClick.AddListener(OnLockOffButtonClicked);
            passwordInputField.onValueChanged.AddListener(OnPasswordTextChanged);
            decreasePlayerCountButton.onClick.AddListener(OnDecreasePlayerCountButtonClicked);
            increasePlayerCountButton.onClick.AddListener(OnIncreasePlayerCountButtonClicked);
            mapNextButton.onClick.AddListener(OnMapNextButtonClicked);
            createButton.onClick.AddListener(OnCreateButtonClicked);

            foreach (var slot in mapSlots)
            {
                slot.Selected += OnMapOptionSelected;
            }

            titleInputField.characterLimit = TitleMaxLength;

            // Pin masks the field and restricts it to digits, so letters and
            // spaces cannot be typed or pasted in the first place.
            passwordInputField.contentType = TMP_InputField.ContentType.Pin;
            passwordInputField.characterLimit = PasswordMaxLength;
        }

        private void Start()
        {
            RefreshLockState();
            RefreshPlayerCount();
            RefreshMapPage();
            RefreshCreateButton();
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            titleInputField.onValueChanged.RemoveListener(OnTitleTextChanged);
            lockOnButton.onClick.RemoveListener(OnLockOnButtonClicked);
            lockOffButton.onClick.RemoveListener(OnLockOffButtonClicked);
            passwordInputField.onValueChanged.RemoveListener(OnPasswordTextChanged);
            decreasePlayerCountButton.onClick.RemoveListener(OnDecreasePlayerCountButtonClicked);
            increasePlayerCountButton.onClick.RemoveListener(OnIncreasePlayerCountButtonClicked);
            mapNextButton.onClick.RemoveListener(OnMapNextButtonClicked);
            createButton.onClick.RemoveListener(OnCreateButtonClicked);

            foreach (var slot in mapSlots)
            {
                if (slot != null)
                {
                    slot.Selected -= OnMapOptionSelected;
                }
            }
        }

        public void Open()
        {
            titleInputField.SetTextWithoutNotify(string.Empty);
            passwordInputField.SetTextWithoutNotify(string.Empty);
            isLocked = false;
            maxPlayers = RoomSettings.MaxPlayerCount;
            mapPageIndex = 0;
            selectedMapId = mapIds.Count > 0 ? mapIds[0] : null;

            RefreshLockState();
            RefreshPlayerCount();
            RefreshMapPage();
            RefreshCreateButton();

            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void SetMapOptions(IReadOnlyList<string> availableMapIds)
        {
            if (availableMapIds == null)
            {
                throw new ArgumentNullException(nameof(availableMapIds));
            }

            mapIds.Clear();

            for (var index = 0; index < availableMapIds.Count; index++)
            {
                mapIds.Add(availableMapIds[index]);
            }

            mapPageIndex = 0;
            selectedMapId = mapIds.Count > 0 ? mapIds[0] : null;

            RefreshMapPage();
            RefreshCreateButton();
        }

        public void SetBusy(bool busy)
        {
            isBusy = busy;
            RefreshCreateButton();
        }

        private RoomCreateRequest BuildRequest() =>
            new RoomCreateRequest(
                titleInputField.text,
                isLocked,
                passwordInputField.text,
                maxPlayers,
                selectedMapId);

        private void RefreshLockState()
        {
            lockOnBackground.color = isLocked ? selectedSegmentColor : unselectedSegmentColor;
            lockOffBackground.color = isLocked ? unselectedSegmentColor : selectedSegmentColor;
            passwordGroup.SetActive(isLocked);

            if (!isLocked)
            {
                passwordInputField.SetTextWithoutNotify(string.Empty);
            }
        }

        private void RefreshPlayerCount()
        {
            playerCountText.text = maxPlayers.ToString();
            decreasePlayerCountButton.interactable = maxPlayers > RoomSettings.MinPlayerCount;
            increasePlayerCountButton.interactable = maxPlayers < RoomSettings.MaxPlayerCount;
        }

        private void RefreshMapPage()
        {
            // Nothing to page to until the maps outnumber the slots.
            mapNextButton.gameObject.SetActive(mapIds.Count > mapSlots.Length);

            for (var slotIndex = 0; slotIndex < mapSlots.Length; slotIndex++)
            {
                var mapIndex = mapPageIndex * mapSlots.Length + slotIndex;
                var slot = mapSlots[slotIndex];
                var hasMap = mapIndex < mapIds.Count;
                slot.gameObject.SetActive(hasMap);

                if (hasMap)
                {
                    var mapId = mapIds[mapIndex];
                    slot.Bind(mapId, string.Equals(mapId, selectedMapId, StringComparison.Ordinal));
                }
            }
        }

        private void RefreshCreateButton()
        {
            var isRequestValid = BuildRequest().TryCreateSettings(
                RoomSettings.MaxPlayerCount,
                out _,
                out _);

            createButton.interactable = isRequestValid && !isBusy;
        }

        private void OnCloseButtonClicked() => CloseRequested?.Invoke();

        private void OnTitleTextChanged(string text) => RefreshCreateButton();

        private void OnPasswordTextChanged(string text) => RefreshCreateButton();

        private void OnLockOnButtonClicked() => SetLocked(true);

        private void OnLockOffButtonClicked() => SetLocked(false);

        private void SetLocked(bool locked)
        {
            isLocked = locked;
            RefreshLockState();
            RefreshCreateButton();
        }

        private void OnDecreasePlayerCountButtonClicked() => SetMaxPlayers(maxPlayers - 1);

        private void OnIncreasePlayerCountButtonClicked() => SetMaxPlayers(maxPlayers + 1);

        private void SetMaxPlayers(int count)
        {
            maxPlayers = Mathf.Clamp(
                count,
                RoomSettings.MinPlayerCount,
                RoomSettings.MaxPlayerCount);

            RefreshPlayerCount();
            RefreshCreateButton();
        }

        private void OnMapNextButtonClicked()
        {
            var pageCount = (mapIds.Count + mapSlots.Length - 1) / mapSlots.Length;

            if (pageCount <= 1)
            {
                return;
            }

            mapPageIndex = (mapPageIndex + 1) % pageCount;
            RefreshMapPage();
        }

        private void OnMapOptionSelected(string mapId)
        {
            selectedMapId = mapId;
            RefreshMapPage();
            RefreshCreateButton();
        }

        private void OnCreateButtonClicked() => CreateRequested?.Invoke(BuildRequest());
    }
}
