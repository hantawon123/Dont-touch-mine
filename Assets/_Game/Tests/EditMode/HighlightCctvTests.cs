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
                var b = Child("b", new Vector3(20, 3, -5)).gameObject.AddComponent<HighlightCctvCamera>();
                a.Configure("CAM A"); b.Configure("CAM B");
                a.transform.LookAt(Vector3.zero); b.transform.LookAt(Vector3.right * 20);
                using var director = new HighlightCameraDirector(output, output, new[] { player },
                    new SceneWorldObjectReference[0], collisionLayerMask: 0, cctvCameras: new[] { a, b });
                director.Focus(new HighlightCandidate(HighlightType.MostStunned, 0, 10, "0"));
                Assert.That(output.position, Is.EqualTo(a.transform.position));
                director.Tick(2.1f);
                player.position = Vector3.right * 20;
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
