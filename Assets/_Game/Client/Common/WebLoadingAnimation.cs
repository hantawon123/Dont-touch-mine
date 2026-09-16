using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

namespace Game.Client.Common
{
    /// <summary>WebGL loading letters run on the browser compositor during Unity stalls.</summary>
    internal static class WebLoadingAnimation
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int GameLoadingShow(string text, float x, float y, float width,
            float height, float fontSize, float bounce, float letterSeconds);
        [DllImport("__Internal")] private static extern void GameLoadingHide();
        [DllImport("__Internal")] private static extern int GameLoadingReady();
        private static readonly Vector3[] Corners = new Vector3[4];
#endif

        public static bool Show(TMP_Text label)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (label == null || Screen.width <= 0 || Screen.height <= 0) return false;
            label.rectTransform.GetWorldCorners(Corners);
            var canvas = label.canvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            var bottom = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
            var top = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);
            var height = top.y - bottom.y;
            if (height <= 0f) return false;
            var scale = height / Mathf.Max(1f, label.rectTransform.rect.height);
            return GameLoadingShow(LoadingView.LabelText, bottom.x / Screen.width,
                1f - top.y / Screen.height, (top.x - bottom.x) / Screen.width,
                height / Screen.height, label.fontSize * scale / Screen.height,
                LoadingView.BounceHeight * scale / Screen.height, LoadingView.LetterSeconds) != 0;
#else
            return false;
#endif
        }

        public static bool IsReady
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return GameLoadingReady() != 0;
#else
                return true;
#endif
            }
        }

        public static void Hide()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GameLoadingHide();
#endif
        }
    }
}
