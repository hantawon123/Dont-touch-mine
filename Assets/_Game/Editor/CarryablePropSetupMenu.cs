using System;
using System.Collections.Generic;
using System.IO;
using Game.Client.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class CarryablePropSetupMenu
    {
        private const string MenuRoot = "Tools/Game/Interaction/";
        private const string VariantFolder = "Assets/_Game/Content/Prefabs/Carryable";

        private static readonly string[] SmallBathroomPropPrefixes =
        {
            "Bin_",
            "ToiletPaper_",
            "Toothbrush_",
            "ToothbrushCup_",
            "Toothpaste_",
            "Towel_"
        };

        private static readonly string[] SmallKitchenPropPrefixes =
        {
            "Cezve_",
            "CoffeeMachine_",
            "Cup",
            "CuttingBoard_",
            "Drainer_",
            "Hanger_",
            "Kettle",
            "Knife_",
            "Microwave_",
            "Pan",
            "Paper_",
            "Papper_",
            "Plate",
            "Salt_",
            "Spoon_",
            "Teapot_",
            "Toaster_",
            "Wineglass"
        };

        private static readonly string[] SmallRoomPropPrefixes =
        {
            "Bin",
            "Book_",
            "Box_",
            "Clock",
            "Clothes",
            "Dumbbell_",
            "Globe_",
            "Laptop_",
            "Painting_",
            "PC_01",
            "PC_Keyboard_",
            "PC_Mouse_",
            "Reward_",
            "TV_"
        };

        [MenuItem(MenuRoot + "1. Preview Recommended Carryable Props")]
        private static void PreviewRecommendedProps()
        {
            var candidates = FindRecommendedProps();
            Selection.objects = candidates.ToArray();
            Debug.Log($"Carryable preview: selected {candidates.Count} recommended props.");
        }

        [MenuItem(MenuRoot + "2. Generate Variants And Replace Recommended Props")]
        private static void GenerateVariantsAndReplaceProps()
        {
            var scene = SceneManager.GetActiveScene();
            var candidates = FindRecommendedProps();
            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Carryable Props",
                    "현재 씬에서 적용할 추천 소품을 찾지 못했습니다.",
                    "확인");
                return;
            }

            var sourcePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var sourcePath = GetSourcePrefabPath(candidate);
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    sourcePaths.Add(sourcePath);
                }
            }

            if (!EditorUtility.DisplayDialog(
                    "Carryable Props",
                    $"{VariantFolder}에 Variant {sourcePaths.Count}개를 생성하고\n" +
                    $"{scene.name} 씬의 소품 {candidates.Count}개를 교체합니다.",
                    "적용",
                    "취소"))
            {
                return;
            }

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Carryable Prop Variants");
            EnsureVariantFolder();
            var variantsBySourcePath = new Dictionary<string, GameObject>(
                StringComparer.Ordinal);
            var replacedCount = 0;
            var skippedCount = 0;

            foreach (var sourcePath in sourcePaths)
            {
                try
                {
                    var variant = GetOrCreateVariant(sourcePath);
                    if (variant != null)
                    {
                        variantsBySourcePath.Add(sourcePath, variant);
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Exception exception)
                {
                    skippedCount++;
                    Debug.LogException(exception);
                }
            }

            foreach (var candidate in candidates)
            {
                try
                {
                    var sourcePath = GetSourcePrefabPath(candidate);
                    if (variantsBySourcePath.TryGetValue(sourcePath, out var variant) &&
                        ReplaceInstance(candidate, variant, scene))
                    {
                        replacedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Exception exception)
                {
                    skippedCount++;
                    Debug.LogException(exception);
                }
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog(
                "Carryable Props",
                $"Variant {variantsBySourcePath.Count}개, 씬 교체 {replacedCount}개, " +
                $"실패 또는 제외 {skippedCount}개",
                "확인");
        }

        private static List<GameObject> FindRecommendedProps()
        {
            var scene = SceneManager.GetActiveScene();
            var candidates = new HashSet<GameObject>();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return new List<GameObject>();
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(
                        transform.gameObject);
                    if (instanceRoot != transform.gameObject ||
                        instanceRoot.GetComponent<CarryableItem>() != null ||
                        !IsRecommendedProp(instanceRoot))
                    {
                        continue;
                    }

                    candidates.Add(instanceRoot);
                }
            }

            var topLevelCandidates = new List<GameObject>();
            foreach (var candidate in candidates)
            {
                if (!HasCandidateAncestor(candidate.transform.parent, candidates))
                {
                    topLevelCandidates.Add(candidate);
                }
            }

            return topLevelCandidates;
        }

        private static bool HasCandidateAncestor(
            Transform ancestor,
            ISet<GameObject> candidates)
        {
            while (ancestor != null)
            {
                if (candidates.Contains(ancestor.gameObject))
                {
                    return true;
                }

                ancestor = ancestor.parent;
            }

            return false;
        }

        private static bool IsRecommendedProp(GameObject instanceRoot)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            var path = AssetDatabase.GetAssetPath(source).Replace('\\', '/');
            var prefabName = Path.GetFileNameWithoutExtension(path);

            if (path.Contains("/Interior/Food/", StringComparison.Ordinal) ||
                path.Contains("/Interior/Plants/", StringComparison.Ordinal))
            {
                return true;
            }

            if (prefabName.StartsWith("Chair_", StringComparison.Ordinal) ||
                prefabName.StartsWith("PC_Chair", StringComparison.Ordinal))
            {
                return true;
            }

            if (path.Contains("/Interior/Bathroom/", StringComparison.Ordinal))
            {
                return StartsWithAny(prefabName, SmallBathroomPropPrefixes);
            }

            if (path.Contains("/Interior/KitchenProps/", StringComparison.Ordinal))
            {
                return StartsWithAny(prefabName, SmallKitchenPropPrefixes);
            }

            return path.Contains("/Interior/Props/", StringComparison.Ordinal) &&
                   StartsWithAny(prefabName, SmallRoomPropPrefixes);
        }

        private static GameObject GetOrCreateVariant(string sourcePath)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"Carryable source prefab not found: {sourcePath}");
                return null;
            }

            var variantPath = $"{VariantFolder}/{source.name} Carryable.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (existing != null)
            {
                return existing;
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = PrefabUtility.InstantiatePrefab(source, previewScene) as GameObject;
                if (instance == null || !ConfigureCarryable(instance))
                {
                    Debug.LogWarning($"Carryable variant skipped: {sourcePath}", source);
                    return null;
                }

                instance.name = $"{source.name} Carryable";
                var saved = PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    variantPath,
                    out var success);
                return success ? saved : null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static bool ConfigureCarryable(GameObject target)
        {
            if (target.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                return false;
            }

            foreach (var meshCollider in target.GetComponentsInChildren<MeshCollider>(true))
            {
                meshCollider.convex = true;
            }

            var carryable = target.GetComponent<CarryableItem>();
            if (carryable == null)
            {
                carryable = target.AddComponent<CarryableItem>();
            }

            var body = target.GetComponent<Rigidbody>();
            if (body == null)
            {
                return false;
            }

            body.isKinematic = true;
            var serialized = new SerializedObject(carryable);
            serialized.FindProperty("displayName").stringValue = target.name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool ReplaceInstance(
            GameObject current,
            GameObject variant,
            Scene scene)
        {
            var replacement = PrefabUtility.InstantiatePrefab(variant, scene) as GameObject;
            if (replacement == null)
            {
                return false;
            }

            var currentTransform = current.transform;
            var replacementTransform = replacement.transform;
            replacementTransform.SetParent(currentTransform.parent, false);
            replacementTransform.SetSiblingIndex(currentTransform.GetSiblingIndex());
            replacementTransform.SetLocalPositionAndRotation(
                currentTransform.localPosition,
                currentTransform.localRotation);
            replacementTransform.localScale = currentTransform.localScale;
            replacement.name = current.name;
            replacement.SetActive(current.activeSelf);

            Undo.RegisterCreatedObjectUndo(replacement, "Create Carryable Prop");
            Undo.DestroyObjectImmediate(current);
            return true;
        }

        private static string GetSourcePrefabPath(GameObject instanceRoot)
        {
            if (instanceRoot == null)
            {
                return string.Empty;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            return source == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(source).Replace('\\', '/');
        }

        private static void EnsureVariantFolder()
        {
            if (!AssetDatabase.IsValidFolder(VariantFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Game/Content/Prefabs",
                    "Carryable");
            }
        }

        private static bool StartsWithAny(string value, IReadOnlyList<string> prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
