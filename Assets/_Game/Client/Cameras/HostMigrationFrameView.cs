using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Cameras
{
    /// <summary>Holds the last rendered frame while the network replaces its objects.</summary>
    public sealed class HostMigrationFrameView : MonoBehaviour
    {
        private Canvas canvas;
        private CanvasGroup group;
        private RawImage image;
        private RenderTexture frame;
        private bool revealing;

        // Must be called at end-of-frame, before the old runner is shut down.
        public void Capture()
        {
            Clear();
            Prepare();
            if (frame == null || !frame.IsCreated()) return;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(frame);
            image.texture = frame;
            group.alpha = 1f;
            canvas.gameObject.SetActive(true);
        }

        // Warm up during normal room updates, not on the migration/render stack.
        public void Prepare()
        {
            if (Application.isBatchMode || Screen.width <= 0 || Screen.height <= 0) return;
            if (canvas != null && canvas.gameObject.activeSelf) return;
            if (canvas == null)
            {
                var root = new GameObject("Migration Frame", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasGroup), typeof(GraphicRaycaster));
                root.transform.SetParent(transform, false);
                canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                group = root.GetComponent<CanvasGroup>();
                var picture = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
                picture.transform.SetParent(root.transform, false);
                image = picture.GetComponent<RawImage>();
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
                image.raycastTarget = true;
                root.SetActive(false);
            }
            if (frame != null && frame.width == Screen.width && frame.height == Screen.height && frame.IsCreated()) return;
            Release();
            frame = new RenderTexture(Screen.width, Screen.height, 0);
            frame.Create();
        }

        public void Reveal() => revealing = true;

        private void Update()
        {
            if (!revealing || group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime / 0.15f);
            if (group.alpha <= 0f) Clear();
        }

        public void Clear()
        {
            revealing = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (image != null) image.texture = null;
        }

        public void Release()
        {
            Clear();
            if (frame == null) return;
            frame.Release();
            if (Application.isPlaying) Destroy(frame);
            else DestroyImmediate(frame);
            frame = null;
        }

        private void OnDestroy() => Release();
    }
}
