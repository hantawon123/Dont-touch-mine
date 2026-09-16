using System.Collections.Generic;
using Game.Client.Interactions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap
{
    // Retire only inert map geometry. Unknown behaviours (including Fusion objects),
    // cameras and scene scopes keep their whole subtree until normal scene teardown.
    internal sealed class RetiredMapCleanup
    {
        private readonly Stack<Transform> pending = new();
        private readonly List<Component> components = new();
        private readonly Scene scene;
        internal bool IsComplete => pending.Count == 0;

        internal RetiredMapCleanup(IReadOnlyList<GameObject> roots, Scene scene)
        {
            this.scene = scene;
            foreach (var root in roots)
                if (root != null) pending.Push(root.transform);
        }

        internal void Tick(System.Action<GameObject> destroy = null)
        {
            var started = Time.realtimeSinceStartupAsDouble;
            var destroyed = 0;
            var inspected = 0;
            while (pending.Count > 0 && destroyed < 64 && inspected++ < 256 &&
                   Time.realtimeSinceStartupAsDouble - started < 0.001d)
            {
                var node = pending.Pop();
                if (node == null || node.gameObject.scene != scene) continue;
                node.GetComponents(components);
                var inert = true;
                foreach (var component in components)
                    if (component is not (Transform or MeshFilter or MeshRenderer or Collider or
                        Rigidbody or LODGroup or CarryableItem)) { inert = false; break; }
                if (!inert) continue;
                // A parent with a protected descendant must remain too. Destroy only leaves;
                // Unity's normal unload removes the now-lightweight grouping transforms.
                if (node.childCount == 0)
                {
                    if (destroy == null) Object.Destroy(node.gameObject); else destroy(node.gameObject);
                    destroyed++;
                }
                else
                {
                    for (var i = 0; i < node.childCount; i++) pending.Push(node.GetChild(i));
                }
            }
        }
    }
}
