using Game.Client.Common;
using Game.Client.Home;
using Game.Core.Emotes;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Emotes
{
    public sealed class EmoteWheelView : MonoBehaviour
    {
        public const float OuterRadius = 220f;
        public const float InnerRadius = 78f;

        private EmoteWheelRingGraphic ring;
        private TextMeshProUGUI[] labels;
        private TextMeshProUGUI hint;

        public static EmoteWheelView Create(Transform parent)
        {
            var root = new GameObject("EmoteWheel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;
            HudScreenScale.EnsureOn(canvas);

            var view = root.AddComponent<EmoteWheelView>();
            view.Build();
            view.Hide();
            return view;
        }

        public void Show()
        {
            // 언어 설정이 바뀌어도 휠을 다시 만들지 않으므로 열 때마다 라벨을 다시 읽는다.
            RefreshLabels();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetHighlight(int? slice)
        {
            if (ring != null)
            {
                ring.HighlightedSlice = slice;
            }

            if (labels == null)
            {
                return;
            }

            for (var i = 0; i < labels.Length; i++)
            {
                var selected = slice == i;
                labels[i].color = selected ? Color.white : new Color(0.85f, 0.82f, 0.76f, 0.85f);
                labels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }

            if (hint != null)
            {
                hint.text = slice is int index
                    ? $"{index + 1}  {EmoteCatalog.All[index].Label}"
                    : UiLocale.Applied(UiText.Emote.Hint);
            }
        }

        private void RefreshLabels()
        {
            if (labels == null)
            {
                return;
            }

            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].text = $"{i + 1}  {EmoteCatalog.All[i].Label}";
            }
        }

        private void Build()
        {
            var font = HomeUiFonts.Apply();
            var host = (RectTransform)transform;
            host.anchorMin = Vector2.zero;
            host.anchorMax = Vector2.one;
            host.offsetMin = Vector2.zero;
            host.offsetMax = Vector2.zero;

            var ringObject = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmoteWheelRingGraphic));
            var ringRect = (RectTransform)ringObject.transform;
            ringRect.SetParent(host, false);
            ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.sizeDelta = new Vector2(OuterRadius * 2f, OuterRadius * 2f);
            ring = ringObject.GetComponent<EmoteWheelRingGraphic>();
            ring.raycastTarget = false;

            labels = new TextMeshProUGUI[EmoteCatalog.Count];
            var labelRadius = (InnerRadius + OuterRadius) * 0.5f;
            for (var i = 0; i < EmoteCatalog.Count; i++)
            {
                var angle = (i + 0.5f) * EmoteWheelSelection.SliceDegrees * Mathf.Deg2Rad;
                var labelObject = new GameObject(EmoteCatalog.All[i].StateName, typeof(RectTransform), typeof(TextMeshProUGUI));
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(host, false);
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = new Vector2(140f, 48f);
                labelRect.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * labelRadius;
                var text = labelObject.GetComponent<TextMeshProUGUI>();
                text.font = font;
                text.fontSize = 28f;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.text = $"{i + 1}  {EmoteCatalog.All[i].Label}";
                labels[i] = text;
            }

            var hintObject = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            var hintRect = (RectTransform)hintObject.transform;
            hintRect.SetParent(host, false);
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(160f, 72f);
            hint = hintObject.GetComponent<TextMeshProUGUI>();
            hint.font = font;
            hint.fontSize = 18f;
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color(0.92f, 0.9f, 0.84f, 0.9f);
            hint.raycastTarget = false;
            SetHighlight(null);
        }
    }
}
