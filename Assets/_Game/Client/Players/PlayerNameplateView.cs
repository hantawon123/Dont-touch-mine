using System;
using Game.Client.Home;
using Game.Client.Lobby;
using TMPro;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>World-space nickname shown just above an in-game character.</summary>
    public sealed class PlayerNameplateView : MonoBehaviour
    {
        private const string ObjectName = "PlayerNameplate";
        private const string VisualName = "Visual";
        public const string VoiceIconName = "Voice";
        internal const float VoiceIconLocalSize = 1.2f;
        internal const float VoiceIconGap = 0.2f;
        internal const float HeadClearance = 0.22f;
        private const float FallbackHeightOffset = 1.7f;

        private TextMeshPro label;
        private SpriteRenderer voiceIcon;
        private string displayedName = string.Empty;
        private Transform followAnchor;
        private CharacterController bodyController;
        private Renderer[] bodyRenderers;

        public bool HasNickname => displayedName.Length > 0;

        internal float WorldHalfHeight
        {
            get
            {
                var height = label != null ? label.rectTransform.rect.height : 1.2f;
                return height * 0.5f * Mathf.Abs(transform.lossyScale.y);
            }
        }

        internal Vector3 PositionAbove(float clearance, float objectHalfHeight)
        {
            RefreshPlacement();
            var top = HasNickname
                ? transform.position + Vector3.up * WorldHalfHeight
                : ResolveHeadTop();
            return top + Vector3.up * (clearance + objectHalfHeight);
        }

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
            view.BindFollow(playerRoot);
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
            LayoutVoiceIcon();
        }

        public void SetVoice(bool muted, bool talking)
        {
            EnsureVoiceIcon();
            if (voiceIcon == null)
            {
                return;
            }

            var show = displayedName.Length > 0;
            voiceIcon.enabled = show;
            if (!show)
            {
                return;
            }

            voiceIcon.sprite = LobbyPlayerListSprites.SoundOf(muted, talking);
            LayoutVoiceIcon();
        }

        private void Awake() => EnsureLabel();

        private void LateUpdate() => RefreshPlacement();

        internal void RefreshPlacement()
        {
            transform.position = ResolveHeadTop() + Vector3.up * HeadClearance;

            var camera = Camera.main;
            if (camera != null)
            {
                transform.rotation = camera.transform.rotation;
            }
        }

        private void BindFollow(Transform playerRoot)
        {
            followAnchor = playerRoot.Find(VisualName) ?? playerRoot;
            bodyController = playerRoot.GetComponent<CharacterController>();
            CacheBodyRenderers();
        }

        private void CacheBodyRenderers()
        {
            if (followAnchor == null)
            {
                bodyRenderers = Array.Empty<Renderer>();
                return;
            }

            var found = followAnchor.GetComponentsInChildren<Renderer>(true);
            var count = 0;
            for (var index = 0; index < found.Length; index++)
            {
                if (IsBodyRenderer(found[index]))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                bodyRenderers = Array.Empty<Renderer>();
                return;
            }

            bodyRenderers = new Renderer[count];
            var write = 0;
            for (var index = 0; index < found.Length; index++)
            {
                if (!IsBodyRenderer(found[index]))
                {
                    continue;
                }

                bodyRenderers[write++] = found[index];
            }
        }

        private bool IsBodyRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.transform == transform || renderer.transform.IsChildOf(transform))
            {
                return false;
            }

            return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
        }

        private Vector3 ResolveHeadTop()
        {
            if (TryGetBodyBounds(out var bounds))
            {
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }

            if (bodyController != null && bodyController.enabled)
            {
                var capsule = bodyController.bounds;
                return new Vector3(capsule.center.x, capsule.max.y, capsule.center.z);
            }

            var origin = followAnchor != null ? followAnchor.position : transform.position;
            return origin + Vector3.up * FallbackHeightOffset;
        }

        private bool TryGetBodyBounds(out Bounds bounds)
        {
            bounds = default;
            if (bodyRenderers == null || bodyRenderers.Length == 0)
            {
                CacheBodyRenderers();
            }

            var found = false;
            var alive = 0;
            for (var index = 0; index < bodyRenderers.Length; index++)
            {
                var renderer = bodyRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                alive++;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            if (alive == 0 && followAnchor != null)
            {
                CacheBodyRenderers();
            }

            return found && bounds.size.sqrMagnitude > 0.0001f;
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

            label.font = HomeUiFonts.Apply();
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
            EnsureVoiceIcon();
        }

        private void EnsureVoiceIcon()
        {
            if (voiceIcon != null)
            {
                return;
            }

            var child = transform.Find(VoiceIconName);
            if (child == null)
            {
                var created = new GameObject(VoiceIconName);
                child = created.transform;
                child.SetParent(transform, false);
            }

            voiceIcon = child.GetComponent<SpriteRenderer>();
            if (voiceIcon == null)
            {
                voiceIcon = child.gameObject.AddComponent<SpriteRenderer>();
            }

            voiceIcon.sprite = LobbyPlayerListSprites.SoundWhite;
            voiceIcon.color = Color.white;
            voiceIcon.enabled = displayedName.Length > 0;
            var tmpRenderer = label != null ? label.GetComponent<Renderer>() : null;
            if (tmpRenderer != null)
            {
                voiceIcon.sortingLayerID = tmpRenderer.sortingLayerID;
                voiceIcon.sortingOrder = tmpRenderer.sortingOrder + 1;
            }

            LayoutVoiceIcon();
        }

        private void LayoutVoiceIcon()
        {
            if (voiceIcon == null || label == null)
            {
                return;
            }

            var sprite = voiceIcon.sprite;
            if (sprite != null && sprite.pixelsPerUnit > 0f)
            {
                var native = sprite.rect.width / sprite.pixelsPerUnit;
                if (native > 0f)
                {
                    var scale = VoiceIconLocalSize / native;
                    voiceIcon.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            label.ForceMeshUpdate();
            var width = displayedName.Length == 0
                ? 0f
                : label.GetPreferredValues(displayedName).x;
            voiceIcon.transform.localPosition = new Vector3(
                (width * 0.5f) + VoiceIconGap + (VoiceIconLocalSize * 0.5f),
                0f,
                0f);
        }
    }
}
