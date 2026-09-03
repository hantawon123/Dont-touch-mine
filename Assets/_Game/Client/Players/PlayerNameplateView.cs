using TMPro;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>World-space nickname shown above an in-game character.</summary>
    public sealed class PlayerNameplateView : MonoBehaviour
    {
        private const string ObjectName = "PlayerNameplate";
        // Lobby canvas: 2.6m anchor - 0.7m nameplate offset.
        private const float HeightOffset = 1.7f;

        private TextMeshPro label;
        private string displayedName = string.Empty;
        private Transform followAnchor;

        public bool HasNickname => displayedName.Length > 0;

        public static PlayerNameplateView Attach(Transform playerRoot)
        {
            var child = playerRoot.Find(ObjectName);
            if (child == null)
            {
                var childObject = new GameObject(ObjectName);
                child = childObject.transform;
                child.SetParent(playerRoot, false);
                child.localScale = Vector3.one * 0.25f;
            }

            var view = child.GetComponent<PlayerNameplateView>();
            view = view != null ? view : child.gameObject.AddComponent<PlayerNameplateView>();
            view.followAnchor = playerRoot.Find("Visual") ?? playerRoot;
            return view;
        }

        public void SetNickname(string nickname)
        {
            EnsureLabel();

            var trimmed = nickname?.Trim() ?? string.Empty;
            if (displayedName == trimmed)
            {
                return;
            }

            displayedName = trimmed;
            label.text = displayedName;
            label.enabled = displayedName.Length > 0;
        }

        private void Awake() => EnsureLabel();

        private void LateUpdate()
        {
            if (followAnchor != null)
            {
                transform.position = followAnchor.position + Vector3.up * HeightOffset;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                transform.rotation = camera.transform.rotation;
            }
        }

        private void EnsureLabel()
        {
            if (label != null)
            {
                return;
            }

            label = GetComponent<TextMeshPro>();
            if (label == null)
            {
                label = gameObject.AddComponent<TextMeshPro>();
            }

            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 3f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.outlineColor = Color.black;
            label.outlineWidth = 0.2f;
            label.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
            label.enabled = displayedName.Length > 0;
        }
    }
}
