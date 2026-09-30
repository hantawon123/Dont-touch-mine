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

    }
}
