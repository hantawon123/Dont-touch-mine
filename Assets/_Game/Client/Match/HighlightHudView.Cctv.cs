using System;
using Game.Client.Home;
using TMPro;
using UnityEngine;

namespace Game.Client.Match
{
    public sealed partial class HighlightHudView
    {
        private RectTransform cctvOverlay;
        private TMP_Text cctvLocation, cctvTime;
        public void SetCctvInfo(string location, double? sourceTime)
        {
            EnsureCctvLayout();
            cctvLocation.text = string.IsNullOrEmpty(location) ? "CCTV" : location;
            var seconds = sourceTime.HasValue && double.IsFinite(sourceTime.Value)
                ? (long)Math.Max(0d, sourceTime.Value) : 0L;
            cctvTime.text = $"<color=#E74C3C>●</color> REC  {seconds / 60:00}:{seconds % 60:00}";
        }

        private void EnsureCctvLayout()
        {
            if (cctvOverlay != null) return;
            cctvOverlay = CreateRect(transform, "CCTV");
            Stretch(cctvOverlay);
            cctvOverlay.SetAsFirstSibling();
            for (var i = 0; i < 4; i++)
            {
                var x = i % 2;
                var y = i / 2;
                var anchor = new Vector2(x, y);
                var corner = CreateRect(cctvOverlay, "Corner" + i);
                Place(corner, anchor, new Vector2(x == 0 ? 24 : -24, y == 0 ? 24 : -24),
                    new Vector2(44, 44), anchor);
                var horizontal = CreateImage(corner, "Horizontal", Color.white, null);
                Place(horizontal.rectTransform, anchor, Vector2.zero, new Vector2(44, 2), anchor);
                var vertical = CreateImage(corner, "Vertical", Color.white, null);
                Place(vertical.rectTransform, anchor, Vector2.zero, new Vector2(2, 44), anchor);
            }
            cctvLocation = CreateText(cctvOverlay, "Location", "CCTV", 24, HomeUiFonts.Apply());
            cctvTime = CreateText(cctvOverlay, "RecordingTime", "REC  00:00", 20, HomeUiFonts.Apply());
            foreach (var label in new[] { cctvLocation, cctvTime })
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            Place(cctvLocation.rectTransform, new Vector2(0, 1), new Vector2(48, -40),
                new Vector2(370, 34), new Vector2(0, 1));
            Place(cctvTime.rectTransform, new Vector2(0, 1), new Vector2(48, -77),
                new Vector2(300, 28), new Vector2(0, 1));
            cctvOverlay.gameObject.SetActive(shown);
        }
    }
}
