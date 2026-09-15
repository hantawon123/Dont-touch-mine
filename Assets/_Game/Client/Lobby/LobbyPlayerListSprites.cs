using Game.Client.Home;
using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Leader crown and friend-add plus from <c>Resources/UI</c>.
    /// </summary>
    internal static class LobbyPlayerListSprites
    {
        public const string PlusResource = "UI/Icon_Plus";
        public const string PlusGrayResource = "UI/Icon_Plus_Gray";
        public const string LeaderResource = "UI/Icon_Leader";
        public const string MicOffWhiteResource = "UI/Icon_Mic_Off_White";
        public const string SoundWhiteResource = "UI/Icon_Sound_White";
        public const string SoundGreenResource = "UI/Icon_Sound_Green";
        public const string SoundMuteResource = "UI/Icon_Sound_Mute_Gray";

        private static Sprite plus;
        private static Sprite plusGray;
        private static Sprite leader;
        private static Sprite micOffWhite;
        private static Sprite soundWhite;
        private static Sprite soundGreen;
        private static Sprite soundMute;

        public static Sprite Plus => plus ??= Resources.Load<Sprite>(PlusResource) ?? BuildPlus();

        public static Sprite PlusGray =>
            plusGray ??= Resources.Load<Sprite>(PlusGrayResource) ?? BuildPlusGray();

        public static Sprite Leader => leader ??= Resources.Load<Sprite>(LeaderResource) ?? BuildCrown();

        public static Sprite MicOffWhite =>
            micOffWhite ??= Resources.Load<Sprite>(MicOffWhiteResource);

        public static Sprite SoundWhite =>
            soundWhite ??= Resources.Load<Sprite>(SoundWhiteResource);

        public static Sprite SoundGreen =>
            soundGreen ??= Resources.Load<Sprite>(SoundGreenResource);

        public static Sprite SoundMute =>
            soundMute ??= Resources.Load<Sprite>(SoundMuteResource);

        /// <summary>
        /// Mute wins. Talking lights green only while voice is leaving.
        /// </summary>
        public static Sprite SoundOf(bool muted, bool talking)
        {
            if (muted)
            {
                return SoundMute;
            }

            return talking ? SoundGreen : SoundWhite;
        }

        private static Sprite BuildPlus()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            var center = (size - 1) * 0.5f;
            var hexRadius = center - 2f;
            var bar = 4.5f;
            var arm = 12f;
            var accent = HomeStyle.Palette.Accent;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    if (!InsideHex(dx, dy, hexRadius))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var onPlus = Mathf.Abs(dx) <= bar && Mathf.Abs(dy) <= arm
                        || Mathf.Abs(dy) <= bar && Mathf.Abs(dx) <= arm;
                    texture.SetPixel(x, y, onPlus ? Color.white : accent);
                }
            }

            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildPlusGray()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            var center = (size - 1) * 0.5f;
            var hexRadius = center - 2f;
            var bar = 4.5f;
            var arm = 12f;
            var gray = new Color(0.62f, 0.62f, 0.62f, 1f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    if (!InsideHex(dx, dy, hexRadius))
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var onPlus = Mathf.Abs(dx) <= bar && Mathf.Abs(dy) <= arm
                        || Mathf.Abs(dy) <= bar && Mathf.Abs(dx) <= arm;
                    texture.SetPixel(x, y, onPlus ? gray : Color.clear);
                }
            }

            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildCrown()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            var accent = HomeStyle.Palette.Accent;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, InsideCrown(x, y, size) ? accent : Color.clear);
                }
            }

            texture.Apply(false, false);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool InsideHex(float dx, float dy, float radius)
        {
            var q = ((0.57735027f * dx) - (dy / 3f)) / radius;
            var r = (dy * 2f / 3f) / radius;
            var s = -q - r;
            return Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(s)) <= 1f;
        }

        private static bool InsideCrown(int x, int y, int size)
        {
            var nx = x / (float)(size - 1);
            var ny = y / (float)(size - 1);
            if (nx < 0.12f || nx > 0.88f || ny < 0.18f || ny > 0.82f)
            {
                return false;
            }

            if (ny <= 0.38f)
            {
                return ny >= 0.22f;
            }

            var left = Mathf.Abs(nx - 0.26f);
            var mid = Mathf.Abs(nx - 0.5f);
            var right = Mathf.Abs(nx - 0.74f);
            var peak = Mathf.Min(left, Mathf.Min(mid, right));
            var height = 0.82f - (peak * 1.15f);
            return ny <= height && ny >= 0.36f;
        }
    }
}
