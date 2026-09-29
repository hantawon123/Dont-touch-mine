using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 선택한 모듈 조각(천장 타일·벽·바닥)을 자기 크기만큼 옆으로 밀어 복제한다.
    /// 복제본이 선택되므로 단축키를 연타하면 한 줄로 이어 붙일 수 있다.
    /// </summary>
    /// <remarks>
    /// 이동량은 조각의 메시 바운드를 로컬 축(right/forward/up)으로 잰 길이다. 회전된 타일도
    /// 자기 방향 기준으로 붙는다. 프리팹 인스턴스는 프리팹 연결을 유지한 채 복제한다.
    /// 단축키: Alt+Shift+방향키(수평), Alt+Shift+PageUp/PageDown(수직).
    /// </remarks>
    public static class DuplicateAdjacentMenu
    {
        private const string Menu = "Game/Match Map/Duplicate Adjacent/";

        [MenuItem(Menu + "+X (Right) &#RIGHT")]
        public static void PlusX() => DuplicateAlong(Vector3.right);

        [MenuItem(Menu + "-X (Left) &#LEFT")]
        public static void MinusX() => DuplicateAlong(Vector3.left);

        [MenuItem(Menu + "+Z (Forward) &#UP")]
        public static void PlusZ() => DuplicateAlong(Vector3.forward);

        [MenuItem(Menu + "-Z (Back) &#DOWN")]
        public static void MinusZ() => DuplicateAlong(Vector3.back);

        [MenuItem(Menu + "+Y (Up) &#PGUP")]
        public static void PlusY() => DuplicateAlong(Vector3.up);

        [MenuItem(Menu + "-Y (Down) &#PGDN")]
        public static void MinusY() => DuplicateAlong(Vector3.down);

        private static void DuplicateAlong(Vector3 localDirection)
        {
            var sources = Selection.gameObjects.Where(go => go.scene.IsValid()).ToArray();
            if (sources.Length == 0)
            {
                Debug.LogWarning("[DuplicateAdjacent] 씬 오브젝트를 선택하세요.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Duplicate Adjacent");
            var created = new GameObject[sources.Length];
            for (var i = 0; i < sources.Length; i++)
            {
                created[i] = DuplicateOne(sources[i], localDirection);
            }

            Selection.objects = created;
        }

        private static GameObject DuplicateOne(GameObject source, Vector3 localDirection)
        {
            var size = LocalExtent(source, localDirection);
            var offset = source.transform.TransformDirection(localDirection) * size;

            GameObject copy;
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(source);
            if (prefabSource != null && PrefabUtility.IsAnyPrefabInstanceRoot(source))
            {
                copy = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource, source.scene);
                copy.transform.SetParent(source.transform.parent, false);
                copy.transform.localScale = source.transform.localScale;
            }
            else
            {
                copy = Object.Instantiate(source, source.transform.parent);
                copy.name = source.name;
            }

            copy.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation);
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Adjacent");
            return copy;
        }

        /// <summary>로컬 축 방향으로 잰 조각 길이(m). 메시 바운드 × 스케일. 렌더러가 없으면 1 m.</summary>
        private static float LocalExtent(GameObject source, Vector3 localDirection)
        {
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                return 1f;
            }

            // 자식 메시들을 루트 로컬 공간에서 합친 바운드
            var rootToLocal = source.transform.worldToLocalMatrix;
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var localToRoot = rootToLocal * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var p = new Vector3(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);
                    var q = localToRoot.MultiplyPoint3x4(p);
                    if (first)
                    {
                        bounds = new Bounds(q, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(q);
                    }
                }
            }

            var extent = Vector3.Scale(bounds.size, source.transform.lossyScale);
            var axis = new Vector3(Mathf.Abs(localDirection.x), Mathf.Abs(localDirection.y), Mathf.Abs(localDirection.z));
            var length = Vector3.Dot(extent, axis);
            return length > 0.0001f ? length : 1f;
        }
    }
}
