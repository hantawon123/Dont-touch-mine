using Game.Client.Common;
using Game.Client.Lobby;
using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class HudScreenScaleTests
    {
        [Test]
        public void Apply_ScalesWithTheShippedReferenceResolution()
        {
            var host = new GameObject("Hud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                HudScreenScale.Apply(host.GetComponent<CanvasScaler>());
                var scaler = host.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(HudScreenScale.ScaledReference));
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(HudScreenScale.WidthOrHeight));
                Assert.That(HudScreenScale.OverallSize, Is.EqualTo(0.8f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LobbyHud_UsesTheSharedScreenScale()
        {
            var host = new GameObject("LobbyHud", typeof(RectTransform), typeof(Canvas), typeof(LobbyHudView));
            try
            {
                host.GetComponent<LobbyHudView>().SendMessage("Awake");
                var scaler = host.GetComponent<CanvasScaler>();
                Assert.That(scaler, Is.Not.Null);
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(HudScreenScale.ScaledReference));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HidingIntro_UsesTheSharedScreenScale()
        {
            var host = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingIntroView.Create(host.transform);
                view.Show("햄버거");
                var scaler = view.GetComponent<CanvasScaler>();
                Assert.That(scaler, Is.Not.Null);
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(HudScreenScale.ScaledReference));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(HudScreenScale.WidthOrHeight));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ShortcutOverlay_UsesTheSharedScreenScale()
        {
            var host = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyShortcutOverlayView.Ensure(host.transform);
                var scaler = host.transform.Find(LobbyShortcutOverlayView.RootName)
                    .GetComponent<CanvasScaler>();
                Assert.That(scaler, Is.Not.Null);
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(HudScreenScale.ScaledReference));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
