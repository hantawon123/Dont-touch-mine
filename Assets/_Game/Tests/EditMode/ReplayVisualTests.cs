using Game.Client.Players;
using Game.Bootstrap;
using Game.Server.Match;
using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public sealed class ReplayVisualTests
    {
        [Test]
        public void ReplayCopy_InitializesAnimatorBeforeManualPlayback()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var source = Object.Instantiate(prefab);
            ReplayVisual visual = null;
            try
            {
                visual = new ReplayVisual(source.transform, null);
                Assert.That(visual.Animator, Is.Not.Null);
                Assert.That(visual.Animator.isInitialized, Is.True);
            }
            finally
            {
                visual?.Dispose();
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void MigrationFrame_PreparesHiddenAndReusesBufferUntilRoomExit()
        {
            if (Application.isBatchMode || Screen.width <= 0 || Screen.height <= 0)
                Assert.Ignore("Requires an Editor graphics context, like the migration frame capture.");
            var root = new GameObject("Migration Presentation Test");
            try
            {
                var view = root.AddComponent<Game.Client.Cameras.HostMigrationFrameView>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var frameField = view.GetType().GetField("frame", flags);
                view.Prepare();
                var frame = frameField.GetValue(view) as RenderTexture;
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.IsCreated(), Is.True);
                Assert.That(root.GetComponentInChildren<Canvas>(true).gameObject.activeSelf, Is.False,
                    "Prewarming must not cover normal gameplay or block its UI.");
                view.Clear();
                view.Prepare();
                Assert.That(frameField.GetValue(view), Is.SameAs(frame), "Do not allocate again on each migration.");
                view.Release();
                Assert.That(frameField.GetValue(view), Is.Null);
                Assert.That(frame == null || !frame.IsCreated(), Is.True, "Release GPU memory on room exit.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LobbyCamera_OnlyBindsExplicitTarget_AndCutsToItsPlacedPosition()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab");
            var characterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            var unrelated = Object.Instantiate(characterPrefab);
            var root = Object.Instantiate(prefab);
            var placed = new GameObject("Placed Local Avatar");
            try
            {
                var camera = root.GetComponent<Game.Client.Cameras.PlayerCameraController>();
                camera.RequireExplicitFollowTarget();
                typeof(Game.Client.Cameras.PlayerCameraController).GetMethod("Start",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(camera, null);
                Assert.That(camera.FollowTarget, Is.Null, "Do not auto-bind another avatar while joining.");
                placed.transform.position = new Vector3(30f, 4f, 40f);
                placed.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                camera.SetFollowTarget(placed.transform);
                Assert.That(root.transform.position, Is.EqualTo(placed.transform.position + Vector3.up * 1.6f));
                Assert.That(Quaternion.Angle(root.transform.rotation, placed.transform.rotation), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(placed);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void MigrationCamera_RebindsDestroyedTargetWithoutResettingView()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab");
            var root = Object.Instantiate(prefab);
            var previous = new GameObject("Previous Avatar");
            var replacement = new GameObject("Restored Avatar");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(Game.Client.Cameras.PlayerCameraController);
            try
            {
                var camera = root.GetComponent<Game.Client.Cameras.PlayerCameraController>();
                camera.SetFollowTarget(previous.transform);
                type.GetField("yaw", flags).SetValue(camera, 123f);
                type.GetField("pitch", flags).SetValue(camera, -25f);
                type.GetField("isFirstPerson", flags).SetValue(camera, true);
                camera.SetCursorCaptureEnabled(false);
                var pose = new Pose(new Vector3(4f, 2f, 8f), Quaternion.Euler(-25f, 123f, 0f));
                root.transform.SetPositionAndRotation(pose.position, pose.rotation);
                camera.SetMigrationSuspended(true);
                Object.DestroyImmediate(previous);
                replacement.transform.position = new Vector3(4f, 0.4f, 8f);
                camera.SetFollowTarget(replacement.transform, preserveView: true);
                type.GetMethod("LateUpdate", flags).Invoke(camera, null);
                Assert.That(camera.FollowTarget, Is.EqualTo(replacement.transform));
                Assert.That(root.transform.position, Is.EqualTo(pose.position), "Keep the last view during recovery.");
                Assert.That(type.GetField("yaw", flags).GetValue(camera), Is.EqualTo(123f));
                Assert.That(type.GetField("pitch", flags).GetValue(camera), Is.EqualTo(-25f));
                Assert.That(type.GetField("isFirstPerson", flags).GetValue(camera), Is.True);
                Assert.That(type.GetField("cursorCaptureEnabled", flags).GetValue(camera), Is.False);
                camera.SetMigrationSuspended(false);
                type.GetMethod("LateUpdate", flags).Invoke(camera, null);
                Assert.That(Quaternion.Angle(root.transform.rotation, pose.rotation), Is.LessThan(0.01f));
            }
            finally
            {
                if (previous != null) Object.DestroyImmediate(previous);
                Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(root);
            }
        }

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
