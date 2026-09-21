using Game.Client.Home;
using Game.Core.Settings;
using Game.Core.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Tutorial
{
    public sealed class TutorialChecklistView : MonoBehaviour
    {
        public const float MinWidth = 350f;
        public const float PanelHeight = 510f;
        public const float SidePadding = 16f;

        private static readonly string[] StepKeys =
        {
            UiText.Tutorial.StepMove,
            UiText.Tutorial.StepSprint,
            UiText.Tutorial.StepJump,
            UiText.Tutorial.StepCrouch,
            UiText.Tutorial.StepProne,
            UiText.Tutorial.StepPickUp,
            UiText.Tutorial.StepDrop,
            UiText.Tutorial.StepThrow,
            UiText.Tutorial.StepPlace,
            UiText.Tutorial.StepShredder,
            UiText.Tutorial.StepExit
        };
        private readonly TMP_Text[] rows = new TMP_Text[StepKeys.Length];
        private TMP_Text actionHint;
        private TMP_Text title;

        public static TutorialChecklistView Create(Transform canvas)
        {
            var root = new GameObject("TutorialChecklist", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(32, -30);
            rect.sizeDelta = new Vector2(MinWidth, PanelHeight);
            root.GetComponent<Image>().color = new Color(.04f, .045f, .06f, .82f);
            root.GetComponent<Image>().raycastTarget = false;
            var view = root.AddComponent<TutorialChecklistView>();
            var language = UiLocale.AppliedLanguage;
            view.title = view.Label(
                "Title",
                UiTextCatalog.Shipped.Get(UiText.Tutorial.ChecklistTitle, language),
                26,
                14);
            for (var i = 0; i < StepKeys.Length; i++)
            {
                view.rows[i] = view.Label(
                    "Step" + i,
                    $"{i + 1:00}  {UiTextCatalog.Shipped.Get(StepKeys[i], language)}",
                    25,
                    58 + i * 35);
            }
            view.actionHint = view.Label("CurrentAction", string.Empty, 24, 450);
            view.actionHint.color = new Color(1, .8f, .25f);
            view.FitWidth();
            return view;
        }

        public void Show(TutorialStep step, string hint, string language = null)
        {
            language ??= UiLocale.AppliedLanguage;
            if (title != null)
            {
                title.text = UiTextCatalog.Shipped.Get(UiText.Tutorial.ChecklistTitle, language);
            }

            for (var i = 0; i < rows.Length; i++)
            {
                rows[i].color = i < (int)step ? new Color(.6f, .6f, .6f, .55f)
                    : i == (int)step ? new Color(1, .8f, .25f) : new Color(.88f,.88f,.88f);
                rows[i].fontStyle = i == (int)step ? FontStyles.Bold : FontStyles.Normal;
                rows[i].text = $"{i + 1:00}  {UiTextCatalog.Shipped.Get(StepKeys[i], language)}";
            }
            actionHint.text = hint;
            FitWidth();
        }

        /// <summary>
        /// The plate is as wide as its longest line, never narrower than the
        /// original design. English steps such as "Set it down in the zone"
        /// would otherwise clip inside the Korean width.
        /// </summary>
        private void FitWidth()
        {
            var needed = MinWidth;
            needed = Mathf.Max(needed, TextWidth(title) + SidePadding * 2f);
            for (var i = 0; i < rows.Length; i++)
            {
                needed = Mathf.Max(needed, TextWidth(rows[i]) + SidePadding * 2f);
            }

            needed = Mathf.Max(needed, TextWidth(actionHint) + SidePadding * 2f);
            var width = Mathf.Ceil(needed);
            ((RectTransform)transform).sizeDelta = new Vector2(width, PanelHeight);
            var labelWidth = width - SidePadding * 2f;
            SetLabelWidth(title, labelWidth);
            for (var i = 0; i < rows.Length; i++)
            {
                SetLabelWidth(rows[i], labelWidth);
            }

            SetLabelWidth(actionHint, labelWidth);
        }

        private static void SetLabelWidth(TMP_Text text, float width)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private static float TextWidth(TMP_Text text)
        {
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                return 0f;
            }

            text.ForceMeshUpdate();
            return text.GetPreferredValues(text.text).x;
        }

        private TMP_Text Label(string name, string content, float size, float top)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(transform, false);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(SidePadding, -top);
            rect.sizeDelta = new Vector2(MinWidth - SidePadding * 2f, 54);
            text.font = HomeUiFonts.Apply();
            text.fontSize = size;
            text.text = content;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
