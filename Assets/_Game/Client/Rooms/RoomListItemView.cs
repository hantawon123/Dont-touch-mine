using System;
using Game.Core.Lobby;
using Game.Core.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// One room in the browser list, drawn to the 0904 wire-frame.
    /// </summary>
    /// <remarks>
    /// Built in code and handed its fonts, like the screen that holds it, so the
    /// row and the panel around it read their measurements from the same place
    /// and cannot drift apart. <see cref="Create"/> is the only way to make one.
    /// <para>
    /// The columns sit at fixed offsets rather than in a layout group. The
    /// wire-frame spaces them from the row's left edge, not from each other, so
    /// a short room title and a long one put the map name in the same place.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RoomListItemView : MonoBehaviour
    {
        private Image fill;
        private Image hover;
        private Image stroke;
        private Image statusDot;
        private TMP_Text statusText;
        private TMP_Text titleText;
        private TMP_Text mapText;
        private TMP_Text playerCountText;
        private TMP_Text hostNicknameText;
        private Button selectButton;

        private string roomId;

        public event Action<string> Selected;

        public static RoomListItemView Create(
            Transform parent, TMP_FontAsset semiBold, TMP_FontAsset medium)
        {
            var rect = RoomBrowserUi.CreateRect("RoomListItem", parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RoomBrowserStyle.Layout.RowHeight);

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = RoomBrowserStyle.Layout.RowHeight;
            element.minHeight = RoomBrowserStyle.Layout.RowHeight;

            var view = rect.gameObject.AddComponent<RoomListItemView>();
            view.Build(rect, semiBold, medium);
            return view;
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnSelectButtonClicked);
            }
        }

        public void Bind(RoomSummary room)
        {
            roomId = room.RoomId;

            titleText.text = room.Settings.Title;
            mapText.text = room.Settings.MapId;
            playerCountText.text = $"{room.CurrentPlayerCount}/{room.Settings.MaxPlayers}";
            hostNicknameText.text = string.IsNullOrEmpty(room.HostNickname)
                ? string.Empty
                : $"{room.HostNickname}의 방";

            statusText.text = room.Status == RoomStatus.Waiting ? "대기중" : "게임중";

            // What greys a row out is whether it can be entered, not what it is
            // doing: a waiting room with six of six players reads the same as one
            // mid-game, because the player can do the same thing with either.
            var text = room.CanJoin
                ? RoomBrowserStyle.Palette.TextPrimary
                : RoomBrowserStyle.Palette.RowDisabledText;

            titleText.color = text;
            mapText.color = text;
            playerCountText.color = text;
            hostNicknameText.color = text;

            statusText.color = room.CanJoin
                ? RoomBrowserStyle.Palette.StatusWaiting
                : RoomBrowserStyle.Palette.RowDisabledText;
            statusDot.color = statusText.color;

            selectButton.interactable = room.CanJoin;
        }

        private void Build(
            RectTransform rect, TMP_FontAsset semiBold, TMP_FontAsset medium)
        {
            var radius = RoomBrowserStyle.Radius.Row;

            // Transparent, and still needed: without a graphic the row catches
            // no pointer, and the hover highlight has nothing to sit on.
            fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = RoomBrowserUi.Rounded(radius);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;
            fill.color = Color.clear;

            hover = RoomBrowserUi.CreateImage(
                "Hover", rect, RoomBrowserStyle.Palette.RowHoverFill,
                RoomBrowserUi.Rounded(radius));
            hover.rectTransform.Stretch();
            hover.raycastTarget = false;

            stroke = RoomBrowserUi.CreateImage(
                "Stroke", rect, RoomBrowserStyle.Palette.RowStroke,
                RoomBrowserUi.Outline(radius, RoomBrowserStyle.Layout.RowStrokeThickness));
            stroke.rectTransform.Stretch();
            stroke.raycastTarget = false;

            statusDot = RoomBrowserUi.CreateImage(
                "StatusDot", rect, RoomBrowserStyle.Palette.StatusWaiting,
                RoomBrowserUi.Circle());
            statusDot.type = Image.Type.Simple;
            statusDot.raycastTarget = false;
            statusDot.rectTransform.Anchor(
                new Vector2(0f, 0.5f),
                new Vector2(RoomBrowserStyle.Layout.RowPaddingLeft, 0f),
                new Vector2(
                    RoomBrowserStyle.Layout.StatusDotDiameter,
                    RoomBrowserStyle.Layout.StatusDotDiameter));

            statusText = Label(
                "Status", rect, semiBold, RoomBrowserStyle.FontSize.RoomStatus,
                RoomBrowserStyle.Palette.StatusWaiting,
                RoomBrowserStyle.Layout.RowPaddingLeft
                    + RoomBrowserStyle.Layout.StatusDotDiameter
                    + RoomBrowserStyle.Layout.StatusDotGap,
                RoomBrowserStyle.Layout.StatusLabelWidth);

            titleText = Label(
                "Title", rect, semiBold, RoomBrowserStyle.FontSize.RoomTitle,
                RoomBrowserStyle.Palette.TextPrimary,
                RoomBrowserStyle.Layout.RowTitleX,
                RoomBrowserStyle.Layout.RowTitleWidth);

            // A room title is shown whole. The column is wide enough for the
            // twenty characters a title can hold, and the gap after it absorbs
            // the few pixels a full one can run over, so nothing is cut and the
            // map name still starts at the same x in every row.
            titleText.overflowMode = TextOverflowModes.Overflow;

            mapText = Label(
                "Map", rect, medium, RoomBrowserStyle.FontSize.MapName,
                RoomBrowserStyle.Palette.TextPrimary,
                RoomBrowserStyle.Layout.RowMapX,
                RoomBrowserStyle.Layout.RowMapWidth);

            playerCountText = RightLabel(
                "PlayerCount", rect, medium, RoomBrowserStyle.FontSize.PlayerCount,
                RoomBrowserStyle.Layout.RowLineOffsetY);

            hostNicknameText = RightLabel(
                "HostNickname", rect, medium, RoomBrowserStyle.FontSize.HostName,
                -RoomBrowserStyle.Layout.RowLineOffsetY);

            selectButton = rect.gameObject.AddComponent<Button>();
            selectButton.targetGraphic = hover;
            selectButton.transition = Selectable.Transition.ColorTint;
            selectButton.navigation = new Navigation { mode = Navigation.Mode.None };

            // The hover graphic carries the colour; these states carry only how
            // much of it shows, so a row that cannot be entered stays flat.
            var colors = selectButton.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.08f;
            selectButton.colors = colors;

            selectButton.onClick.AddListener(OnSelectButtonClicked);
        }

        private static TMP_Text Label(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            float size,
            Color color,
            float offsetX,
            float width)
        {
            var text = RoomBrowserUi.CreateText(
                name, parent, font, size, color, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Anchor(
                new Vector2(0f, 0.5f),
                new Vector2(offsetX, 0f),
                new Vector2(width, RoomBrowserStyle.Layout.RowHeight));
            return text;
        }

        private static TMP_Text RightLabel(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            float size,
            float offsetY)
        {
            var text = RoomBrowserUi.CreateText(
                name,
                parent,
                font,
                size,
                RoomBrowserStyle.Palette.TextPrimary,
                TextAlignmentOptions.MidlineRight);
            text.rectTransform.Anchor(
                new Vector2(1f, 0.5f),
                new Vector2(-RoomBrowserStyle.Layout.RowPaddingRight, offsetY),
                new Vector2(RoomBrowserStyle.Layout.RowCountWidth, 28f));
            return text;
        }

        private void OnSelectButtonClicked()
        {
            Selected?.Invoke(roomId);
        }
    }
}
