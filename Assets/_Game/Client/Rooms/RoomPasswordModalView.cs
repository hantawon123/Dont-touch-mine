using System;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Rooms;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Modal that asks for the password of a locked room before entering it.
    /// </summary>
    /// <remarks>
    /// Owns only its own widgets: the confirm button reports the typed password
    /// and never enters a room itself. Whoever answers <see cref="SubmitRequested"/>
    /// decides the outcome and reports it back through
    /// <see cref="ShowFailure"/>, so the modal never judges a password.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoomPasswordModalView : MonoBehaviour
    {
        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private TMP_Text roomTitleText;

        [SerializeField]
        private TMP_InputField passwordInputField;

        [SerializeField]
        private TMP_Text errorText;

        [SerializeField]
        private Button confirmButton;

        /// <summary>Digits only, matching the length the create modal allows.</summary>
        private const int PasswordMaxLength = 10;

        private bool isBusy;
        private UiLocale chromeLocale;

        public event Action CloseRequested;
        public event Action<string> SubmitRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            HomeUiFonts.ApplyTmp(transform);
            SettingsStyle.ApplyCloseButton(closeButton);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            passwordInputField.onValueChanged.AddListener(OnPasswordTextChanged);
            passwordInputField.onSubmit.AddListener(OnPasswordSubmitted);
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);

            // Pin masks the field and restricts it to digits, so letters and
            // spaces cannot be typed or pasted in the first place.
            passwordInputField.contentType = TMP_InputField.ContentType.Pin;
            passwordInputField.characterLimit = PasswordMaxLength;
        }

        private void Start()
        {
            RefreshConfirmButton();
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            passwordInputField.onValueChanged.RemoveListener(OnPasswordTextChanged);
            passwordInputField.onSubmit.RemoveListener(OnPasswordSubmitted);
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
        }

        public void Open(string roomTitle)
        {
            roomTitleText.text = roomTitle ?? string.Empty;
            passwordInputField.SetTextWithoutNotify(string.Empty);
            isBusy = false;

            ShowFailure(RoomEntryFailure.None);
            RefreshConfirmButton();

            gameObject.SetActive(true);

            // Puts the caret in the only field there is, so the password can be
            // typed without a click.
            passwordInputField.ActivateInputField();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void SetBusy(bool busy)
        {
            isBusy = busy;
            RefreshConfirmButton();
        }

        public void ShowFailure(RoomEntryFailure failure)
        {
            var message = DescribeFailure(failure);
            errorText.text = message;
            errorText.gameObject.SetActive(message.Length > 0);
        }

        /// <summary>
        /// Turns a failure into the line the player is shown. Anything the
        /// player cannot act on collapses into one neutral message, so a
        /// networking detail never reaches the screen.
        /// </summary>
        public void ShowChrome(UiLocale locale) => chromeLocale = locale;

        private string DescribeFailure(RoomEntryFailure failure)
        {
            var language = chromeLocale != null ? chromeLocale.LanguageCode : "ko";
            switch (failure)
            {
                case RoomEntryFailure.None:
                    return string.Empty;
                case RoomEntryFailure.WrongPassword:
                    return UiTextCatalog.Shipped.Get(UiText.Rooms.WrongPassword, language);
                case RoomEntryFailure.Full:
                    return UiTextCatalog.Shipped.Get(UiText.Rooms.FullModal, language);
                case RoomEntryFailure.Closed:
                    return UiTextCatalog.Shipped.Get(UiText.Rooms.ClosedModal, language);
                case RoomEntryFailure.NotFound:
                    return UiTextCatalog.Shipped.Get(UiText.Rooms.NotFoundModal, language);
                case RoomEntryFailure.AlreadyInRoom:
                    return UiTextCatalog.Shipped.Get(UiText.Rooms.AlreadyInModal, language);
                case RoomEntryFailure.ConnectionFailed:
                    return UiTextCatalog.Shipped.Get(UiText.Rooms.ConnectFailed, language);
                default:
                    return UiTextCatalog.Shipped.Get(UiText.Rooms.JoinFailed, language);
            }
        }

        private void RefreshConfirmButton()
        {
            confirmButton.interactable =
                !isBusy && !string.IsNullOrEmpty(passwordInputField.text);
        }

        private void OnCloseButtonClicked() => CloseRequested?.Invoke();

        private void OnPasswordTextChanged(string text)
        {
            // The previous answer no longer describes what is in the field.
            ShowFailure(RoomEntryFailure.None);
            RefreshConfirmButton();
        }

        private void OnPasswordSubmitted(string text) => TrySubmit();

        private void OnConfirmButtonClicked() => TrySubmit();

        private void TrySubmit()
        {
            if (!confirmButton.interactable)
            {
                return;
            }

            SubmitRequested?.Invoke(passwordInputField.text);
        }
    }
}
