using System;
using System.Linq;
using Game.Client.Interactions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Marks only fixed, opaque lobby geometry for build-time batching.</summary>
    public static class LobbyStaticBatchingMenu
    {
        public const string EnvironmentPath = "Assets/_Game/Content/Prefabs/LobbyBasementEnvironment.prefab";

        [MenuItem("Game/Lobby/Rendering/Apply Static Background Batching")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(EnvironmentPath);
            try
            {
                var included = 0;
                var excluded = 0;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    var eligible = IsFixedOpaqueGeometry(renderer, root.transform);
                    var next = eligible ? flags | StaticEditorFlags.BatchingStatic
                        : flags & ~StaticEditorFlags.BatchingStatic;
                    if (next != flags) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, next);
                    if (eligible) included++; else excluded++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath);
                Debug.Log($"[LobbyBatching] fixed={included}, excluded={excluded}. Lighting and materials unchanged.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static bool IsFixedOpaqueGeometry(MeshRenderer renderer, Transform root)
        {
            if (renderer.GetComponent<MeshFilter>()?.sharedMesh == null) return false;
            // Do not freeze interactable props, animated hierarchies, or scripted transforms.
            for (var node = renderer.transform; node != null; node = node.parent)
            {
                if (node.GetComponent<CarryableItem>() != null || node.GetComponent<Rigidbody>() != null ||
                    node.GetComponent<Animator>() != null || node.GetComponent<Animation>() != null ||
                    node.GetComponents<MonoBehaviour>().Length != 0) return false;
                if (node == root) break;
            }
            var materials = renderer.sharedMaterials;
            return materials.Length > 0 && materials.All(material => material != null &&
                material.renderQueue <= 2500 &&
                !string.Equals(material.GetTag("DisableBatching", false, "False"), "True", StringComparison.OrdinalIgnoreCase));
        }
    }
}
