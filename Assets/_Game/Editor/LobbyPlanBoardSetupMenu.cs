using System.Collections.Generic;
using System.Linq;
using Game.Client.Interactions;
using Game.Client.Lobby;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 로비 씬의 서벽 작업대(공구판 <c>Basement_ToolBoard</c> + 책상 <c>Basement_Desk</c>)를
    /// 하나의 작전 계획판으로 만든다. 두 소품의 렌더러 바운드를 합쳐 BoxCollider와
    /// <see cref="LobbyPlanBoardInteractable"/>을 가진 씬 오브젝트를 놓고,
    /// 두 소품의 렌더러를 <see cref="InteractableFocusOutline"/>의 실루엣 대상으로 연결한다.
    /// 환경 프리팹은 건드리지 않으므로 라이트맵 재베이크가 필요 없다.
    /// </summary>
    public static class LobbyPlanBoardSetupMenu
    {
        private const string MenuPath = "Game/Lobby/Place Plan Board (ToolBoard + Desk)";
        private const string LobbyScenePath = "Assets/_Game/Content/Scenes/Lobby.unity";
        private const string EnvironmentRootName = "LobbyBasementEnvironment";
        private const string BoardSourceName = "Basement_ToolBoard";
        private const string DeskSourceName = "Basement_Desk";
        private const string BoardObjectName = "LobbyPlanBoard";

        // 소품 자체 콜라이더보다 먼저 맞아야 조준 대상이 되므로 폭·깊이를 조금 키운다.
        private const float ColliderPadding = 0.03f;

        // 서벽 콜라이더(x -3.10~-3.00) 바깥 면에서 시작해 책상 앞면(-2.32)보다 조금 앞까지.
        // 공구판·걸린 공구·책상 위 고정 공구의 콜라이더가 모두 이 안에 들어가 광선이 판을 먼저 맞는다.
        private const float WallOuterX = -3.10f;

        // 상시 표시라 조준 시에만 켜지는 소품(주황 2 px)과 구분되게 흰색·굵게.
        private static readonly Color OutlineColor = Color.white;
        private const float OutlinePixels = 5f;

        // 공중 라벨: 작업대 위 공간에 "ROOM SETTING"과 아래 화살표. 선반 위 상자(y 2.24)보다 위.
        private const string LabelObjectName = "Label";
        private const string LabelText = "ROOM SETTING";
        private const string ArrowText = "▼";
        private const float LabelHeightAboveTop = 0.55f;
        private const float LabelFontSize = 2.1f;
        private const float ArrowFontSize = 2.2f;
        private const float ArrowOffsetY = -0.3f;
        private const string LabelFontPath = "Assets/_Game/Content/Fonts/Paperlogy-7Bold SDF.asset";
        private const string LabelMaterialPath = "Assets/_Game/Content/Materials/MAT_PlanBoardLabel.mat";

        // 작업대는 서벽(x -3)에 붙어 있어 정면은 방 안쪽 +X. 라벨은 이쪽을 향해 고정한다(카메라 추적 없음).
        private static readonly Vector3 LabelFacing = Vector3.right;

        // 순백(1.0)은 포스트프로세스 톤매핑에 눌려 회색빛으로 보여 HDR 밝기를 준다. 블룸이 살짝 얹힌다.
        private static readonly Color LabelFaceColor = new(1.8f, 1.8f, 1.8f, 1f);
        private const float LabelFaceDilate = 0.12f;
        private const float LabelOutlineWidth = 0.22f;
        private static readonly Color LabelOutlineColor = new(0.05f, 0.05f, 0.05f, 1f);

        // 책상 아래(플라스틱통·종이상자)와 선반 위(Fragile 상자)에는 집을 수 있는 소품이 있다.
        // 콜라이더가 그것들을 가리면 집을 수 없으므로 상판 표면부터 선반 밑면까지만 덮는다.
        // (선반 윗면 -2 cm까지 덮었을 때는 눈높이에서 위로 올려 보는 광선이 앞면에 먼저 걸려
        // 선반 위 상자를 집을 수 없었다.)

        [MenuItem(MenuPath)]
        public static void PlacePlanBoard()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != LobbyScenePath)
            {
                if (!EditorUtility.DisplayDialog("Lobby Plan Board",
                        $"활성 씬이 Lobby가 아닙니다.\n{LobbyScenePath} 를 열고 진행할까요?", "열기", "취소") ||
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            }

            var root = GameObject.Find(EnvironmentRootName);
            var board = FindProp(root, BoardSourceName);
            var desk = FindProp(root, DeskSourceName);
            if (board == null || desk == null)
            {
                EditorUtility.DisplayDialog("Lobby Plan Board",
                    $"'{EnvironmentRootName}/**/{BoardSourceName}' 또는 '{DeskSourceName}' 을 찾지 못했습니다.", "확인");
                return;
            }

            var sources = VisibleRenderers(board).Concat(VisibleRenderers(desk)).ToArray();
            if (sources.Length == 0)
            {
                EditorUtility.DisplayDialog("Lobby Plan Board", "작업대에 렌더러가 없습니다.", "확인");
                return;
            }

            var boardBounds = Encapsulate(VisibleRenderers(board).Select(r => r.bounds));
            var deskBounds = Encapsulate(VisibleRenderers(desk).Select(r => r.bounds));
            var deskTopY = DeskTopSurfaceY(desk, boardBounds.min.y);

            var min = new Vector3(WallOuterX, deskTopY,
                Mathf.Min(boardBounds.min.z, deskBounds.min.z) - ColliderPadding);
            var max = new Vector3(Mathf.Max(boardBounds.max.x, deskBounds.max.x) + ColliderPadding,
                ShelfUndersideY(desk, Mathf.Max(boardBounds.max.y, deskBounds.max.y)),
                Mathf.Max(boardBounds.max.z, deskBounds.max.z) + ColliderPadding);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);

            var existing = GameObject.Find(BoardObjectName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var planBoard = new GameObject(BoardObjectName);
            Undo.RegisterCreatedObjectUndo(planBoard, "Place Lobby Plan Board");
            planBoard.transform.position = bounds.center;
            var box = planBoard.AddComponent<BoxCollider>();
            box.size = bounds.size;

            var outline = planBoard.AddComponent<InteractableFocusOutline>();
            using (var serialized = new SerializedObject(outline))
            {
                var property = serialized.FindProperty("sourceRenderers");
                property.arraySize = sources.Length;
                for (var index = 0; index < sources.Length; index++)
                {
                    property.GetArrayElementAtIndex(index).objectReferenceValue = sources[index];
                }

                // 소품은 정적 배칭 대상이라 플레이 중에는 MeshFilter가 결합 메시를 가리킨다.
                // 실루엣이 원본 형태를 유지하도록 지금(에디트 모드)의 원본 메시를 함께 저장한다.
                var meshes = serialized.FindProperty("sourceMeshes");
                meshes.arraySize = sources.Length;
                for (var index = 0; index < sources.Length; index++)
                {
                    var filter = sources[index].GetComponent<MeshFilter>();
                    meshes.GetArrayElementAtIndex(index).objectReferenceValue =
                        filter != null ? filter.sharedMesh : null;
                }

                serialized.FindProperty("outlineColor").colorValue = OutlineColor;
                serialized.FindProperty("pixelWidth").floatValue = OutlinePixels;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var labelTop = Mathf.Max(boardBounds.max.y, deskBounds.max.y) + LabelHeightAboveTop;
            var labelPosition = new Vector3(
                (Mathf.Min(boardBounds.min.x, deskBounds.min.x) + Mathf.Max(boardBounds.max.x, deskBounds.max.x)) * 0.5f,
                labelTop,
                (Mathf.Min(boardBounds.min.z, deskBounds.min.z) + Mathf.Max(boardBounds.max.z, deskBounds.max.z)) * 0.5f);
            var label = CreateLabel(planBoard.transform, labelPosition);

            var interactable = planBoard.AddComponent<LobbyPlanBoardInteractable>();
            using (var serialized = new SerializedObject(interactable))
            {
                serialized.FindProperty("outline").objectReferenceValue = outline;
                serialized.FindProperty("label").objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // 바인딩 전에는 꺼져 있다가 프레젠터가 켠다. 씬에서도 꺼진 채로 저장.
            label.SetActive(false);

            Selection.activeGameObject = planBoard;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Lobby] Plan board placed over {BoardSourceName} + {DeskSourceName}: " +
                      $"x[{min.x:F2},{max.x:F2}] y[{min.y:F2},{max.y:F2}] z[{min.z:F2},{max.z:F2}], " +
                      $"outline sources {sources.Length}. 씬 저장됨.");
        }

        /// <summary>
        /// 작업대 위 공중 라벨. 제목과 화살표는 3D TextMeshPro, 재질은 전용 에셋
        /// (<see cref="LabelMaterialPath"/>)을 쓴다. 폰트 재질을 런타임 인스턴스로 바꾸면
        /// 씬에 에셋이 아닌 재질 참조가 남아 깨지므로 에셋으로 만든다.
        /// </summary>
        private static GameObject CreateLabel(Transform parent, Vector3 worldPosition)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabelFontPath) ?? TMP_Settings.defaultFontAsset;
            var material = EnsureLabelMaterial(font);

            var root = new GameObject(LabelObjectName);
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.position = worldPosition;
            // TMP 텍스트의 앞면은 -Z이므로 +Z를 정면의 반대(벽 쪽)로 두면 글자가 방 안쪽을 본다.
            root.transform.rotation = Quaternion.LookRotation(-LabelFacing, Vector3.up);
            var labelComponent = root.AddComponent<LobbyPlanBoardLabel>();

            CreateText(root.transform, "Title", LabelText, LabelFontSize, Vector3.zero, new Vector2(4f, 0.5f), font, material);
            var arrow = CreateText(root.transform, "Arrow", ArrowText, ArrowFontSize,
                new Vector3(0f, ArrowOffsetY, 0f), new Vector2(1f, 0.4f), font, material);

            using (var serialized = new SerializedObject(labelComponent))
            {
                serialized.FindProperty("arrow").objectReferenceValue = arrow.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return root;
        }

        private static TextMeshPro CreateText(Transform parent, string name, string text, float fontSize,
            Vector3 localPosition, Vector2 size, TMP_FontAsset font, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSharedMaterial = material;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }

        private static Material EnsureLabelMaterial(TMP_FontAsset font)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(LabelMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(font.material) { name = "MAT_PlanBoardLabel" };
            material.SetColor("_FaceColor", LabelFaceColor);
            material.SetFloat("_FaceDilate", LabelFaceDilate);
            material.SetFloat("_OutlineWidth", LabelOutlineWidth);
            material.SetColor("_OutlineColor", LabelOutlineColor);
            AssetDatabase.CreateAsset(material, LabelMaterialPath);
            return material;
        }

        private static Transform FindProp(GameObject root, string name)
        {
            return root != null
                ? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Trim() == name)
                : null;
        }

        /// <summary>
        /// 켜져 있는 렌더러 중 LOD 그룹의 첫 단계에 속하지 않는 하위 LOD는 뺀다.
        /// 실루엣은 LOD 전환을 따라가지 않으므로 두 단계를 모두 넣으면 겹쳐 보인다.
        /// </summary>
        private static IEnumerable<Renderer> VisibleRenderers(Transform prop)
        {
            var lowerLods = new HashSet<Renderer>();
            foreach (var group in prop.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (var level = 1; level < lods.Length; level++)
                {
                    foreach (var renderer in lods[level].renderers)
                    {
                        if (renderer != null)
                        {
                            lowerLods.Add(renderer);
                        }
                    }
                }
            }

            return prop.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && !lowerLods.Contains(r));
        }

        private static Bounds Encapsulate(IEnumerable<Bounds> all)
        {
            Bounds? result = null;
            foreach (var bounds in all)
            {
                if (result == null)
                {
                    result = bounds;
                    continue;
                }

                var expanded = result.Value;
                expanded.Encapsulate(bounds);
                result = expanded;
            }

            return result ?? new Bounds();
        }

        /// <summary>
        /// 책상 상판 표면 높이: 공구판 아래에서 끝나는 책상 콜라이더 중 가장 높은 윗면.
        /// 못 찾으면 공구판 바닥을 그대로 쓴다.
        /// </summary>
        private static float DeskTopSurfaceY(Transform desk, float boardBottomY)
        {
            var tops = desk.GetComponentsInChildren<Collider>(true)
                .Select(c => c.bounds.max.y)
                .Where(y => y <= boardBottomY)
                .ToArray();
            return tops.Length > 0 ? tops.Max() : boardBottomY;
        }

        /// <summary>
        /// 선반 밑면 높이: 책상 콜라이더 중 가장 높이 올라간 것(선반)의 아랫면.
        /// 콜라이더가 없으면 주어진 윗면을 그대로 쓴다.
        /// </summary>
        private static float ShelfUndersideY(Transform desk, float fallbackTopY)
        {
            var shelf = desk.GetComponentsInChildren<Collider>(true)
                .OrderByDescending(c => c.bounds.max.y)
                .FirstOrDefault();
            return shelf != null ? shelf.bounds.min.y : fallbackTopY;
        }
    }
}
