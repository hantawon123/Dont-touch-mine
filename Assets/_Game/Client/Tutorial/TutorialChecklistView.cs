using Game.Client.Home;
using Game.Core.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Tutorial
{
    public sealed class TutorialChecklistView : MonoBehaviour
    {
        private static readonly string[] Steps = { "이동 · 주변 살피기", "달리기", "점프", "앉아서 통과", "기어서 통과", "물건 들기", "지정 구역에 내려놓기", "표적에 던지기", "책상에 배치하기", "파쇄기 사용", "출구 문 열기" };
        private readonly TMP_Text[] rows = new TMP_Text[Steps.Length];
        private TMP_Text actionHint;

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
            view.Label("Title", "도둑의 기본 훈련", 26, 14);
            for (var i = 0; i < Steps.Length; i++) view.rows[i] = view.Label("Step" + i, $"{i + 1:00}  {Steps[i]}", 25, 58 + i * 35);
            view.actionHint = view.Label("CurrentAction", string.Empty, 24, 450);
            view.actionHint.color = new Color(1, .8f, .25f);
            return view;
        }

        public void Show(TutorialStep step, string hint)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                rows[i].color = i < (int)step ? new Color(.6f, .6f, .6f, .55f)
                    : i == (int)step ? new Color(1, .8f, .25f) : new Color(.88f,.88f,.88f);
                rows[i].fontStyle = i == (int)step ? FontStyles.Bold : FontStyles.Normal;
                rows[i].text = $"{i + 1:00}  {Steps[i]}";
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
