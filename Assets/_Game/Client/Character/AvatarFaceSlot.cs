using Game.Core.Players;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Character
{
    /// <summary>
    /// Keeps a circular profile slot showing the worn face for a player id,
    /// account id, or the local selection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarFaceSlot : MonoBehaviour
    {
        private string playerId;
        private string userId;
        private bool followLocal;
        private bool pinned;
        private AvatarAppearance shown;
        private bool hasShown;

        public static AvatarFaceSlot Attach(RectTransform circle)
        {
            if (circle == null)
            {
                return null;
            }

            return circle.GetComponent<AvatarFaceSlot>() ??
                   circle.gameObject.AddComponent<AvatarFaceSlot>();
        }

        public void Follow(string nextPlayerId, string nextUserId = null)
        {
            var nextId = string.IsNullOrWhiteSpace(nextPlayerId) ? null : nextPlayerId.Trim();
            var nextUser = string.IsNullOrWhiteSpace(nextUserId) ? null : nextUserId.Trim();
            if (!followLocal && !pinned && playerId == nextId && userId == nextUser)
            {
                Refresh();
                return;
            }

            pinned = false;
            followLocal = false;
            playerId = nextId;
            userId = nextUser;
            hasShown = false;
            Refresh();
        }

        public void FollowLocal()
        {
            if (followLocal && !pinned)
            {
                Refresh();
                return;
            }

            pinned = false;
            followLocal = true;
            playerId = null;
            userId = null;
            hasShown = false;
            Refresh();
        }

        public void Show(AvatarAppearance appearance)
        {
            pinned = true;
            followLocal = false;
            playerId = null;
            userId = null;
            Apply(appearance, true);
        }

        private void LateUpdate()
        {
            if (pinned || (hasShown && IsPortraitShowing() && BoardMatchesShown()))
            {
                return;
            }

            Refresh();
        }

        private void OnDisable()
        {
            AvatarFacePortrait.Clear(transform as RectTransform);
            hasShown = false;
        }

        private void Refresh()
        {
            if (pinned)
            {
                return;
            }

            if (followLocal)
            {
                if (AvatarAppearanceBoard.HasLocal)
                {
                    Apply(AvatarAppearanceBoard.Local, true);
                }

                return;
            }

            if (AvatarAppearanceBoard.TryGet(playerId, out var appearance) ||
                AvatarAppearanceBoard.TryGet(userId, out appearance))
            {
                Apply(appearance, true);
                return;
            }

            if (hasShown)
            {
                AvatarFacePortrait.Clear(transform as RectTransform);
                hasShown = false;
            }
        }

        private void Apply(AvatarAppearance appearance, bool available)
        {
            if (!available)
            {
                return;
            }

            if (hasShown && shown == appearance && IsPortraitShowing())
            {
                return;
            }

            shown = appearance;
            hasShown = true;
            AvatarFacePortrait.Bind(transform as RectTransform, appearance);
        }

        private bool BoardMatchesShown()
        {
            if (followLocal)
            {
                return AvatarAppearanceBoard.HasLocal && shown == AvatarAppearanceBoard.Local;
            }

            return (AvatarAppearanceBoard.TryGet(playerId, out var appearance) ||
                    AvatarAppearanceBoard.TryGet(userId, out appearance)) &&
                   shown == appearance;
        }

        private bool IsPortraitShowing()
        {
            var portrait = transform.Find(AvatarFacePortrait.PortraitName)?.GetComponent<RawImage>();
            return portrait != null && portrait.enabled && portrait.texture != null;
        }
    }
}
