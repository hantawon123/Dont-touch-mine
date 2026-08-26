using System;
using System.Text;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Modal for entering a room by the code its host shared.
    /// </summary>
    /// <remarks>
    /// The six boxes only draw characters. One off-screen input field holds the
    /// actual text, so typing, backspace, and paste all behave the way the
    /// operating system already makes them behave, and a pasted code lands in
    /// every box at once.
    /// <para>
    /// Owns no opinion about the room: it reports a completed code and is told
    /// back which of <see cref="ShowOpenRoom"/>, <see cref="ShowLockedRoom"/>,
    /// or <see cref="ShowFailure"/> to draw.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoomCodeModalView : MonoBehaviour, IRoomCodeModalView
    {
        /// <summary>Resized per state, because a code alone needs far less room.</summary>
        [SerializeField]
        private RectTransform panel;

        [SerializeField]
        private Vector2 codeEntrySize = new Vector2(560f, 270f);

        [SerializeField]
        private Vector2 openRoomSize = new Vector2(560f, 300f);

        [SerializeField]
        private Vector2 lockedRoomSize = new Vector2(560f, 370f);

        /// <summary>
        /// Where the message sits, measured down from the top of the panel. It
        /// follows whatever it is explaining rather than the panel edge, so it
        /// stays under the boxes while a code is typed and under the password
        /// field once one is asked for.
        /// </summary>
        [SerializeField]
        private float codeEntryErrorY = -200f;

        [SerializeField]
        private float lockedRoomErrorY = -236f;

        [SerializeField]
        private Button closeButton;

        /// <summary>Off-screen; holds the text the boxes draw.</summary>
        [SerializeField]
        private TMP_InputField codeInputField;

        [SerializeField]
        private GameObject codeBoxGroup;

        [SerializeField]
        private TMP_Text[] codeBoxTexts;

        /// <summary>
        /// Optional. The code field already covers the boxes, so clicking them
        /// lands on it; this is only for a layout that puts something else on
        /// top.
        /// </summary>
        [SerializeField]
        private Button codeBoxButton;

        [SerializeField]
        private GameObject shortenedCodeGroup;

        /// <summary>Clicked to go back to the boxes; shows the accepted code.</summary>
        [SerializeField]
        private Button shortenedCodeButton;

        [SerializeField]
        private TMP_Text shortenedCodeText;

        [SerializeField]
        private GameObject passwordGroup;

        [SerializeField]
        private TMP_InputField passwordInputField;

        [SerializeField]
        private TMP_Text errorText;

        [SerializeField]
        private GameObject enterButtonGroup;

        [SerializeField]
        private Button enterButton;

        /// <summary>Digits only, matching what the other modals allow.</summary>
        private const int PasswordMaxLength = 10;

        private readonly StringBuilder filtered = new StringBuilder(RoomCodeFormat.CodeLength);

        private bool isBusy;
        private bool isLocked;
        private bool hasRoom;

        public event Action CloseRequested;
        public event Action<string> CodeCompleted;
        public event Action CodeCleared;
        public event Action CodeEditRequested;
        public event Action<string, string> EnterRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            codeInputField.onValueChanged.AddListener(OnCodeTextChanged);

            if (codeBoxButton != null)
            {
                codeBoxButton.onClick.AddListener(FocusCodeField);
            }
            shortenedCodeButton.onClick.AddListener(OnShortenedCodeClicked);
            passwordInputField.onValueChanged.AddListener(OnPasswordTextChanged);
            passwordInputField.onSubmit.AddListener(OnPasswordSubmitted);
            enterButton.onClick.AddListener(OnEnterButtonClicked);

            // Cloned from the password field, which is a masked digit pad.
            // A code is letters as well, and must be readable while typed.
            codeInputField.contentType = TMP_InputField.ContentType.Custom;
            codeInputField.inputType = TMP_InputField.InputType.Standard;
            codeInputField.characterValidation = TMP_InputField.CharacterValidation.None;
            codeInputField.lineType = TMP_InputField.LineType.SingleLine;

            // Refocusing must not swallow the whole code: the field is
            // reactivated while the player is still editing it.
            codeInputField.onFocusSelectAll = false;
            codeInputField.characterLimit = RoomCodeFormat.CodeLength;

            // Pin masks the field and restricts it to digits, so letters and
            // spaces cannot be typed or pasted in the first place.
            passwordInputField.contentType = TMP_InputField.ContentType.Pin;
            passwordInputField.characterLimit = PasswordMaxLength;
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            codeInputField.onValueChanged.RemoveListener(OnCodeTextChanged);

            if (codeBoxButton != null)
            {
                codeBoxButton.onClick.RemoveListener(FocusCodeField);
            }
            shortenedCodeButton.onClick.RemoveListener(OnShortenedCodeClicked);
            passwordInputField.onValueChanged.RemoveListener(OnPasswordTextChanged);
            passwordInputField.onSubmit.RemoveListener(OnPasswordSubmitted);
            enterButton.onClick.RemoveListener(OnEnterButtonClicked);
        }

        public void Open()
        {
            isBusy = false;
            codeInputField.SetTextWithoutNotify(string.Empty);
            passwordInputField.SetTextWithoutNotify(string.Empty);

            gameObject.SetActive(true);
            ShowCodeEntry();
            FocusCodeField();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void SetBusy(bool busy)
        {
            isBusy = busy;
            RefreshEnterButton();
        }

        public void ShowCodeEntry()
        {
            hasRoom = false;
            isLocked = false;

            codeBoxGroup.SetActive(true);
            shortenedCodeGroup.SetActive(false);
            passwordGroup.SetActive(false);
            enterButtonGroup.SetActive(false);
            passwordInputField.SetTextWithoutNotify(string.Empty);

            ShowFailure(RoomEntryFailure.None);
            RefreshCodeBoxes();
            RefreshEnterButton();
            ResizePanel(codeEntrySize);
            PlaceError(codeEntryErrorY);

            // No focus call here. This runs on the keystroke that takes the
            // code below full length, and moving the caret mid-edit is what
            // made the first character impossible to erase.
        }

        public void ShowOpenRoom()
        {
            hasRoom = true;
            isLocked = false;

            // The boxes stay: an open room needs nothing more than the code,
            // so there is no reason to take editing away.
            codeBoxGroup.SetActive(true);
            shortenedCodeGroup.SetActive(false);
            passwordGroup.SetActive(false);
            enterButtonGroup.SetActive(true);

            ShowFailure(RoomEntryFailure.None);
            RefreshEnterButton();
            ResizePanel(openRoomSize);
            PlaceError(codeEntryErrorY);
        }

        public void ShowLockedRoom()
        {
            hasRoom = true;
            isLocked = true;

            codeBoxGroup.SetActive(false);
            shortenedCodeGroup.SetActive(true);
            shortenedCodeText.text = Spaced(codeInputField.text);
            passwordGroup.SetActive(true);
            passwordInputField.SetTextWithoutNotify(string.Empty);
            enterButtonGroup.SetActive(true);

            ShowFailure(RoomEntryFailure.None);
            RefreshEnterButton();
            ResizePanel(lockedRoomSize);
            PlaceError(lockedRoomErrorY);
            passwordInputField.ActivateInputField();
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
        private static string DescribeFailure(RoomEntryFailure failure)
        {
            switch (failure)
            {
                case RoomEntryFailure.None:
                    return string.Empty;
                case RoomEntryFailure.InvalidCode:
                case RoomEntryFailure.NotFound:
                    return "존재하지 않는 방코드입니다.";
                case RoomEntryFailure.WrongPassword:
                    return "비밀번호가 일치하지 않습니다.";
                case RoomEntryFailure.Full:
                    return "방이 가득 찼습니다.";
                case RoomEntryFailure.Closed:
                    return "입장할 수 없는 방입니다.";
                case RoomEntryFailure.AlreadyInRoom:
                    return "이미 다른 방에 있습니다.";
                case RoomEntryFailure.ConnectionFailed:
                    return "연결에 실패했습니다. 잠시 후 다시 시도해 주세요.";
                default:
                    return "입장하지 못했습니다.";
            }
        }

        /// <summary>
        /// Draws the code one character per box, so the boxes always agree with
        /// the field behind them.
        /// </summary>
        private void RefreshCodeBoxes()
        {
            var code = codeInputField.text;

            for (var index = 0; index < codeBoxTexts.Length; index++)
            {
                codeBoxTexts[index].text = index < code.Length
                    ? code[index].ToString()
                    : string.Empty;
            }
        }

        private void RefreshEnterButton()
        {
            var canEnter = hasRoom &&
                           (!isLocked || !string.IsNullOrEmpty(passwordInputField.text));

            enterButton.interactable = canEnter && !isBusy;
        }

        private void ResizePanel(Vector2 size)
        {
            if (panel != null)
            {
                panel.sizeDelta = size;
            }
        }

        private void PlaceError(float y)
        {
            var rect = errorText.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private void FocusCodeField()
        {
            codeInputField.ActivateInputField();

            // Collapses any selection as well, which setting caretPosition
            // alone does not do.
            codeInputField.MoveTextEnd(false);
        }

        /// <summary>
        /// Keeps only characters a code can contain, uppercased. Typing a
        /// letter that was left out of the alphabet simply does not register,
        /// which reads as an unresponsive key rather than a rejected code.
        /// </summary>
        private void OnCodeTextChanged(string text)
        {
            filtered.Clear();

            for (var index = 0; index < text.Length; index++)
            {
                var character = char.ToUpperInvariant(text[index]);

                if (RoomCodeFormat.IsAllowed(character) &&
                    filtered.Length < RoomCodeFormat.CodeLength)
                {
                    filtered.Append(character);
                }
            }

            var cleaned = filtered.ToString();

            if (!string.Equals(cleaned, text, StringComparison.Ordinal))
            {
                codeInputField.SetTextWithoutNotify(cleaned);
                codeInputField.caretPosition = cleaned.Length;
            }

            RefreshCodeBoxes();

            if (cleaned.Length == RoomCodeFormat.CodeLength)
            {
                CodeCompleted?.Invoke(cleaned);
                return;
            }

            if (hasRoom)
            {
                CodeCleared?.Invoke();
                return;
            }

            ShowFailure(RoomEntryFailure.None);
        }

        private static string Spaced(string code)
        {
            return code == null ? string.Empty : string.Join(" ", code.ToCharArray());
        }

        private void OnCloseButtonClicked() => CloseRequested?.Invoke();

        private void OnShortenedCodeClicked()
        {
            CodeEditRequested?.Invoke();

            // Whoever answered has put the boxes back by now, so the caret can
            // go to the code the player just asked to change.
            FocusCodeField();
        }

        private void OnPasswordTextChanged(string text)
        {
            // The previous answer no longer describes what is in the field.
            ShowFailure(RoomEntryFailure.None);
            RefreshEnterButton();
        }

        private void OnPasswordSubmitted(string text) => TryEnter();

        private void OnEnterButtonClicked() => TryEnter();

        private void TryEnter()
        {
            if (!enterButton.interactable)
            {
                return;
            }

            EnterRequested?.Invoke(
                codeInputField.text,
                isLocked ? passwordInputField.text : null);
        }
    }
}
