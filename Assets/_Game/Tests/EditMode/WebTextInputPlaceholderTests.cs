using Game.Client.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class WebTextInputPlaceholderTests
    {
        [TestCase("", true)]
        [TestCase("한", false)]
        public void TmpRefreshCannotRevealPlaceholderDuringBrowserComposition(string committed, bool visibleAfterClose)
        {
            var root = new GameObject("IME input", typeof(RectTransform), typeof(Canvas));
            try
            {
                var input = root.AddComponent<TMP_InputField>();
                var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                text.transform.SetParent(root.transform, false);
                input.textComponent = text;
                var placeholder = new GameObject("Placeholder", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                placeholder.transform.SetParent(root.transform, false);
                placeholder.text = "방 이름 입력";
                input.placeholder = placeholder;
                input.ForceLabelUpdate();
                Assert.That(placeholder.enabled, Is.True);

                var wasActive = WebTextInput.HidePlaceholder(input);
                // A browser can already display 'ㅎ' while TMP still contains an empty string.
                input.ForceLabelUpdate();
                Assert.That(placeholder.enabled, Is.True, "TMP re-enables the Graphic during its label update");
                Assert.That(placeholder.gameObject.activeInHierarchy, Is.False);

                input.text = committed;
                WebTextInput.RestorePlaceholder(input, wasActive);
                Assert.That(placeholder.isActiveAndEnabled, Is.EqualTo(visibleAfterClose));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
