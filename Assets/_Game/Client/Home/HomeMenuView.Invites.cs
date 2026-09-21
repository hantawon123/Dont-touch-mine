using System;
using System.Collections.Generic;
using Game.Client.Common;
using Game.Core.Home;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Client.Home
{
    /// <summary>
    /// The stack of room invitations at the top left: three cards, each with
    /// the sender's name and an accept and a decline button.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Common.ConnectionToast"/>, these take clicks and do not
    /// time out, because an invite is a question rather than a notice. The
    /// cards are built once and reused: a card's buttons read the id it is
    /// currently showing, so rebinding is a matter of setting a field.
    /// </remarks>
    public sealed partial class HomeMenuView
    {
        private sealed class InviteCard
        {
            public RectTransform Rect;
            public TMP_Text Body;
            public Image AcceptIcon;
            public Image DeclineIcon;

            /// <summary>What this card is showing right now, or null when it is off.</summary>
            public string Id;
        }

        private readonly List<InviteCard> inviteCards = new List<InviteCard>(RoomInviteInbox.VisibleLimit);

        /// <summary>
        /// The cards' own scene root, kept apart from the home canvas.
        /// </summary>
        private GameObject inviteRoot;

        /// <summary>The player took up an invitation, by the id it was shown under.</summary>
        public event Action<string> RoomInviteAccepted;

        /// <summary>The player turned an invitation down, by the id it was shown under.</summary>
        public event Action<string> RoomInviteDeclined;

        /// <summary>
        /// Builds the invite stack on a canvas of its own, at the root of this
        /// screen's scene rather than inside the home canvas.
        /// </summary>
        /// <remarks>
        /// An invitation is a question a friend is waiting on, so it has to be
        /// answerable from wherever the player wanders off to. Room browsing,
        /// the closet and the settings screen all keep this scene loaded and
        /// merely switch its roots off; a card inside the home canvas would go
        /// dark with it and only resurface once the player came back, which is
        /// too late to be an answer.
        /// <para>
        /// It draws above the home canvas so a card is never behind a panel,
        /// and it is the only thing on its canvas, so nothing else it might
        /// have covered goes with it.
        /// </para>
        /// </remarks>
        private void CreateInviteStack()
        {
            inviteRoot = new GameObject("InviteToasts", typeof(RectTransform));
            inviteRoot.layer = LayerMask.NameToLayer("UI");
            inviteRoot.AddComponent<FrontendPersistentRoot>();

            // Built while this scene is loading, when the active scene may still
            // be another one. Placed by hand so it is a root of this scene and
            // goes when this scene does.
            if (gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(inviteRoot, gameObject.scene);
            }

            var canvasComponent = inviteRoot.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = HomeStyle.Toast.SortingOrder;

            var scaler = inviteRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HomeStyle.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            inviteRoot.AddComponent<GraphicRaycaster>();

            var canvas = inviteRoot.GetComponent<RectTransform>();
            var root = CreateRect("InviteStack", canvas);
            SetAnchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            root.anchoredPosition = new Vector2(HomeStyle.Toast.Left, -HomeStyle.Toast.Top);
            root.sizeDelta = new Vector2(
                HomeStyle.Toast.Width,
                (RoomInviteInbox.VisibleLimit * HomeStyle.Toast.Height)
                    + ((RoomInviteInbox.VisibleLimit - 1) * HomeStyle.Toast.Gap));

            inviteCards.Clear();
            for (var slot = 0; slot < RoomInviteInbox.VisibleLimit; slot++)
            {
                inviteCards.Add(CreateInviteCard(root, slot));
            }
        }

        private InviteCard CreateInviteCard(RectTransform parent, int slot)
        {
            var rect = CreateRect($"Invite{slot}", parent);
            SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(
                0f, -slot * (HomeStyle.Toast.Height + HomeStyle.Toast.Gap));
            rect.sizeDelta = new Vector2(HomeStyle.Toast.Width, HomeStyle.Toast.Height);

            var fill = AddImage(
                rect,
                HomeStyle.Palette.ToastFill,
                HomeUiFonts.Rounded(HomeStyle.Radius.Toast),
                raycastTarget: true);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;

            var icon = CreateRect("Alert", rect);
            SetAnchor(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            icon.anchoredPosition = new Vector2(HomeStyle.Toast.IconLeft, 0f);
            icon.sizeDelta = new Vector2(HomeStyle.Toast.IconWidth, HomeStyle.Toast.IconHeight);
            var iconImage = AddImage(icon, Color.white, alertIcon);
            iconImage.preserveAspect = true;
            iconImage.enabled = alertIcon != null;

            var card = new InviteCard { Rect = rect };

            // Accept sits at the right edge with decline beside it, the way the
            // mock-up reads: the answer the friend hopes for is the outer one.
            card.AcceptIcon = CreateInviteButton(
                rect, "Accept", acceptIcon, 0, () => RoomInviteAccepted?.Invoke(card.Id));
            card.DeclineIcon = CreateInviteButton(
                rect, "Decline", rejectIcon, 1, () => RoomInviteDeclined?.Invoke(card.Id));

            var buttonsWidth =
                HomeStyle.Toast.ButtonRight
                + (2f * HomeStyle.Toast.ButtonDiameter)
                + HomeStyle.Toast.ButtonGap;

            var textRect = CreateRect("Body", rect);
            SetAnchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            textRect.offsetMin = new Vector2(
                HomeStyle.Toast.IconLeft + HomeStyle.Toast.IconWidth + HomeStyle.Toast.IconToText, 0f);
            textRect.offsetMax = new Vector2(-(buttonsWidth + HomeStyle.Toast.TextToButtons), 0f);
            card.Body = AddText(
                textRect,
                string.Empty,
                HomeStyle.FontSize.Toast,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            card.Body.color = HomeStyle.Palette.ToastText;

            // The name and the sentence are two lines by design. A name too long
            // even for its own line is cut short rather than pushed onto a third.
            card.Body.textWrappingMode = TextWrappingModes.Normal;
            card.Body.overflowMode = TextOverflowModes.Ellipsis;
            card.Body.maxVisibleLines = 2;

            rect.gameObject.SetActive(false);
            return card;
        }

        private static Image CreateInviteButton(
            RectTransform parent, string name, Sprite sprite, int slotFromRight, Action onClick)
        {
            var rect = CreateRect(name, parent);
            SetAnchor(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            rect.anchoredPosition = new Vector2(
                -(HomeStyle.Toast.ButtonRight
                    + (slotFromRight * (HomeStyle.Toast.ButtonDiameter + HomeStyle.Toast.ButtonGap))),
                0f);
            rect.sizeDelta = new Vector2(HomeStyle.Toast.ButtonDiameter, HomeStyle.Toast.ButtonDiameter);

            var image = AddImage(rect, Color.white, sprite, raycastTarget: true);
            image.preserveAspect = true;
            image.enabled = sprite != null;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            return image;
        }

        /// <summary>
        /// Shows these invitations, oldest at the top. Anything past the stack's
        /// three slots is not this view's concern; the inbox holds it back.
        /// </summary>
        public void SetRoomInvites(IReadOnlyList<RoomInvite> invites)
        {
            if (invites == null)
            {
                throw new ArgumentNullException(nameof(invites));
            }

            for (var slot = 0; slot < inviteCards.Count; slot++)
            {
                var card = inviteCards[slot];
                var used = slot < invites.Count;
                card.Rect.gameObject.SetActive(used);
                if (!used)
                {
                    card.Id = null;
                    continue;
                }

                card.Id = invites[slot].Id;
                card.Body.text = string.Format(
                    Copy(UiText.Home.InviteBody),
                    invites[slot].FromNickname);
            }
        }
    }
}
