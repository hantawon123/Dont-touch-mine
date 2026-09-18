using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Editor.Tutorial
{
    /// <summary>Checks the authored geometry, rather than just the presence of lesson objects.</summary>
    internal static class TutorialMapValidation
    {
        internal static void Validate(Transform root)
        {
            Physics.SyncTransforms();
            var architecture = root.Find("Architecture");
            var doors = architecture.GetComponentsInChildren<Transform>().Where(t => t.name == "Door").ToArray();
            if (doors.Length != 8)
                throw new InvalidOperationException($"Expected 8 route doorways, found {doors.Length}.");
            foreach (var door in doors)
            {
                for (float offset = -1; offset <= 1; offset += .25f)
                {
                    var at = door.TransformPoint(new Vector3(0, 0, offset));
                    var hits = Physics.OverlapCapsule(at + Vector3.up * .53f, at + Vector3.up * 1.55f, .34f,
                        ~0, QueryTriggerInteraction.Ignore);
                    var blocking = hits.FirstOrDefault(c => c.transform.IsChildOf(root) && !c.transform.IsChildOf(root.Find("Runtime")));
                    if (blocking != null)
                        throw new InvalidOperationException($"Door {door.parent.name} is blocked by {blocking.name} at {at}.");
                }
            }

            // Scan each wall along its entire authored span at body height. Only door apertures may be empty.
            foreach (Transform wall in architecture)
            {
                var spans = wall.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "SolidWall").ToArray();
                if (spans.Length == 0)
                    continue;
                float max = spans.Max(c => wall.InverseTransformPoint(c.bounds.center).x + c.transform.localScale.x * .5f);
                var doorCenters = wall.Cast<Transform>().Where(t => t.name == "Door").Select(t => t.localPosition.x).ToArray();
                for (float x = .2f; x < max - .1f; x += .2f)
                {
                    if (doorCenters.Any(d => Mathf.Abs(x - d) < TutorialHideoutArt.DoorWidth * .5f + .02f))
                        continue;
                    var point = wall.TransformPoint(new Vector3(x, 1.5f, 0));
                    if (!Physics.OverlapSphere(point, .055f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(c => c.name == "SolidWall" && c.transform.IsChildOf(wall)))
                        throw new InvalidOperationException($"Unintended wall gap: {wall.name} at {x:0.00}.");
                }
            }
            ValidateRoute(root);
        }

        private static void ValidateRoute(Transform root)
        {
            var runtime = root.Find("Runtime");
            bool Blocked(Vector3 at, float height)
            {
                const float radius = .27f;
                return Physics.OverlapCapsule(at + Vector3.up * (.12f + radius),
                    at + Vector3.up * (.12f + height - radius), radius, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => c.transform.IsChildOf(root) && !c.transform.IsChildOf(runtime));
            }
            foreach (var stage in new[] { (z: 2.2f, pass: 1.1f, fail: 1.67f), (z: -1.1f, pass: .56f, fail: 1.1f) })
            {
                var at = new Vector3(20, 0, stage.z);
                if (Blocked(at, stage.pass) || !Blocked(at, stage.fail))
                    throw new InvalidOperationException($"Posture clearance invalid at {at}.");
                foreach (float x in new[] { 17.6f, 18.5f, 21.5f, 22.4f })
                    if (!Blocked(new Vector3(x, 0, stage.z), .56f))
                        throw new InvalidOperationException("Posture tunnel can be bypassed from the side.");
            }
            for (float z = 5.7f; z < 14.5f; z += .5f)
                if (Physics.Raycast(new Vector3(16, 1, z), Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Jump gap has a hidden floor or foundation.");
            if (!Blocked(new Vector3(0, 0, 0), 1.67f))
                throw new InvalidOperationException("Center still provides a shortcut.");

            // Navigation includes the required low posture; the pit itself is checked separately above.
            const float step = .5f;
            const int width = 91, depth = 59;
            var clear = new bool[width, depth];
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
                clear[x, z] = !Blocked(new Vector3(-22.5f + x * step, 0, -14.5f + z * step), .56f);
            Vector2Int Cell(Vector3 p) => new(Mathf.RoundToInt((p.x + 22.5f) / step), Mathf.RoundToInt((p.z + 14.5f) / step));
            var start = Cell(root.Find("Zones/01_Briefing/PlayerStart").position);
            if (!clear[start.x, start.y])
                throw new InvalidOperationException("Player start obstructed.");
            var seen = new bool[width, depth];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            seen[start.x, start.y] = true;
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var n = at + d;
                    if (n.x < 0 || n.y < 0 || n.x >= width || n.y >= depth || seen[n.x, n.y] || !clear[n.x, n.y])
                        continue;
                    seen[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }
            foreach (var point in new[]{new Vector3(-5,0,10),new Vector3(6,0,10),new Vector3(14,0,10),
                new Vector3(20,0,10),new Vector3(20,0,2.2f),new Vector3(20,0,-1.1f),new Vector3(18,0,-8),
                new Vector3(3,0,-10),new Vector3(-6,0,-10),new Vector3(-18,0,-12),new Vector3(-17.7f,0,-1.5f),new Vector3(-19,0,3.5f)})
            {
                var cell = Cell(point);
                if (!seen[cell.x, cell.y])
                    throw new InvalidOperationException($"Route disconnected at {point}.");
            }
            Debug.Log("[Tutorial] Route checks passed: 8 doors, real pit, crouch/prone-only clearances, sealed center, 12 stations connected.");
        }
    }
}
