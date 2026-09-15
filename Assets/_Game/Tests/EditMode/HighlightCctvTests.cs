using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Cameras;
using Game.Client.Match;
using Game.Server.Match;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HighlightCctvTests
    {
        [Test]
        public void Camera_StaysAtMountAndSwitchesUnderFade()
        {
            var root = new GameObject("test");
            try
            {
                Transform Child(string name, Vector3 position)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(root.transform); t.position = position; return t;
                }
                var output = Child("output", Vector3.zero);
                var player = Child("actor", Vector3.zero);
                var a = Child("a", new Vector3(0, 3, -5)).gameObject.AddComponent<HighlightCctvCamera>();
                var b = Child("b", new Vector3(40, 3, -5)).gameObject.AddComponent<HighlightCctvCamera>();
                a.Configure("CAM A"); b.Configure("CAM B");
                a.transform.LookAt(Vector3.zero); b.transform.LookAt(Vector3.right * 40);
                using var director = new HighlightCameraDirector(output, output, new[] { player },
                    new SceneWorldObjectReference[0], collisionLayerMask: 0, cctvCameras: new[] { a, b });
                director.Focus(new HighlightCandidate(HighlightType.MostStunned, 0, 10, "0"));
                Assert.That(output.position, Is.EqualTo(a.transform.position));
                director.Tick(2.1f);
                player.position = Vector3.right * 40;
                director.Tick(0.3f);
                Assert.That(output.position, Is.EqualTo(a.transform.position));
                director.Tick(0.1f);
                Assert.That(director.CctvOpacity, Is.GreaterThan(0f));
                director.Tick(0.11f);
                Assert.That(output.position, Is.EqualTo(b.transform.position));
                director.Tick(0.3f);
                Assert.That(director.CctvOpacity, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Camera_KeepsAuthoredPoseAndLensThroughActionAndItemRemoval()
        {
            var root = new GameObject("test");
            try
            {
                Transform Child(string name, Vector3 position)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(root.transform); t.position = position; return t;
                }
                var output = Child("output", Vector3.zero).gameObject.AddComponent<Camera>();
                var actor = Child("actor", new Vector3(8, 0, 8));
                var item = Child("item", actor.position);
                var mount = Child("mount", new Vector3(0, 4, -5)).gameObject.AddComponent<HighlightCctvCamera>();
                mount.Configure("CAM A");
                using var director = new HighlightCameraDirector(output.transform, output.transform, new[] { actor },
                    new[] { new SceneWorldObjectReference("item", item) }, collisionLayerMask: 0,
                    cctvCameras: new[] { mount });
                director.Focus(new HighlightCandidate(HighlightType.FirstBlood,
                    new[] { new HighlightSegment(0, 10) }, "item", 5, 60, actorPlayerIndex: 0));
                director.Tick(1f);
                var wideFov = output.fieldOfView;
                director.SetPlaybackTime(4.5);
                Assert.That(output.fieldOfView, Is.EqualTo(wideFov), "Shot changes must not snap the lens.");
                director.Tick(1f);
                Assert.That(output.fieldOfView, Is.EqualTo(mount.FieldOfView));
                item.gameObject.SetActive(false);
                item.position = Vector3.left * 100;
                director.SetPlaybackTime(6);
                director.Tick(1f);
                Assert.That(output.transform.position, Is.EqualTo(mount.transform.position));
                Assert.That(Quaternion.Angle(output.transform.rotation, mount.transform.rotation), Is.LessThan(0.01f));
                Assert.That(output.fieldOfView, Is.EqualTo(mount.FieldOfView));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Camera_UsesVisibleFurnitureInsteadOfGameplayColliders()
        {
            var root = new GameObject("test");
            try
            {
                Transform Child(string name, Vector3 position)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(root.transform); t.position = position; return t;
                }
                var playerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                playerObject.transform.SetParent(root.transform);
                playerObject.transform.position = Vector3.up * 0.5f;
                playerObject.GetComponent<Collider>().enabled = false;
                var player = playerObject.transform;
                var output = Child("output", Vector3.zero);
                var blocked = Child("blocked", new Vector3(0, 3, -5)).gameObject.AddComponent<HighlightCctvCamera>();
                var clear = Child("clear", new Vector3(8, 3, 0)).gameObject.AddComponent<HighlightCctvCamera>();
                blocked.transform.LookAt(player); clear.transform.LookAt(player);
                blocked.Configure("blocked"); clear.Configure("clear");
                var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.transform.SetParent(root.transform);
                shelf.transform.position = new Vector3(0, 1.5f, -2.5f);
                shelf.transform.localScale = new Vector3(2, 3, 1);
                shelf.GetComponent<Collider>().enabled = false;
                var invisibleBlocker = Child("invisible gameplay collider", new Vector3(4, 1.5f, 0));
                invisibleBlocker.gameObject.AddComponent<BoxCollider>().size = new Vector3(2, 3, 1);
                Physics.SyncTransforms();
                Assert.That(Physics.Linecast(clear.transform.position, Vector3.up * 0.5f), Is.True);
                using var director = new HighlightCameraDirector(output, output, new[] { player },
                    new SceneWorldObjectReference[0], cctvCameras: new[] { blocked, clear });
                director.Focus(new HighlightCandidate(HighlightType.MostStunned, 0, 10, "0"));
                Assert.That(director.CctvLocation, Is.EqualTo("clear"));
                Assert.That(shelf.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(shelf.GetComponent<Renderer>().forceRenderingOff, Is.False);
                Assert.That(shelf.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Camera_KeepsClearViewEvenWhenAnotherMountBecomesCloser()
        {
            var root = new GameObject("test");
            try
            {
                Transform Child(string name, Vector3 position)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(root.transform); t.position = position; return t;
                }
                var player = Child("actor", Vector3.zero);
                var output = Child("output", Vector3.zero);
                var a = Child("a", new Vector3(0, 3, -10)).gameObject.AddComponent<HighlightCctvCamera>();
                var b = Child("b", new Vector3(10, 3, -10)).gameObject.AddComponent<HighlightCctvCamera>();
                a.Configure("A"); b.Configure("B");
                a.transform.LookAt(Vector3.zero); b.transform.LookAt(Vector3.right * 10);
                using var director = new HighlightCameraDirector(output, output, new[] { player },
                    new SceneWorldObjectReference[0], collisionLayerMask: 0, cctvCameras: new[] { a, b });
                director.Focus(new HighlightCandidate(HighlightType.MostStunned, 0, 10, "0"));
                director.Tick(2.1f);
                director.SetPlaybackTime(3);
                player.position = Vector3.right * 6;
                director.Tick(0.3f); director.Tick(0.3f);
                Assert.That(director.CctvLocation, Is.EqualTo("A"));
                Assert.That(director.CctvOpacity, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Camera_SwitchesOnlyAfterMovingReplayObjectBlocksTheSubject()
        {
            var root = new GameObject("test");
            try
            {
                Transform Child(string name, Vector3 position)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(root.transform); t.position = position; return t;
                }
                var target = Child("target", Vector3.zero);
                var output = Child("output", Vector3.zero);
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.SetParent(root.transform);
                blocker.transform.position = Vector3.right * 50;
                blocker.transform.localScale = new Vector3(2, 3, 1);
                blocker.GetComponent<Collider>().enabled = false;
                var a = Child("a", new Vector3(0, 3, -5)).gameObject.AddComponent<HighlightCctvCamera>();
                var b = Child("b", new Vector3(8, 3, 0)).gameObject.AddComponent<HighlightCctvCamera>();
                a.Configure("A"); b.Configure("B");
                a.transform.LookAt(Vector3.up * 0.5f); b.transform.LookAt(Vector3.up * 0.5f);
                using var director = new HighlightCameraDirector(output, output, new[] { target },
                    new[] { new SceneWorldObjectReference("blocker", blocker.transform) }, cctvCameras: new[] { a, b });
                director.Focus(new HighlightCandidate(HighlightType.MostStunned, 0, 10, "0"));
                director.Tick(1f);
                Assert.That(director.CctvLocation, Is.EqualTo("A"));
                blocker.transform.position = new Vector3(0, 1.5f, -2.5f);
                director.Tick(0.3f);
                Assert.That(director.CctvLocation, Is.EqualTo("A"));
                director.Tick(0.21f);
                Assert.That(director.CctvLocation, Is.EqualTo("B"));
                Assert.That(Quaternion.Angle(output.rotation, b.transform.rotation), Is.LessThan(0.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Camera_AcceptsVisibleUpperBodyAndLeavesTheShelfVisible()
        {
            var root = new GameObject("test");
            try
            {
                var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                actor.transform.SetParent(root.transform);
                actor.transform.position = Vector3.up;
                actor.transform.localScale = new Vector3(1, 2, 1);
                var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.transform.SetParent(root.transform);
                shelf.transform.position = new Vector3(0, 0.65f, -2.5f);
                shelf.transform.localScale = new Vector3(2, 1.3f, 1);
                HighlightCctvCamera Mount(string name, Vector3 position)
                {
                    var camera = new GameObject(name).AddComponent<HighlightCctvCamera>();
                    camera.transform.SetParent(root.transform);
                    camera.transform.position = position;
                    camera.transform.LookAt(Vector3.up);
                    camera.Configure(name);
                    return camera;
                }
                var partial = Mount("partial", new Vector3(0, 1.5f, -5));
                var clear = Mount("clear", new Vector3(8, 3, 0));
                var output = new GameObject("output").transform;
                output.SetParent(root.transform);
                using var director = new HighlightCameraDirector(output, output, new[] { actor.transform },
                    new SceneWorldObjectReference[0], cctvCameras: new[] { partial, clear });
                director.Focus(new HighlightCandidate(HighlightType.MostStunned, 0, 10, "0"));
                director.Tick(1f);
                Assert.That(director.CctvLocation, Is.EqualTo("partial"));
                Assert.That(shelf.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(shelf.GetComponent<Renderer>().forceRenderingOff, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Hud_PreservesHeaderAndAddsFourCornersAndSourceClock()
        {
            var root = new GameObject("test", typeof(RectTransform));
            try
            {
                var hud = HighlightHudView.Create(root.transform);
                hud.Show("물건 쟁탈전 : 민수", new[] { 0.3f, 0f });
                hud.SetCctvInfo("CAM 03 · 중앙 통로", 154);
                Assert.That(hud.transform.Find("Header/Title").GetComponent<TMP_Text>().text, Is.EqualTo("HIGHLIGHT"));
                for (var i = 0; i < 4; i++) Assert.That(hud.transform.Find("CCTV/Corner" + i), Is.Not.Null);
                Assert.That(hud.transform.Find("CCTV/RecordingTime").GetComponent<TMP_Text>().text, Does.Contain("02:34"));
                hud.Hide();
                Assert.That(hud.transform.Find("CCTV").gameObject.activeSelf, Is.False);
                hud.Show("다음", new[] { 0f });
                hud.SetCctvInfo("CAM 04", 12);
                Assert.That(hud.transform.Find("CCTV/RecordingTime").GetComponent<TMP_Text>().text, Does.Contain("00:12"));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
