using Game.Client.Cameras;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Play 중 Inspector에서 맞춘 1인칭 팔·물건 설정을 PlayerCameraRig 프리팹에 저장한다.
    /// Play 모드에서 바꾼 값은 Stop하면 사라지므로, 값을 맞춘 뒤 이 메뉴를 한 번 누르면 된다.
    /// </summary>
    public static class FirstPersonSettingsSaveMenu
    {
        private const string PrefabPath = "Assets/_Game/Content/Prefabs/PlayerCameraRig.prefab";
        private static readonly string[] SavedFields = { "firstPersonArms", "firstPersonHold" };

        [MenuItem("Game/First Person/Save Arm And Hold Settings To Prefab")]
        public static void SaveToPrefab()
        {
            var live = Object.FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
            if (live == null)
            {
                EditorUtility.DisplayDialog("1인칭 설정 저장", "씬에 PlayerCameraController가 없습니다. Play 중 매치 씬에서 실행하세요.", "OK");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var target = prefab != null ? prefab.GetComponent<PlayerCameraController>() : null;
            if (target == null)
            {
                EditorUtility.DisplayDialog("1인칭 설정 저장", $"프리팹을 찾지 못했습니다: {PrefabPath}", "OK");
                return;
            }

            var source = new SerializedObject(live);
            var destination = new SerializedObject(target);
            foreach (var field in SavedFields)
            {
                var property = source.FindProperty(field);
                if (property == null) continue;
                destination.CopyFromSerializedProperty(property);
            }

            destination.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FirstPerson] 팔·물건 설정을 프리팹에 저장했습니다: {PrefabPath}", target);
            EditorUtility.DisplayDialog("1인칭 설정 저장",
                "First Person Arms / First Person Hold 값을 PlayerCameraRig 프리팹에 저장했습니다.\nStop해도 유지됩니다.", "OK");
        }
    }
}
