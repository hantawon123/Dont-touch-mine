using Game.Client.Players;
using Game.Bootstrap;
using Game.Server.Match;
using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ReplayVisualTests
    {
        [Test]
        public void HighlightHud_KeepsOnlyTitleAndNotice_AndRestoresPriorVisibility()
        {
            var root = new GameObject("HUD", typeof(Canvas));
            try
            {
                var hud = root.AddComponent<Game.Client.Match.NetworkMatchHudView>();
                var title = new GameObject("Title", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                title.transform.SetParent(root.transform);
                var notice = new GameObject("Notice", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                notice.transform.SetParent(root.transform);
                var timer = new GameObject("Timer", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                timer.transform.SetParent(root.transform);
                var hidden = new GameObject("AlreadyHidden", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                hidden.transform.SetParent(root.transform);
                hidden.GetComponent<UnityEngine.UI.Image>().enabled = false;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var timerView = timer.AddComponent<Game.Client.Match.MatchTimerView>();
                var timerLabel = new GameObject("TimerText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                timerLabel.transform.SetParent(timer.transform);
                var timerText = timerLabel.GetComponent<TMPro.TMP_Text>();
                typeof(Game.Client.Match.MatchTimerView).GetField("timerText", flags).SetValue(timerView, timerText);
                typeof(Game.Client.Match.NetworkMatchHudView).GetField("timerView", flags).SetValue(hud, timerView);
                typeof(Game.Client.Match.NetworkMatchHudView).GetField("highlightTitleText", flags)
                    .SetValue(hud, title.GetComponent<TMPro.TMP_Text>());
                typeof(Game.Client.Match.NetworkMatchHudView).GetField("destructionNoticeRoot", flags)
                    .SetValue(hud, notice);
                hud.SetPhase(Game.Core.Match.MatchPhase.Highlight, "");
                Assert.That(title.GetComponent<TMPro.TMP_Text>().enabled, Is.True);
                Assert.That(notice.GetComponent<TMPro.TMP_Text>().enabled, Is.True);
                Assert.That(timer.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                hud.SetEndCountdown(3d);
                Assert.That(timerText.enabled, Is.True);
                Assert.That(timerText.text, Is.EqualTo("00:03"));
                Assert.That(timer.GetComponent<UnityEngine.UI.Image>().enabled, Is.True);
                hud.SetEndCountdown(0d);
                Assert.That(timerText.enabled, Is.False);
                Assert.That(timer.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
                hud.SetPhase(Game.Core.Match.MatchPhase.Searching, "");
                Assert.That(timer.GetComponent<UnityEngine.UI.Image>().enabled, Is.True);
                Assert.That(hidden.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SourceClock_FollowsSpeedCutsAndRestart_AndHoldsAtEnd()
        {
            var player = new HighlightReplayPlayer(new Transform[0], new SceneWorldObjectReference[0]);
            var clips = new[]
            {
                new HighlightReplayClip(new HighlightSegment(10, 14, 2), new[]
                {
                    new HighlightReplayFrame(10, new Pose[0], new WorldObjectState[0]),
                    new HighlightReplayFrame(14, new Pose[0], new WorldObjectState[0])
                }),
                new HighlightReplayClip(new HighlightSegment(30, 32), new[]
                {
                    new HighlightReplayFrame(30, new Pose[0], new WorldObjectState[0]),
                    new HighlightReplayFrame(32, new Pose[0], new WorldObjectState[0])
                })
            };
            Assert.That(player.SourceTime, Is.Null);
            player.Start(clips);
            Assert.That(player.SourceTime, Is.EqualTo(10d));
            player.Advance(1);
            Assert.That(player.SourceTime, Is.EqualTo(12d));
            player.Advance(1);
            Assert.That(player.SourceTime, Is.EqualTo(30d));
            player.Advance(2);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(player.SourceTime, Is.EqualTo(32d));
            player.Start(clips);
            Assert.That(player.SourceTime, Is.EqualTo(10d));
        }

        [Test]
        public void ReplayCopy_HasNoPhysics_AndNeverMovesOriginal()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.AddComponent<Rigidbody>();
            var visual = new ReplayVisual(source.transform, null);
            try
            {
                visual.SetPlaying(true);
                visual.Target.position = Vector3.one * 20f;
                Assert.That(source.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(visual.Target.GetComponentInChildren<Collider>(), Is.Null);
                Assert.That(visual.Target.GetComponentInChildren<Rigidbody>(), Is.Null);
                Assert.That(source.GetComponent<Renderer>().forceRenderingOff, Is.True);
                visual.SetPlaying(false);
                Assert.That(source.GetComponent<Renderer>().forceRenderingOff, Is.False);
            }
            finally { visual.Dispose(); Object.DestroyImmediate(source); }
        }

        [Test]
        public void DeletedOriginal_RemainsReplayable_ThenDisappearsAtRecordedBoundary()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var visual = new ReplayVisual(source.transform, null);
            Object.DestroyImmediate(source);
            try
            {
                var player = new HighlightReplayPlayer(new Transform[0],
                    new[] { new SceneWorldObjectReference("item", visual.Target) });
                var frames = new[]
                {
                    new HighlightReplayFrame(0, new Pose[0], new[] { new WorldObjectState("item", new Pose(Vector3.one, Quaternion.identity)) }),
                    new HighlightReplayFrame(1, new Pose[0], new WorldObjectState[0])
                };
                player.Start(new[] { new HighlightReplayClip(new HighlightSegment(0, 1), frames) });
                Assert.That(visual.Target.gameObject.activeSelf, Is.True);
                player.Advance(1);
                Assert.That(visual.Target.gameObject.activeSelf, Is.False);
                player.Start(new[] { new HighlightReplayClip(new HighlightSegment(0, 1), frames) });
                Assert.That(visual.Target.gameObject.activeSelf, Is.True);
            }
            finally { visual.Dispose(); }
        }
    }
}
