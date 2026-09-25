using System.Collections.Generic;
using System.Text;
using Game.Training.HideSeek;
using UnityEditor;
using UnityEngine;

namespace Game.Training.EditorTools
{
    /// <summary>
    /// Tools > AI > Verify Hide-Seek Vision: builds a few temporary walls far above the map (not saved) and checks
    /// that the seeker's vision follows the player rules: nothing through walls, nothing behind or outside the
    /// camera cone, nothing smaller than 1 degree, open things are seen. Logs PASS / FAIL per case.
    /// </summary>
    public static class HideSeekVisionCheck
    {
        private static readonly Vector3 Base = new(0f, 500f, 0f);

        [MenuItem("Tools/AI/Verify Hide-Seek Vision")]
        public static void Run()
        {
            var temp = new List<GameObject>();
            var log = new StringBuilder("[HideSeek Vision Check]\n");
            var failed = 0;
            try
            {
                var floor = Block(temp, Base + new Vector3(0f, -0.05f, 0f), new Vector3(60f, 0.1f, 60f));
                var mask = Physics.DefaultRaycastLayers;
                var eye = Base + Vector3.up * HideSeekRules.StandEyeHeight;
                var half = new Vector3(0.2f, 0.11f, 0.15f);
                Physics.SyncTransforms();

                void Case(string name, bool expected, bool actual)
                {
                    var ok = expected == actual;
                    if (!ok) failed++;
                    log.AppendLine($"  {(ok ? "PASS" : "FAIL")} {name}: expected {(expected ? "seen" : "not seen")}, got {(actual ? "seen" : "not seen")}");
                }

                bool See(Vector3 bottom, float yaw = 0f, float pitch = 0f, Vector3? h = null) =>
                    HideSeekVision.CanSeeBox(eye, yaw, pitch, bottom, h ?? half, mask);

                Case("open floor 5 m ahead", true, See(Base + new Vector3(0f, 0f, 5f)));
                Case("5 m behind", false, See(Base + new Vector3(0f, 0f, -5f)));
                Case("40 deg to the side (inside 91 deg cone)", true, See(Base + Quaternion.Euler(0f, 40f, 0f) * Vector3.forward * 5f));
                Case("55 deg to the side (outside the cone)", false, See(Base + Quaternion.Euler(0f, 55f, 0f) * Vector3.forward * 5f));
                Case("beyond 20 m", false, See(Base + new Vector3(0f, 0f, 21f)));
                Case("tiny prop at 19 m (under 1 deg)", false, See(Base + new Vector3(0f, 0f, 19f), h: new Vector3(0.05f, 0.05f, 0.05f)));
                Case("floor 1 m ahead with level gaze (below the 30 deg half cone)", false, See(Base + new Vector3(0f, 0f, 1f)));
                Case("floor 1 m ahead while looking down (scan pitch -45 deg)", true, See(Base + new Vector3(0f, 0f, 1f), pitch: -45f));

                var wall = Block(temp, Base + new Vector3(0f, 1.5f, 2.5f), new Vector3(4f, 3f, 0.2f));
                Physics.SyncTransforms();
                Case("behind a wall", false, See(Base + new Vector3(0f, 0f, 5f)));
                Case("body behind a wall", false, HideSeekVision.CanSeeBody(eye, 0f, 0f, Base + new Vector3(0f, 0f, 5f), HideSeekRules.BodyHeight, mask));
                Object.DestroyImmediate(wall);
                temp.Remove(wall);

                var box = Block(temp, Base + new Vector3(0f, 0.4f, 4.4f), new Vector3(1f, 0.8f, 0.4f));
                Physics.SyncTransforms();
                Case("low prop behind a crate (crate occludes, props count)", false, See(Base + new Vector3(0f, 0f, 5f)));
                Case("body behind the crate (head still visible)", true, HideSeekVision.CanSeeBody(eye, 0f, 0f, Base + new Vector3(0f, 0f, 5f), HideSeekRules.BodyHeight, mask));
                Object.DestroyImmediate(box);
                temp.Remove(box);
                Physics.SyncTransforms();
                Case("open again after removing the crate", true, See(Base + new Vector3(0f, 0f, 5f)));
                Object.DestroyImmediate(floor);
                temp.Remove(floor);
            }
            finally
            {
                foreach (var go in temp)
                {
                    if (go != null) Object.DestroyImmediate(go);
                }

                Physics.SyncTransforms();
            }

            log.Insert(0, failed == 0 ? "ALL PASS " : $"{failed} FAILED ");
            if (failed == 0) Debug.Log(log.ToString());
            else Debug.LogError(log.ToString());
        }

        private static GameObject Block(List<GameObject> temp, Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.hideFlags = HideFlags.DontSave;
            go.transform.position = centre;
            go.transform.localScale = size;
            temp.Add(go);
            return go;
        }
    }
}
