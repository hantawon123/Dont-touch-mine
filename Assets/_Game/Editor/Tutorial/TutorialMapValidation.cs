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
            if (doors.Length != 10)
                throw new InvalidOperationException($"Expected 10 interior doorways, found {doors.Length}.");
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
            const float step = .5f;
            const int width = 91, depth = 59;
            var runtime = root.Find("Runtime");
            var clear = new bool[width, depth];
            var hits = new Collider[32];
            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth; z++)
                {
                    var at = new Vector3(-22.5f + x * step, 0, -14.5f + z * step);
                    int count = Physics.OverlapCapsuleNonAlloc(at + Vector3.up * .53f, at + Vector3.up * 1.55f,
                        .34f, hits, ~0, QueryTriggerInteraction.Ignore);
                    clear[x, z] = count < hits.Length;
                    for (int i = 0; i < count; i++)
                    {
                        var t = hits[i].transform;
                        if (t.IsChildOf(root) && !t.IsChildOf(runtime) && !t.name.Contains("TrainingItem"))
                            clear[x, z] = false;
                    }
                }
            Vector2Int Cell(Vector3 p) => new(Mathf.RoundToInt((p.x + 22.5f) / step), Mathf.RoundToInt((p.z + 14.5f) / step));
            var start = Cell(root.Find("Zones/01_Briefing/PlayerStart").position);
            if (!clear[start.x, start.y])
                throw new InvalidOperationException("Player start is obstructed.");
            var seen = new bool[width, depth];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            seen[start.x, start.y] = true;
            while (queue.Count != 0)
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
            foreach (var point in new[] { new Vector3(-3,0,8), new Vector3(10,0,8), new Vector3(-9,0,-10),
                new Vector3(8,0,-10), new Vector3(20,0,-12) })
            {
                var cell = Cell(point);
                if (!seen[cell.x, cell.y])
                    throw new InvalidOperationException($"No standing route from briefing to {point}.");
            }
            Debug.Log("[Tutorial] Geometry checks: 10 clear doorways, continuous walls, all lesson rooms reachable (0.68m capsule).");
        }
    }
}
