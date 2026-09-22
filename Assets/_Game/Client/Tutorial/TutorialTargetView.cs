using Game.Client.Home;
using TMPro;
using UnityEngine;

namespace Game.Client.Tutorial
{
    public sealed class TutorialTargetView : MonoBehaviour
    {
        private Transform caption;
        private TMP_Text captionText;
        private Camera viewCamera;

        /// <summary>
        /// Writes the outline's caption again, for when the language changes
        /// while the course is already standing.
        /// </summary>
        public void SetLabel(string label)
        {
            if (captionText != null)
            {
                captionText.text = label;
            }
        }

        public static GameObject Create(Transform parent, string name, Vector3 position, Vector2 size,
            bool vertical, string label, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = .07f;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var corners = new[] { new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1) };
            for (var i = 0; i < corners.Length; i++)
            {
                var point = Vector2.Scale(corners[i], size) * .5f;
                line.SetPosition(i, vertical ? new Vector3(point.x, point.y, 0) : new Vector3(point.x, 0, point.y));
            }
            var text = new GameObject("Instruction").AddComponent<TextMeshPro>();
            text.transform.SetParent(root.transform, false);
            text.transform.localPosition = Vector3.up * (vertical ? size.y * .5f + .35f : .65f);
            text.text = label;
            text.font = HomeUiFonts.Apply();
            text.fontSize = 1.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1, .8f, .2f);
            var view = root.AddComponent<TutorialTargetView>();
            view.caption = text.transform;
            view.captionText = text;
            return root;
        }

        private void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera != null && caption != null) caption.rotation = viewCamera.transform.rotation;
        }
    }
}
