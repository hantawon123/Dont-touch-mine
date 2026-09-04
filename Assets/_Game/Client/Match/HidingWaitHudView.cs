using System;
using System.Collections.Generic;
using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public readonly struct HidingWaitPlayer
    {
        public HidingWaitPlayer(string name, bool completed, bool current)
        {
            Name = name ?? string.Empty;
            Completed = completed;
            Current = current;
        }

        public string Name { get; }
        public bool Completed { get; }
        public bool Current { get; }
    }

    public interface IHidingWaitHudView
    {
        void Show(
            int completedCount,
            int totalCount,
            string hidingPlayerName,
            IReadOnlyList<HidingWaitPlayer> players);
        void Hide();
    }

    /// <summary>
    /// Waiting-player HUD during hiding: order list, progress, and status.
    /// Chat stays on the existing match chat view; this only paints the rest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingWaitHudView : MonoBehaviour, IHidingWaitHudView
    {
        public const float CountFontSize = 45f;
        public const float StatusFontSize = 28f;
        public const float NameFontSize = 18f;
        public const float TopPadding = 20f;
        public const float PersonIconSize = 40f;
        public const float AvatarSize = 40f;
        public const int MaxPlayers = 6;
        public static readonly Color AccentColor = new Color(1f, 0.54f, 0.24f, 1f);
        public static readonly Color DoneNameColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        public static readonly Color PendingColor = new Color(1f, 1f, 1f, 0.42f);
        private const string PersonIconResource = "UI/ic_person";
        private const string CheckMark = "✓";

        [SerializeField]
        private TMP_Text countText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private GameObject topPrompt;

        [SerializeField]
        private GameObject playerList;

        [SerializeField]
        [Tooltip("Shows the waiting HUD in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        private bool shown;

        public static HidingWaitHudView Create(Transform parent)
        {
            var rootObject = new GameObject("HidingWaitHud", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            Stretch((RectTransform)rootObject.transform);
            return rootObject.AddComponent<HidingWaitHudView>();
        }

        public static string FormatCount(int completedCount, int totalCount)
        {
            return $"{Mathf.Max(0, completedCount)} / {Mathf.Max(0, totalCount)}";
        }

        public static string FormatStatus(string hidingPlayerName)
        {
            return string.IsNullOrWhiteSpace(hidingPlayerName)
                ? "물건을 숨기는 중"
                : $"{hidingPlayerName.Trim()}님이 물건을 숨기는 중";
        }

        private void Awake()
        {
            EnsureLayout();
            if (previewOnAwake && !shown)
            {
                Show(
                    2,
                    6,
                    "이거언바로열두글자랍니다",
                    new[]
                    {
                        new HidingWaitPlayer("플레이어1", true, false),
                        new HidingWaitPlayer("플레이어2", true, false),
                        new HidingWaitPlayer("이거언바로열두글자랍니다", false, true),
                        new HidingWaitPlayer("플레이어4", false, false),
                        new HidingWaitPlayer("플레이어5", false, false),
                        new HidingWaitPlayer("플레이어6", false, false)
                    });
                return;
            }

            if (!shown)
            {
                Hide();
            }
        }

        public void Show(
            int completedCount,
            int totalCount,
            string hidingPlayerName,
            IReadOnlyList<HidingWaitPlayer> players)
        {
            shown = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            ApplyFonts();
            ApplyProgress(completedCount, totalCount, hidingPlayerName);
            ApplyPlayers(players);
            SetContentVisible(true);
        }

        public void Hide()
        {
            shown = false;
            SetContentVisible(false);
        }

        private void SetContentVisible(bool visible)
        {
            if (topPrompt != null)
            {
                topPrompt.SetActive(visible);
            }

            if (playerList != null)
            {
                playerList.SetActive(visible);
            }
        }

        private void ApplyFonts()
        {
            var font = HomeUiFonts.Apply();
            if (countText != null)
            {
                countText.font = font;
                countText.fontSize = CountFontSize;
                countText.fontStyle = FontStyles.Normal;
                countText.color = Color.white;
            }

            if (statusText != null)
            {
                statusText.font = font;
                statusText.fontSize = StatusFontSize;
                statusText.fontStyle = FontStyles.Normal;
                statusText.color = Color.white;
            }
        }

        private void ApplyProgress(int completedCount, int totalCount, string hidingPlayerName)
        {
            if (countText != null)
            {
                countText.text = FormatCount(completedCount, totalCount);
            }

            if (statusText != null)
            {
                statusText.text = FormatStatus(hidingPlayerName);
            }
        }

        private void ApplyPlayers(IReadOnlyList<HidingWaitPlayer> players)
        {
            if (playerList == null)
            {
                return;
            }

            var list = players ?? Array.Empty<HidingWaitPlayer>();
            for (var index = 0; index < MaxPlayers; index++)
            {
                var row = playerList.transform.Find($"Row{index}")?.gameObject;
                if (row == null)
                {
                    continue;
                }

                if (index >= list.Count)
                {
                    row.SetActive(false);
                    continue;
                }

                row.SetActive(true);
                PaintRow(row.transform, list[index]);
            }
        }

        private static void PaintRow(Transform row, HidingWaitPlayer player)
        {
            var ring = row.Find("Avatar/Ring")?.GetComponent<Image>();
            if (ring != null)
            {
                ring.enabled = player.Current;
                ring.color = AccentColor;
            }

            var avatar = row.Find("Avatar/Face")?.GetComponent<Image>();
            if (avatar != null)
            {
                avatar.color = player.Current
                    ? Color.white
                    : player.Completed
                        ? new Color(0.82f, 0.82f, 0.82f, 1f)
                        : PendingColor;
            }

            var check = row.Find("Avatar/Check")?.GetComponent<TMP_Text>();
            if (check != null)
            {
                check.gameObject.SetActive(player.Completed);
                check.color = AccentColor;
            }

            var name = row.Find("Name")?.GetComponent<TMP_Text>();
            if (name != null)
            {
                name.text = player.Name;
                name.font = HomeUiFonts.Apply();
                name.fontSize = NameFontSize;
                name.color = player.Current
                    ? AccentColor
                    : player.Completed
                        ? DoneNameColor
                        : PendingColor;
            }
        }

        private void EnsureLayout()
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                Stretch(rect);
            }

            if (transform.Find("TopPrompt") == null)
            {
                BuildLayout();
            }

            if (countText == null)
            {
                countText = transform.Find("TopPrompt/Count")?.GetComponent<TMP_Text>();
            }

            if (statusText == null)
            {
                statusText = transform.Find("TopPrompt/Status")?.GetComponent<TMP_Text>();
            }

            if (topPrompt == null)
            {
                topPrompt = transform.Find("TopPrompt")?.gameObject;
            }

            if (playerList == null)
            {
                playerList = transform.Find("PlayerList")?.gameObject;
            }
        }

        private void BuildLayout()
        {
            topPrompt = CreateRect(transform, "TopPrompt").gameObject;
            Place(
                topPrompt.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -TopPadding),
                new Vector2(980f, 110f),
                new Vector2(0.5f, 1f));

            var icon = CreateImage(
                topPrompt.transform,
                "Person",
                Color.white,
                Resources.Load<Sprite>(PersonIconResource));
            icon.preserveAspect = true;
            Place(
                icon.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(-78f, -20f),
                new Vector2(PersonIconSize, PersonIconSize),
                new Vector2(0.5f, 0.5f));

            countText = CreateText(topPrompt.transform, "Count", "0 / 6", CountFontSize);
            Place(
                countText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(24f, 0f),
                new Vector2(220f, 56f),
                new Vector2(0.5f, 1f));

            statusText = CreateText(topPrompt.transform, "Status", FormatStatus(string.Empty), StatusFontSize);
            Place(
                statusText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -56f),
                new Vector2(920f, 40f),
                new Vector2(0.5f, 1f));

            playerList = CreateRect(transform, "PlayerList").gameObject;
            Place(
                playerList.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(48f, -TopPadding),
                new Vector2(420f, 420f),
                new Vector2(0f, 1f));

            for (var index = 0; index < MaxPlayers; index++)
            {
                BuildRow(playerList.transform, index);
            }
        }

        private static void BuildRow(Transform parent, int index)
        {
            var row = CreateRect(parent, $"Row{index}");
            Place(
                row,
                new Vector2(0f, 1f),
                new Vector2(0f, -index * 56f),
                new Vector2(420f, 52f),
                new Vector2(0f, 1f));

            var avatar = CreateRect(row, "Avatar");
            Place(
                avatar,
                new Vector2(0f, 0.5f),
                new Vector2(24f, 0f),
                new Vector2(AvatarSize + 6f, AvatarSize + 6f));

            var ring = CreateImage(avatar, "Ring", AccentColor, HomeUiFonts.CircleSprite);
            Stretch(ring.rectTransform);
            ring.enabled = false;

            var face = CreateImage(
                avatar,
                "Face",
                new Color(0.72f, 0.72f, 0.72f, 1f),
                HomeUiFonts.CircleSprite);
            Place(
                face.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));

            var check = CreateText(avatar, "Check", CheckMark, 22f);
            check.color = AccentColor;
            Stretch(check.rectTransform);
            check.gameObject.SetActive(false);

            var name = CreateText(row, "Name", string.Empty, NameFontSize);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.overflowMode = TextOverflowModes.Ellipsis;
            Place(
                name.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(78f, 0f),
                new Vector2(320f, 40f),
                new Vector2(0f, 0.5f));
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);

            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.font = HomeUiFonts.Apply();
            text.fontStyle = FontStyles.Normal;
            return text;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
