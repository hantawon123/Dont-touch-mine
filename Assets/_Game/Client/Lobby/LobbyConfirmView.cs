using System;
using Game.Client.Home;
using Game.Core.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    public interface ILobbyConfirmView
    {
        event Action Confirmed;
        event Action Cancelled;

        ReportReason SelectedReason { get; }

        string Note { get; }

        void Show(string message, string confirmLabel);
        void Show(string message, string confirmLabel, bool chooseReason);
        void Hide();
    }

    public class LobbyConfirmView : MonoBehaviour, ILobbyConfirmView
    {
        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Text messageText;

        [SerializeField]
        private Button confirmButton;

        [SerializeField]
        private Button cancelButton;

        public event Action Confirmed;
        public event Action Cancelled;

        public ReportReason SelectedReason { get; private set; } = ReportReason.Other;

        public string Note => string.Empty;

        public static LobbyConfirmView Create(Transform parent, bool showCancel = true)
        {
            var root = new GameObject("Lobby confirmation", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            Game.Client.Common.HudScreenScale.Apply(scaler);
            var background = Rect("Backdrop", root.transform, Vector2.zero, Vector2.one);
            background.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
            var body = Rect("Panel", background, new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.64f));
            body.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f);
            var view = root.AddComponent<LobbyConfirmView>();
            view.panel = root;
            view.messageText = Label(body, "Message", new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.95f));
            view.confirmButton = Button(body, "확인", new Vector2(0.12f, 0.1f), new Vector2(0.45f, 0.32f));
            if (showCancel)
                view.cancelButton = Button(body, "취소", new Vector2(0.55f, 0.1f), new Vector2(0.88f, 0.32f));
            return view;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text Label(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(Transform parent, string label, Vector2 min, Vector2 max)
        {
            var rect = Rect(label, parent, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.25f, 0.4f, 0.6f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            Label(rect, "Label", Vector2.zero, Vector2.one).text = label;
            return button;
        }

        private void OnEnable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancel);
            }
        }

        private void OnDisable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancel);
            }
        }

        public void Show(string message) => Show(message, null, false);

        public void Show(string message, string confirmLabel) =>
            Show(message, confirmLabel, false);

        public void Show(string message, string confirmLabel, bool chooseReason)
        {
            _ = confirmLabel;
            _ = chooseReason;
            HomeUiFonts.ApplyLegacy(panel != null ? panel.transform : transform);
            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void HandleConfirm() => Confirmed?.Invoke();

        private void HandleCancel() => Cancelled?.Invoke();
    }
}
