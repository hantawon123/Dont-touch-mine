using System;
using System.Collections.Generic;
using Game.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Wires the open scene's spawn points into a
    /// <see cref="MatchSceneConfiguration"/>.
    /// </summary>
    /// <remarks>
    /// Done from a menu rather than by hand because the failure is quiet. A
    /// configuration that is short a point, or has an empty slot, throws while
    /// the scene is being read, and the session deliberately swallows that and
    /// falls back to a ring around the origin. The characters then stand in a
    /// heap with nothing in the console pointing at the cause.
    /// <para>
    /// World objects are not wired. Nothing in the match rules names an object
    /// id yet, so an id invented here would be a contract no one agreed to.
    /// </para>
    /// </remarks>
    public static class MatchSceneSetupMenu
    {
        private const string MenuPath = "Game/Setup/Wire Match Scene Configuration";
        private const string SpawnParentName = "SpawnPoints";
        private const string DialogTitle = "Match Scene";

        [MenuItem(MenuPath)]
        public static void WireMatchSceneConfiguration()
        {
            var scene = EditorSceneManager.GetActiveScene();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "맵 씬(Playground 등)을 연 뒤 다시 실행하세요.",
                    "OK");
                return;
            }

            if (!TryFindSpawnPoints(scene, out var parent, out var points))
            {
                // The scene is named because the usual cause is having the wrong
                // one open, not a missing object in the right one.
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    $"열린 씬 '{scene.name}'({scene.rootCount}개 루트 오브젝트)에서 " +
                    $"'{SpawnParentName}'을 찾지 못했거나 자식이 없습니다.\n\n" +
                    "이 씬이 맞는 맵인지 확인하세요. Playground 라면 Floor1, Room_F1, " +
                    "SpawnPoints 등이 Hierarchy 에 보여야 합니다.",
                    "OK");
                return;
            }

            var configuration = parent.GetComponent<MatchSceneConfiguration>();

            if (configuration == null)
            {
                configuration = Undo.AddComponent<MatchSceneConfiguration>(parent);
            }

            Assign(configuration, points);

            // Validated with the component's own rule rather than a copy of it,
            // so this cannot drift from what the session will accept.
            var problem = Describe(configuration);

            EditorSceneManager.MarkSceneDirty(scene);

            if (problem != null)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    $"스폰 지점 {points.Count}개를 연결했지만 아직 사용할 수 없습니다.\n\n" +
                    $"{problem}\n\n씬을 저장하기 전에 고치세요.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                DialogTitle,
                $"'{parent.name}'에 스폰 지점 {points.Count}개를 연결했습니다.\n" +
                "씬을 저장하세요.",
                "OK");
        }

        /// <summary>
        /// Collects the spawn markers, ordered by name so that the numbering in
        /// the scene is the order the configuration reports.
        /// </summary>
        /// <remarks>
        /// Walks the scene's own roots rather than calling
        /// <c>GameObject.Find</c>, which skips inactive objects and searches
        /// every loaded scene. A holder that someone disabled would otherwise
        /// report as missing, and a match found in another open scene would be
        /// wired into the wrong map.
        /// </remarks>
        private static bool TryFindSpawnPoints(
            Scene scene, out GameObject parent, out List<Transform> points)
        {
            points = new List<Transform>();
            parent = null;

            foreach (var root in scene.GetRootGameObjects())
            {
                var holder = FindByName(root.transform, SpawnParentName);

                if (holder == null)
                {
                    continue;
                }

                parent = holder.gameObject;

                foreach (Transform child in holder)
                {
                    points.Add(child);
                }

                points.Sort(CompareByName);
                return points.Count > 0;
            }

            return false;
        }

        private static Transform FindByName(Transform current, string name)
        {
            if (string.Equals(current.name, name, StringComparison.Ordinal))
            {
                return current;
            }

            foreach (Transform child in current)
            {
                var found = FindByName(child, name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static int CompareByName(Transform left, Transform right)
        {
            return string.Compare(left.name, right.name, StringComparison.Ordinal);
        }

        /// <summary>
        /// Writes the list through <c>SerializedObject</c> so the change is
        /// undoable and the fields can stay private.
        /// </summary>
        private static void Assign(
            MatchSceneConfiguration configuration, List<Transform> points)
        {
            var serialized = new SerializedObject(configuration);
            var array = serialized.FindProperty("spawnPoints");

            array.arraySize = points.Count;

            for (var index = 0; index < points.Count; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue = points[index];
            }

            serialized.ApplyModifiedProperties();
        }

        /// <summary>
        /// The reason the configuration would be rejected, or null when it is
        /// usable.
        /// </summary>
        private static string Describe(MatchSceneConfiguration configuration)
        {
            try
            {
                configuration.CaptureSpawnPoses();
                return null;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }
    }
}
