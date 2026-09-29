using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace Game.Client.Common
{
    /// <summary>Web IME uses a native browser input; TMP remains the validated UI model.</summary>
    public sealed class WebTextInput : MonoBehaviour
    {
        /// <summary>
        /// The syllable the browser IME is still building in a field, before it
        /// reaches that field's text. Empty once it lands or is dropped.
        /// </summary>
        /// <remarks>
        /// Off WebGL the same thing comes from <c>Keyboard.onIMECompositionChange</c>;
        /// here the browser owns the composition, so it is relayed from there.
        /// </remarks>
        public static event Action<TMP_InputField, string> ComposingChanged;

        // TMP refreshes placeholder.enabled while its text is still empty during IME
        // composition. Hide the object so only the browser owns the editing placeholder.
        internal static bool HidePlaceholder(TMP_InputField input)
        {
            if (input.placeholder == null) return false;
            var wasActive = input.placeholder.gameObject.activeSelf;
            input.placeholder.gameObject.SetActive(false);
            return wasActive;
        }

        internal static void RestorePlaceholder(TMP_InputField input, bool wasActive)
        {
            if (input.placeholder == null) return;
            input.placeholder.gameObject.SetActive(wasActive);
            input.ForceLabelUpdate();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GameTextOpen(string target, string options);
        [DllImport("__Internal")] private static extern void GameTextLayout(float x, float y, float width, float height, float fontSize);
        [DllImport("__Internal")] private static extern void GameTextValue(string value);
        [DllImport("__Internal")] private static extern void GameTextClose();

        [Serializable] private sealed class Options
        {
            public int id, limit;
            public string value, placeholder, color;
            public bool password, multiline, selectAll;
        }
        [Serializable] private sealed class Edit
        {
            public int id;
            public string value, action, composing;
        }

        private TMP_InputField field;
        private int session;
        private bool captureKeyboard, textEnabled, placeholderActive;
        private readonly Vector3[] corners = new Vector3[4];

        private void LateUpdate()
        {
            if (!ReferenceEquals(field, null) && field == null) Close();
            var selected = EventSystem.current?.currentSelectedGameObject;
            var next = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            // Keep numeric/PIN and custom validators on TMP's character input path.
            if (next != null && (!next.isActiveAndEnabled || !next.interactable || next.readOnly || !next.isFocused
                || next.characterValidation != TMP_InputField.CharacterValidation.None
                || next.onValidateInput != null || next.inputValidator != null))
                next = null;
            if (next != field)
            {
                Close();
                if (next != null) Open(next);
            }
            if (field == null) return;

            var rect = field.textViewport != null ? field.textViewport : (RectTransform)field.transform;
            rect.GetWorldCorners(corners);
            var canvas = field.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var top = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            GameTextLayout(bottom.x / Screen.width, 1f - top.y / Screen.height,
                (top.x - bottom.x) / Screen.width, (top.y - bottom.y) / Screen.height,
                field.textComponent.fontSize * scale / Screen.height);
            GameTextValue(field.text);
        }

        private void Open(TMP_InputField input)
        {
            field = input;
            captureKeyboard = WebGLInput.captureAllKeyboardInput;
            WebGLInput.captureAllKeyboardInput = false;
            textEnabled = field.textComponent.enabled;
            field.textComponent.enabled = false;
            placeholderActive = HidePlaceholder(field);
            GameTextOpen(gameObject.name, JsonUtility.ToJson(new Options
            {
                id = ++session, value = field.text, limit = field.characterLimit,
                placeholder = (field.placeholder as TMP_Text)?.text ?? string.Empty,
                color = "#" + ColorUtility.ToHtmlStringRGBA(field.textComponent.color),
                password = field.inputType == TMP_InputField.InputType.Password,
                multiline = field.lineType == TMP_InputField.LineType.MultiLineNewline,
                selectAll = field.onFocusSelectAll
            }));
        }

        // Called only by the browser input owned by this component. Stale blur/composition
        // callbacks cannot write into the next field or a newly loaded scene.
        [Preserve]
        public void OnBrowserEdit(string json)
        {
            var edit = JsonUtility.FromJson<Edit>(json);
            if (field == null || edit.id != session) return;
            var input = field;
            if (edit.action == "compose")
            {
                ComposingChanged?.Invoke(input, edit.composing ?? string.Empty);
                return;
            }
            // Anything else means the composition has landed in the value or been dropped.
            ComposingChanged?.Invoke(input, string.Empty);
            if (field != input || input == null) return;
            input.text = edit.value;
            if (field != input || input == null) return;
            if (edit.action == "input")
            {
                GameTextValue(input.text);
                return;
            }
            var next = edit.action == "backtab" ? input.FindSelectableOnUp() : input.FindSelectableOnDown();
            Close();
            input.DeactivateInputField();
            if (EventSystem.current?.currentSelectedGameObject == input.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            if (edit.action == "submit") input.onSubmit.Invoke(input.text);
            if (edit.action == "cancel" && EventSystem.current != null)
                ExecuteEvents.ExecuteHierarchy(input.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            if ((edit.action == "tab" || edit.action == "backtab") && next != null) next.Select();
        }

        private void Close()
        {
            if (ReferenceEquals(field, null)) return;
            GameTextClose();
            WebGLInput.captureAllKeyboardInput = captureKeyboard;
            if (field != null)
            {
                field.textComponent.enabled = textEnabled;
                RestorePlaceholder(field, placeholderActive);
            }
            field = null;
            ++session;
        }

        private void OnDisable() => Close();
#endif
    }
}
