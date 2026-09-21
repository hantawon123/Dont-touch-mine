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
            rect.sizeDelta = new Vector2(350, 510);
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
        }

        private TMP_Text Label(string name, string content, float size, float top)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(transform, false);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(16, -top);
            rect.sizeDelta = new Vector2(320, 54);
            text.font = HomeUiFonts.Apply();
            text.fontSize = size;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }
    }
}
