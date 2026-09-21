using System;
using System.Collections.Generic;
using Game.Client.Character;
using Game.Client.Home;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    public readonly struct HidingWaitPlayer
    {
        public HidingWaitPlayer(
            string name,
            bool completed,
            bool current,
            string playerId = null,
            string userId = null)
        {
            Name = name ?? string.Empty;
            Completed = completed;
            Current = current;
            PlayerId = playerId ?? string.Empty;
            UserId = userId ?? string.Empty;
        }

        public string Name { get; }
        public bool Completed { get; }
        public bool Current { get; }
        public string PlayerId { get; }
        public string UserId { get; }
    }

    public interface IHidingWaitHudView
    {
        void Show(
            int completedCount,
            int totalCount,
            string hidingPlayerName,
            IReadOnlyList<HidingWaitPlayer> players,
            bool showNextTurnNotice,
            double remainingSeconds,
            double turnDurationSeconds);
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
        public const float NextTurnFontSize = 40f;
        public const string NextTurnText = "다음 숨길 차례입니다";
        public const float NameFontSize = 24f;
        public const float TopPadding = 20f;
        public const float PersonIconSize = 40f;
        public const float AvatarScale = 1.2f;
        public const float AvatarSize = 54f * AvatarScale;
        public const float AvatarTimerFontSize = 28f;
        public const string AvatarTimerName = "Timer";
        public const string AvatarTimerVeilName = "TimerVeil";
        public static readonly Color AvatarTimerVeilColor = new Color(0f, 0f, 0f, 0.5f);
        public const float RingGap = 3f;
        public const float RingThickness = 3f;
        public const float RowHeight = 84f;
        public const float RowPitch = 90f;
        public const float CheckIconWidth = 21f;
        public const float ListWidth = 630f;
        public const float ListHeight = 630f;
        private const float NameWidth = 480f;
        private const float NameHeight = 60f;
        private const float NameGap = 12f;
        public const int MaxPlayers = 6;
        public static readonly Color AccentColor = new Color(1f, 0.54f, 0.24f, 1f);
        public static readonly Color DoneNameColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        public static readonly Color PendingColor = new Color(1f, 1f, 1f, 0.42f);
        public static readonly Color DoneAvatarColor = new Color(0.42f, 0.42f, 0.42f, 1f);
        public static readonly Color RingTrackColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        public static float RingOuterSize =>
            AvatarSize + (RingGap * 2f) + (RingThickness * 2f);
        private const string PersonIconResource = "UI/ic_person";
        private const string CheckIconResource = "UI/ic_check";
        private static Sprite ringSprite;

        [SerializeField]
        private TMP_Text countText;

        [SerializeField]
        private TMP_Text statusText;

        [SerializeField]
        private TMP_Text nextTurnText;

        [SerializeField]
        private GameObject topPrompt;

        [SerializeField]
        private GameObject playerList;

        [SerializeField]
        [Tooltip("Shows the waiting HUD in the editor Game view without entering Play.")]
        private bool previewOnAwake;

        private bool shown;
        private bool previewing;
        private double remainingSeconds;
        private double turnDurationSeconds = 30d;

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

        public static float RingFillAmount(double remainingSeconds, double durationSeconds)
        {
            if (durationSeconds <= 0d)
            {
                return 1f;
            }

            var clampedRemaining = Math.Min(Math.Max(0d, remainingSeconds), durationSeconds);
            return Mathf.Clamp01((float)(1d - (clampedRemaining / durationSeconds)));
        }

        public static string FormatAvatarTimer(double remainingSeconds)
        {
            return Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds)).ToString();
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
                    3,
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
                    },
                    true,
                    30d,
                    30d);
                previewing = true;
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
            IReadOnlyList<HidingWaitPlayer> players,
            bool showNextTurnNotice,
            double remainingSeconds,
            double turnDurationSeconds)
        {
            shown = true;
            previewing = false;
            this.remainingSeconds = remainingSeconds;
            this.turnDurationSeconds = turnDurationSeconds > 0d ? turnDurationSeconds : 30d;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureLayout();
            ApplyFonts();
            ApplyProgress(completedCount, totalCount, hidingPlayerName, showNextTurnNotice);
            ApplyPlayers(players);
            SetContentVisible(true);
        }

        private void Update()
        {
            if (!previewing || !shown)
            {
                return;
            }

            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds < 0d)
            {
                remainingSeconds = turnDurationSeconds;
            }

            ApplyCurrentTurnProgress();
        }

        public void Hide()
        {
            shown = false;
            previewing = false;
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

            if (nextTurnText != null)
            {
                nextTurnText.font = font;
                nextTurnText.fontSize = NextTurnFontSize;
                nextTurnText.fontStyle = FontStyles.Normal;
                nextTurnText.color = AccentColor;
                nextTurnText.text = NextTurnText;
            }
        }

        private void ApplyProgress(
            int completedCount,
            int totalCount,
            string hidingPlayerName,
            bool showNextTurnNotice)
        {
            if (countText != null)
            {
                countText.text = FormatCount(completedCount, totalCount);
            }

            if (statusText != null)
            {
                statusText.text = FormatStatus(hidingPlayerName);
            }

            if (nextTurnText != null)
            {
                nextTurnText.gameObject.SetActive(showNextTurnNotice);
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
                PaintRow(row.transform, list[index], remainingSeconds, turnDurationSeconds);
            }
        }

        private void ApplyCurrentTurnProgress()
        {
            if (playerList == null)
            {
                return;
            }

            var fillAmount = RingFillAmount(remainingSeconds, turnDurationSeconds);
            var timerText = FormatAvatarTimer(remainingSeconds);
            for (var index = 0; index < MaxPlayers; index++)
            {
                var ring = playerList.transform.Find($"Row{index}/Avatar/Ring")?.GetComponent<Image>();
                if (ring != null && ring.enabled)
                {
                    ring.fillAmount = fillAmount;
                }

                var timer = playerList.transform.Find($"Row{index}/Avatar/{AvatarTimerName}")
                    ?.GetComponent<TMP_Text>();
                if (timer != null && timer.gameObject.activeSelf)
                {
                    timer.text = timerText;
                }
            }
        }

        private static void PaintRow(
            Transform row,
            HidingWaitPlayer player,
            double remainingSeconds,
            double turnDurationSeconds)
        {
            FitAvatar(row);
            var track = row.Find("Avatar/RingTrack")?.GetComponent<Image>();
            if (track != null)
            {
                track.enabled = player.Current;
                track.color = RingTrackColor;
            }

            var ring = row.Find("Avatar/Ring")?.GetComponent<Image>();
            if (ring != null)
            {
                ring.enabled = player.Current;
                ring.color = AccentColor;
                ring.fillAmount = player.Current
                    ? RingFillAmount(remainingSeconds, turnDurationSeconds)
                    : 0f;
            }

            var avatar = row.Find("Avatar/Face")?.GetComponent<Image>();
            var portrait = row.Find($"Avatar/Face/{AvatarFacePortrait.PortraitName}");
            var hasPortrait = portrait != null && portrait.gameObject.activeSelf &&
                              portrait.GetComponent<RawImage>() is { enabled: true };
            if (avatar != null && !hasPortrait)
            {
                avatar.color = player.Current
                    ? Color.white
                    : player.Completed
                        ? DoneAvatarColor
                        : PendingColor;
            }
            else if (avatar != null)
            {
                avatar.color = Color.white;
            }

            AvatarFaceSlot.Attach(row.Find("Avatar/Face") as RectTransform)
                ?.Follow(player.PlayerId, player.UserId);

            var dim = row.Find("Avatar/Dim")?.GetComponent<Image>();
            if (dim != null)
            {
                dim.gameObject.SetActive(player.Completed);
            }

            var check = EnsureCheckIcon(row.Find("Avatar"));
            if (check != null)
            {
                check.gameObject.SetActive(player.Completed);
            }

            var veil = EnsureAvatarTimerVeil(row.Find("Avatar"));
            var timer = EnsureAvatarTimer(row.Find("Avatar"));
            if (veil != null)
            {
                veil.gameObject.SetActive(player.Current);
            }

            if (timer != null)
            {
                timer.gameObject.SetActive(player.Current);
                if (player.Current)
                {
                    timer.text = FormatAvatarTimer(remainingSeconds);
                    veil?.transform.SetAsLastSibling();
                    timer.transform.SetAsLastSibling();
                }
            }

            var name = row.Find("Name")?.GetComponent<TMP_Text>();
            if (name != null)
            {
                name.text = player.Name;
                name.font = HomeUiFonts.ApplyRegular();
                name.fontSize = NameFontSize;
                name.fontStyle = FontStyles.Normal;
                name.color = player.Current
                    ? AccentColor
                    : player.Completed
                        ? DoneNameColor
                        : PendingColor;
            }
        }

        private static void FitAvatar(Transform row)
        {
            var rowRect = row as RectTransform;
            if (rowRect != null)
            {
                Place(
                    rowRect,
                    new Vector2(0f, 1f),
                    new Vector2(0f, -row.GetSiblingIndex() * RowPitch),
                    new Vector2(ListWidth, RowHeight),
                    new Vector2(0f, 1f));
            }

            var avatar = row.Find("Avatar") as RectTransform;
            if (avatar == null)
            {
                return;
            }

            Place(
                avatar,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(RingOuterSize, RingOuterSize),
                new Vector2(0f, 0.5f));

            EnsureRing(avatar);

            var face = avatar.Find("Face") as RectTransform;
            if (face != null)
            {
                Place(
                    face,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(AvatarSize, AvatarSize));
            }

            var dim = avatar.Find("Dim") as RectTransform;
            if (dim == null)
            {
                var dimImage = CreateImage(
                    avatar,
                    "Dim",
                    new Color(0f, 0f, 0f, 0.4f),
                    HomeUiFonts.CircleSprite);
                dim = dimImage.rectTransform;
            }

            Place(
                dim,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));

            var veil = EnsureAvatarTimerVeil(avatar);
            if (veil != null)
            {
                Place(
                    veil.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(AvatarSize, AvatarSize));
            }

            var timer = EnsureAvatarTimer(avatar);
            if (timer != null)
            {
                Place(
                    timer.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(AvatarSize, AvatarSize));
            }

            var name = row.Find("Name") as RectTransform;
            if (name != null)
            {
                Place(
                    name,
                    new Vector2(0f, 0.5f),
                    new Vector2(RingOuterSize + NameGap, 0f),
                    new Vector2(NameWidth, NameHeight),
                    new Vector2(0f, 0.5f));
            }
        }

        private static void EnsureRing(RectTransform avatar)
        {
            if (avatar == null)
            {
                return;
            }

            var track = avatar.Find("RingTrack")?.GetComponent<Image>();
            if (track == null)
            {
                track = CreateImage(avatar, "RingTrack", RingTrackColor, RingSprite);
            }

            track.sprite = RingSprite;
            ConfigureRingImage(track, RingTrackColor, false);
            track.transform.SetSiblingIndex(0);
            Place(
                track.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(RingOuterSize, RingOuterSize));

            var ring = avatar.Find("Ring")?.GetComponent<Image>();
            if (ring == null)
            {
                ring = CreateImage(avatar, "Ring", AccentColor, RingSprite);
            }

            ring.sprite = RingSprite;
            ConfigureRingImage(ring, AccentColor, true);
            ring.transform.SetSiblingIndex(1);
            Place(
                ring.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(RingOuterSize, RingOuterSize));
        }

        private static void ConfigureRingImage(Image image, Color color, bool filled)
        {
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
            }
            else
            {
                image.type = Image.Type.Simple;
            }
        }

        private static Sprite RingSprite
        {
            get
            {
                if (ringSprite != null)
                {
                    return ringSprite;
                }

                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };

                var center = (size - 1) * 0.5f;
                var outer = center - 1f;
                var inner = outer * ((RingOuterSize - (RingThickness * 2f)) / RingOuterSize);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = x - center;
                        var dy = y - center;
                        var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                        var outerAlpha = Mathf.Clamp01(outer - distance + 0.5f);
                        var innerAlpha = Mathf.Clamp01(distance - inner + 0.5f);
                        var alpha = Mathf.Min(outerAlpha, innerAlpha);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply(false, false);
                ringSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                ringSprite.hideFlags = HideFlags.HideAndDontSave;
                return ringSprite;
            }
        }

        private static Image EnsureCheckIcon(Transform avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var oldLabel = avatar.Find("Check")?.GetComponent<TMP_Text>();
            if (oldLabel != null)
            {
                oldLabel.gameObject.SetActive(false);
            }

            var check = avatar.Find("CheckIcon")?.GetComponent<Image>();
            if (check == null)
            {
                check = CreateImage(
                    avatar,
                    "CheckIcon",
                    Color.white,
                    Resources.Load<Sprite>(CheckIconResource));
                check.preserveAspect = true;
            }
            else if (check.sprite == null)
            {
                check.sprite = Resources.Load<Sprite>(CheckIconResource);
            }

            Place(
                check.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(CheckIconWidth, CheckIconWidth));
            check.transform.SetAsLastSibling();
            return check;
        }

        private static Image EnsureAvatarTimerVeil(Transform avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var veil = avatar.Find(AvatarTimerVeilName)?.GetComponent<Image>();
            if (veil == null)
            {
                veil = CreateImage(avatar, AvatarTimerVeilName, AvatarTimerVeilColor, HomeUiFonts.CircleSprite);
            }

            veil.sprite = HomeUiFonts.CircleSprite;
            veil.color = AvatarTimerVeilColor;
            veil.raycastTarget = false;
            Place(
                veil.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));
            return veil;
        }

        private static TMP_Text EnsureAvatarTimer(Transform avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var timer = avatar.Find(AvatarTimerName)?.GetComponent<TMP_Text>();
            if (timer == null)
            {
                timer = CreateText(avatar, AvatarTimerName, "0", AvatarTimerFontSize);
            }

            StyleAvatarTimer(timer);
            Place(
                timer.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));
            return timer;
        }

        private static void StyleAvatarTimer(TMP_Text timer)
        {
            timer.font = HomeUiFonts.Apply();
            timer.fontSize = AvatarTimerFontSize;
            timer.fontStyle = FontStyles.Bold;
            timer.alignment = TextAlignmentOptions.Center;
            timer.color = AccentColor;
            timer.outlineWidth = 0f;
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

            if (nextTurnText == null)
            {
                nextTurnText = transform.Find("TopPrompt/NextTurn")?.GetComponent<TMP_Text>();
            }

            if (topPrompt == null)
            {
                topPrompt = transform.Find("TopPrompt")?.gameObject;
            }

            if (playerList == null)
            {
                playerList = transform.Find("PlayerList")?.gameObject;
            }

            EnsurePlayerList();
            EnsureNextTurn();
        }

        private void EnsurePlayerList()
        {
            if (playerList == null)
            {
                return;
            }

            Place(
                playerList.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(48f, -TopPadding),
                new Vector2(ListWidth, ListHeight),
                new Vector2(0f, 1f));
        }

        private void EnsureNextTurn()
        {
            if (topPrompt == null)
            {
                return;
            }

            Place(
                topPrompt.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -TopPadding),
                new Vector2(980f, 160f),
                new Vector2(0.5f, 1f));

            if (nextTurnText != null)
            {
                return;
            }

            nextTurnText = CreateText(topPrompt.transform, "NextTurn", NextTurnText, NextTurnFontSize);
            nextTurnText.color = AccentColor;
            Place(
                nextTurnText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -104f),
                new Vector2(920f, 48f),
                new Vector2(0.5f, 1f));
            nextTurnText.gameObject.SetActive(false);
        }

        private void BuildLayout()
        {
            topPrompt = CreateRect(transform, "TopPrompt").gameObject;
            Place(
                topPrompt.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -TopPadding),
                new Vector2(980f, 160f),
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

            nextTurnText = CreateText(topPrompt.transform, "NextTurn", NextTurnText, NextTurnFontSize);
            nextTurnText.color = AccentColor;
            Place(
                nextTurnText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -104f),
                new Vector2(920f, 48f),
                new Vector2(0.5f, 1f));
            nextTurnText.gameObject.SetActive(false);

            playerList = CreateRect(transform, "PlayerList").gameObject;
            Place(
                playerList.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(48f, -TopPadding),
                new Vector2(ListWidth, ListHeight),
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
                new Vector2(0f, -index * RowPitch),
                new Vector2(ListWidth, RowHeight),
                new Vector2(0f, 1f));

            var avatar = CreateRect(row, "Avatar");
            Place(
                avatar,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(RingOuterSize, RingOuterSize),
                new Vector2(0f, 0.5f));

            var track = CreateImage(avatar, "RingTrack", RingTrackColor, RingSprite);
            Place(
                track.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(RingOuterSize, RingOuterSize));
            ConfigureRingImage(track, RingTrackColor, false);
            track.enabled = false;

            var ring = CreateImage(avatar, "Ring", AccentColor, RingSprite);
            Place(
                ring.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(RingOuterSize, RingOuterSize));
            ConfigureRingImage(ring, AccentColor, true);
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

            var dim = CreateImage(
                avatar,
                "Dim",
                new Color(0f, 0f, 0f, 0.4f),
                HomeUiFonts.CircleSprite);
            Place(
                dim.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));
            dim.gameObject.SetActive(false);

            var veil = CreateImage(
                avatar,
                AvatarTimerVeilName,
                AvatarTimerVeilColor,
                HomeUiFonts.CircleSprite);
            Place(
                veil.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));
            veil.gameObject.SetActive(false);

            var check = CreateImage(
                avatar,
                "CheckIcon",
                Color.white,
                Resources.Load<Sprite>(CheckIconResource));
            check.preserveAspect = true;
            Place(
                check.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(CheckIconWidth, CheckIconWidth));
            check.gameObject.SetActive(false);

            var timer = CreateText(avatar, AvatarTimerName, "0", AvatarTimerFontSize);
            StyleAvatarTimer(timer);
            Place(
                timer.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));
            timer.gameObject.SetActive(false);

            var name = CreateText(
                row,
                "Name",
                string.Empty,
                NameFontSize,
                HomeUiFonts.ApplyRegular());
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.overflowMode = TextOverflowModes.Ellipsis;
            Place(
                name.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(RingOuterSize + NameGap, 0f),
                new Vector2(NameWidth, NameHeight),
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
            float fontSize,
            TMP_FontAsset font = null)
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.font = font != null ? font : HomeUiFonts.Apply();
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
            rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
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
