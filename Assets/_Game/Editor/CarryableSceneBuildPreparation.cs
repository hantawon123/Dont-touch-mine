using System;
using System.Collections.Generic;
using System.Linq;
using Game.Client.Interactions;
using Game.Bootstrap;
using Game.SOAP.Config;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    // Preparation is paid once by the build machine, not during the loading cover
    // or after the player starts moving. Authored scenes/prefabs are never saved here.
    public sealed class CarryableSceneBuildPreparation : IProcessSceneWithReport
    {
        public int callbackOrder => 100;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !scene.GetRootGameObjects().Any(root =>
                    root.GetComponentInChildren<PlaygroundLifetimeScope>(true) != null)) return;
            Prepare(scene);
            if (report.summary.platformGroup == BuildTargetGroup.Standalone &&
                EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Server)
                Debug.Log($"[ServerBuild] {scene.name}: deferred {PrepareDeferredBodies(scene)} idle carryable bodies.");
        }

        public static int Prepare(Scene scene)
        {
            ItemCatalogSO.Load();
            var items = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CarryableItem>(true)).ToArray();
            var ids = new Dictionary<string, CarryableItem>(StringComparer.Ordinal);
            var layer = LayerMask.NameToLayer("Carryable");
            foreach (var item in items)
            {
                var id = item.ObjectId;
                if (ids.ContainsKey(id) && !item.HasExplicitObjectId)
                {
                    item.UseSceneInstanceObjectId();
                    id = item.ObjectId;
                }
                if (!ids.TryAdd(id, item)) throw new BuildFailedException("Duplicate carryable ID: " + id);
                // Same first-match and duplicate rules as PlaygroundMatchScene.CaptureUniqueItems.
                item.UseObjectId(id);
                if (layer >= 0)
                    foreach (var child in item.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
                var serialized = new SerializedObject(item);
                serialized.FindProperty("preparedCenter").vector3Value = item.PlacementCenterOffset;
                serialized.FindProperty("preparedExtents").vector3Value = item.PlacementHalfExtents;
                serialized.FindProperty("preparedScale").vector3Value = item.transform.lossyScale;
                serialized.FindProperty("preparedForBuild").boolValue = layer >= 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return items.Length;
        }

        public static int PrepareDeferredBodies(Scene scene)
        {
#if !UNITY_SERVER
            return 0;
#else
            // Keep the exact collider geometry. Bodies with custom settings or dependencies stay intact.
            var roots = scene.GetRootGameObjects();
            var joints = roots.SelectMany(r => r.GetComponentsInChildren<Joint>(true)).ToArray();
            var templateObject = new GameObject("Default body comparison") { hideFlags = HideFlags.HideAndDontSave };
            var template = templateObject.AddComponent<Rigidbody>();
            template.isKinematic = true;
            var count = 0;
            try
            {
                using var defaults = new SerializedObject(template);
                foreach (var item in roots.SelectMany(r => r.GetComponentsInChildren<CarryableItem>(true)))
                {
                    var body = item.GetComponent<Rigidbody>();
                    if (body == null || !body.isKinematic ||
                        item.transform.parent != null && item.transform.parent.GetComponentInParent<Rigidbody>() != null ||
                        joints.Any(j => j.connectedBody == body) ||
                        item.GetComponentsInChildren<Component>(true).Any(c => c is not
                            (Transform or Rigidbody or Collider or MeshFilter or MeshRenderer or LODGroup or CarryableItem)) ||
                        item.GetComponentsInChildren<Rigidbody>(true).Length != 1 ||
                        item.GetComponentsInChildren<CarryableItem>(true).Length != 1 ||
                        item.GetComponentsInChildren<Collider>(true).Any(c => c.isTrigger)) continue;
                    var staticParents = true;
                    for (var parent = item.transform.parent; parent != null; parent = parent.parent)
                        if (parent.GetComponents<Component>().Any(c => c is not
                            (Transform or Collider or MeshFilter or MeshRenderer or LODGroup)))
                        { staticParents = false; break; }
                    if (!staticParents) continue;
                    using var serializedBody = new SerializedObject(body);
                    var property = serializedBody.GetIterator();
                    var same = true;
                    var enterChildren = true;
                    while (property.Next(enterChildren))
                    {
                        enterChildren = false; // DataEquals already compares nested values; skip object-reference IDs too.
                        if (property.propertyPath is "m_ObjectHideFlags" or "m_CorrespondingSourceObject" or
                            "m_PrefabInstance" or "m_PrefabAsset" or "m_GameObject") continue;
                        var other = defaults.FindProperty(property.propertyPath);
                        if (other == null || !SerializedProperty.DataEquals(property, other)) { same = false; break; }
                    }
                    if (!same) continue;
                    using var serializedItem = new SerializedObject(item);
                    serializedItem.FindProperty("deferredBody").boolValue = true;
                    serializedItem.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Object.DestroyImmediate(body);
                    count++;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(templateObject); }
            return count;
#endif
        }

    }
}
