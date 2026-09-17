using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 저택 맵(<c>Mansion.unity</c>, Synty Horror Mansion 데모 복제본)에 매치 씬 필수 오브젝트를 심는다.
    /// 마트(<c>Supermarket.unity</c>)의 구성물을 그대로 복사해 이름 규약(<c>SpawnPoint_N</c>, <c>ShredderSpot</c> 등)을
    /// 맞추고, 데모 전용 오브젝트(BIRP 조명 묶음·데모 카메라)는 지운다. 위치는 저택 앞뜰 기준의 임시값이라
    /// 이후 사용자가 씬에서 직접 옮긴다.
    /// </summary>
    /// <remarks>
    /// 한 번만 실행하는 초기화 도구다. 이미 심어져 있으면(<c>MatchLifetimeScope</c> 존재) 중복 생성하지 않고 멈춘다.
    /// </remarks>
    public static class MansionSceneSetupMenu
    {
        private const string MenuRoot = "Game/Match Map/Mansion/";
        private const string ScenePath = "Assets/_Game/Content/Scenes/Mansion.unity";
        private const string SourceScenePath = "Assets/_Game/Content/Scenes/Supermarket.unity";
        private const string EnvironmentRootName = "MansionEnvironment";

        /// <summary>마트에서 그대로 가져오는 루트. 순서대로 복사한다.</summary>
        private static readonly string[] CopyRoots =
        {
            "MatchLifetimeScope",
            "Main Camera",
            "Shredder_A",
            "Shredder_B",
            "SpawnPoints",
            "WaitingSpawnPoints",
            // 단독 테스트 캐릭터(네트워크 없이 Play로 걸어 보기)와 그 카메라 리그. Playground·마트 씬에도 같은 이름으로 놓여 있다.
            // 리그가 없으면 Play에서 씬 카메라만 보인다. HUD는 이후 Game/InGame/Build HUD Layout (Active Scene)으로 만든다.
            "PlayerCharacter",
            "PlayerCameraRig"
        };

        /// <summary>데모 씬 전용이라 지우는 루트. 카메라는 마트 카메라 리그로 대체한다.</summary>
        private static readonly string[] RemoveDemoRoots = { "Lighting (BIRP)", "Main Camera" };

        /// <summary>
        /// 데모의 후처리 볼륨(어두운 밤 톤·비네트). 처음엔 마트처럼 밝게 가려고 껐지만 사용자가 **원본 데모 배경 그대로**를
        /// 원해(2026-09-17) 켜 둔다. 마트 볼륨은 복사하지 않는다.
        /// </summary>
        private const string DemoVolumeName = "Global Volume";
        private const string DisabledDemoVolumeName = "Demo Global Volume (off)";
        private const string MartVolumeName = "Mansion Post Volume";

        /// <summary>저택 본관 앞(북쪽, z가 큰 쪽)의 앞뜰 중심. 데모 기준 본관은 x -26~21, z -54~-7.</summary>
        private static readonly Vector3 FrontYardAnchor = new(-2.5f, 20f, -2f);
        private static readonly Vector3 BackYardAnchor = new(-2.5f, 20f, -60f);
        private const float SpawnEyeOffset = 0.85f;
        private const float GridSpacing = 2f;

        [MenuItem(MenuRoot + "1. Setup Match Objects From Supermarket")]
        public static void SetupMatchObjects()
        {
            var mansion = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (FindRoot(mansion, "MatchLifetimeScope") != null)
            {
                Debug.LogWarning("[Mansion] MatchLifetimeScope가 이미 있어 초기화를 건너뜁니다.");
                return;
            }

            var removed = RemoveDemoObjects(mansion);
            var source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            var copied = new List<string>();
            try
            {
                foreach (var name in CopyRoots)
                {
                    if (FindRoot(mansion, name) != null)
                    {
                        // 예: 2번 메뉴로 먼저 만든 파쇄기. 마트 것을 덧붙이지 않는다.
                        Debug.Log($"[Mansion] '{name}'이 이미 있어 마트 것은 복사하지 않습니다.");
                        continue;
                    }

                    var origin = FindRoot(source, name);
                    if (origin == null)
                    {
                        Debug.LogWarning($"[Mansion] 마트 씬에 '{name}'이 없어 건너뜁니다.");
                        continue;
                    }

                    var copy = Object.Instantiate(origin);
                    copy.name = name;
                    SceneManager.MoveGameObjectToScene(copy, mansion);
                    copied.Add(name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(source, removeScene: true);
            }

            RegisterTestCharacterForInjection(mansion);
            RestoreDemoLook(mansion);
            PlacePlaceholders(mansion);
            EnsureEnvironmentRoot(mansion);

            EditorSceneManager.MarkSceneDirty(mansion);
            EditorSceneManager.SaveScene(mansion);
            Debug.Log($"[Mansion] 매치 오브젝트 심기 완료: 복사 {copied.Count}개({string.Join(", ", copied)}), " +
                      $"데모 오브젝트 제거 {removed}개. 스폰·파쇄기·대기 스폰은 앞뜰 임시 위치라 씬에서 옮겨 주세요.");
        }

        /// <summary>
        /// 단독 테스트 캐릭터를 <c>MatchLifetimeScope</c>의 Auto Inject 목록에 넣는다. 목록에 없으면 VContainer 주입을 못 받아
        /// 입력·카메라가 연결되지 않고 Play에서 씬 카메라만 보인다(마트 복사본은 목록이 비어 있었음, 2026-09-17).
        /// </summary>
        private static void RegisterTestCharacterForInjection(Scene scene)
        {
            var scopeGo = FindRoot(scene, "MatchLifetimeScope");
            var player = FindRoot(scene, "PlayerCharacter");
            if (scopeGo == null || player == null)
            {
                return;
            }

            var scope = scopeGo.GetComponent<VContainer.Unity.LifetimeScope>();
            if (scope == null)
            {
                return;
            }

            var so = new SerializedObject(scope);
            var list = so.FindProperty("autoInjectGameObjects");
            if (list == null)
            {
                return;
            }

            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == player)
                {
                    return;
                }
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ 2. 선택 물건 → 파쇄기

        /// <summary>마트 파쇄기와 같은 마커 위치(로컬, 앞 = +z). 튕김 지점은 몸통 위, 목표는 앞쪽 바닥 근처.</summary>
        private static readonly Vector3 EjectionPointLocal = new(0f, 0.6f, 0.9f);
        private static readonly Vector3 EjectionTargetLocal = new(0f, 0.3f, 2.5f);

        /// <summary>
        /// 씬에 직접 배치한 물건(1~2개 선택)을 파쇄기로 바꾼다. 물건을 감싸는 루트 <c>Shredder_A/B</c>를 만들어
        /// <see cref="Game.Bootstrap.ShredderInteractable"/>과 렌더러 크기에 맞춘 BoxCollider를 붙이고,
        /// 자식 <c>ShredderSpot</c>(튕김 지점)·<c>ShredderTarget</c>(튕김 목표)을 마트와 같은 자리에 둔다.
        /// 물건의 앞면이 로컬 +z를 보게 놓여 있어야 튕김 방향이 맞다. 아니면 마커 두 개만 씬에서 옮기면 된다.
        /// </summary>
        [MenuItem(MenuRoot + "2. Adopt Selected Objects As Shredders")]
        public static void AdoptSelectedAsShredders()
        {
            var selected = Selection.gameObjects
                .Where(go => go.scene.IsValid() && go.GetComponentInParent<Game.Bootstrap.ShredderInteractable>() == null)
                .OrderBy(go => go.name)
                .ToList();
            if (selected.Count == 0 || selected.Count > 2)
            {
                Debug.LogError("[Mansion] 파쇄기로 쓸 씬 오브젝트를 1~2개 선택하세요(이미 파쇄기인 것은 제외).");
                return;
            }

            var scene = selected[0].scene;
            var made = new List<string>();
            foreach (var target in selected)
            {
                var name = new[] { "Shredder_A", "Shredder_B" }.FirstOrDefault(n => FindRoot(scene, n) == null);
                if (name == null)
                {
                    Debug.LogWarning("[Mansion] Shredder_A/B가 둘 다 있어 더 만들지 않습니다. 기존 것을 지우고 다시 실행하세요.");
                    break;
                }

                made.Add(WrapAsShredder(target, name));
            }

            if (made.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[Mansion] 파쇄기 변환: {string.Join(", ", made)}. 자식 ShredderSpot·ShredderTarget 위치를 물건 앞쪽에 맞게 확인하세요.");
            }
        }

        private static string WrapAsShredder(GameObject target, string name)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Adopt Shredder");
            SceneManager.MoveGameObjectToScene(root, target.scene);
            root.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            Undo.SetTransformParent(target.transform, root.transform, "Adopt Shredder");

            var bounds = LocalRendererBounds(root.transform, target);
            var box = Undo.AddComponent<BoxCollider>(root);
            box.center = bounds.center;
            box.size = bounds.size;

            var spot = new GameObject("ShredderSpot");
            spot.transform.SetParent(root.transform, false);
            spot.transform.localPosition = EjectionPointLocal;
            var ejectTarget = new GameObject("ShredderTarget");
            ejectTarget.transform.SetParent(root.transform, false);
            ejectTarget.transform.localPosition = EjectionTargetLocal;

            var shredder = Undo.AddComponent<Game.Bootstrap.ShredderInteractable>(root);
            var serialized = new SerializedObject(shredder);
            serialized.FindProperty("ejectionPoint").objectReferenceValue = spot.transform;
            serialized.FindProperty("ejectionTarget").objectReferenceValue = ejectTarget.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return $"{name} ← {target.name} (상자 {bounds.size.x:F2}×{bounds.size.y:F2}×{bounds.size.z:F2} m)";
        }

        /// <summary>물건의 모든 렌더러 경계를 루트 로컬 좌표로 합친 상자. 렌더러가 없으면 1 m 상자.</summary>
        private static Bounds LocalRendererBounds(Transform root, GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: false);
            if (renderers.Length == 0)
            {
                return new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one);
            }

            Bounds? local = null;
            foreach (var renderer in renderers)
            {
                var world = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = world.center + Vector3.Scale(world.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    var localPoint = root.InverseTransformPoint(point);
                    if (local.HasValue)
                    {
                        var b = local.Value;
                        b.Encapsulate(localPoint);
                        local = b;
                    }
                    else
                    {
                        local = new Bounds(localPoint, Vector3.zero);
                    }
                }
            }

            return local.Value;
        }

        // ------------------------------------------------------------------ 3. 결과 씬 엔딩 무대 연결

        private const string ResultScenePath = "Assets/_Game/Content/Scenes/MansionResult.unity";
        private const string StageName = "EndingStage";
        private const string StageEnvironmentName = "Basement";

        /// <summary>결과 씬에서 무대로 묶지 않는 루트. 스코프·오버레이 카메라는 원래 자리에 둔다.</summary>
        private static readonly string[] ResultLogicRoots = { "Result Lifetime Scope", "Main Camera" };

        // 유치장 무대와 같은 카메라 화각(EndingStageSetupMenu). 앵커는 사용자가 놓은 Main Camera 자리를 그대로 쓴다.
        private const float StageCameraFov = 40f;
        private const float StageCameraDepth = 5f;
        private const int EndingSlotCount = 6;

        /// <summary>카메라 앞으로 이만큼 떨어진 곳에 탈출 줄, 그 뒤에 체포 줄을 임시로 놓는다.</summary>
        private const float EscapeRowDistance = 4f;
        private const float ArrestRowDistance = 7f;

        /// <summary>
        /// 사용자가 <c>MansionResult.unity</c>에 직접 옮겨 둔 지하실 조각·조명을 <see cref="Game.Client.Match.EndingStage"/> 무대로 묶고
        /// 결과 흐름에 연결한다. 유치장 무대(Result 복제본에 들어 있던 <c>EndingStage</c>)는 지운다.
        /// <list type="bullet">
        /// <item>스코프·<c>Main Camera</c>를 제외한 모든 루트(방 조각 프리팹·스포트라이트·Directional Light)를 <c>EndingStage/Basement</c> 아래로.
        /// 스코프가 무대 밖 라이트를 끄기 때문에 조명은 반드시 무대 아래에 있어야 한다.</item>
        /// <item>카메라 앵커 = 사용자가 놓은 <c>Main Camera</c>의 위치·회전. 전용 <c>EndingCamera</c>가 그 자리에서 찍는다.</item>
        /// <item>탈출 자리 6·체포 자리 6은 카메라 정면 바닥 위 두 줄(임시). 씬에서 옮기면 된다.</item>
        /// </list>
        /// 이미 연결돼 있으면(무대 아래 <c>Basement</c> 존재) 다시 묶지 않고 앵커·자리만 남겨 둔다.
        /// </summary>
        [MenuItem(MenuRoot + "3. Wire Ending Stage In MansionResult")]
        public static void WireEndingStageInResultScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var result = EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Single);
            var existingStage = FindRoot(result, StageName);
            if (existingStage != null && existingStage.transform.Find(StageEnvironmentName) != null)
            {
                Debug.LogWarning("[Mansion] MansionResult에 이미 지하실 무대가 연결되어 있습니다. 다시 묶으려면 EndingStage를 지우고 실행하세요.");
                return;
            }

            var viewpoint = FindRoot(result, "Main Camera");
            var anchorPose = viewpoint != null
                ? new Pose(viewpoint.transform.position, viewpoint.transform.rotation)
                : new Pose(new Vector3(0f, 1.75f, -6.6f), Quaternion.identity);

            // 유치장 무대(및 그 아래 환경 프리팹·카메라)는 저택 엔딩에서 쓰지 않는다.
            if (existingStage != null)
            {
                Object.DestroyImmediate(existingStage);
            }

            var environmentRoots = result.GetRootGameObjects()
                .Where(go => !ResultLogicRoots.Contains(go.name))
                .ToList();

            var stageGo = new GameObject(StageName);
            SceneManager.MoveGameObjectToScene(stageGo, result);
            var stage = stageGo.AddComponent<Game.Client.Match.EndingStage>();

            var environment = new GameObject(StageEnvironmentName).transform;
            environment.SetParent(stageGo.transform, false);
            foreach (var go in environmentRoots)
            {
                go.transform.SetParent(environment, worldPositionStays: true);
            }

            var forward = Vector3.ProjectOnPlane(anchorPose.rotation * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            var yaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
            var escapeCenter = GroundBelow(anchorPose.position + forward * EscapeRowDistance);
            var arrestCenter = GroundBelow(anchorPose.position + forward * ArrestRowDistance);
            var escape = CreateSlotRow(stageGo.transform, "EscapeSpawnPoints", "EscapeSpawn_", escapeCenter, yaw + 180f, forward);
            var arrest = CreateSlotRow(stageGo.transform, "ArrestSpawnPoints", "ArrestSpawn_", arrestCenter, yaw + 180f, forward);

            var anchor = new GameObject("EndingCameraAnchor").transform;
            anchor.SetParent(stageGo.transform, false);
            anchor.SetPositionAndRotation(anchorPose.position, anchorPose.rotation);
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(stageGo.transform, false);

            var camGo = new GameObject("EndingCamera");
            camGo.transform.SetParent(stageGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = viewpoint != null && viewpoint.TryGetComponent<Camera>(out var viewCamera) ? viewCamera.fieldOfView : StageCameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.depth = StageCameraDepth;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.enabled = false; // 프레젠터가 켠다
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

            stage.Wire(cam, anchor, escape, arrest, visuals);

            var scope = result.GetRootGameObjects()
                .Select(g => g.GetComponentInChildren<Game.Bootstrap.ResultLifetimeScope>(true))
                .FirstOrDefault(sc => sc != null);
            if (scope == null)
            {
                Debug.LogError("[Mansion] MansionResult 씬에 ResultLifetimeScope가 없습니다. Result.unity의 'Result Lifetime Scope'를 복사해 넣으세요.");
            }
            else
            {
                var so = new SerializedObject(scope);
                so.FindProperty("endingStage").objectReferenceValue = stage;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(result);
            EditorSceneManager.SaveScene(result);
            Selection.activeGameObject = stageGo;
            Debug.Log($"[Mansion] 엔딩 무대 연결: 환경 루트 {environmentRoots.Count}개를 {StageName}/{StageEnvironmentName} 아래로, " +
                      $"카메라 앵커 {anchorPose.position}(화각 {cam.fieldOfView:F0}), 탈출 자리 {stage.EscapeSlotCount}·체포 자리 {stage.ArrestSlotCount}, " +
                      $"스코프 연결={scope != null}. 자리와 앵커는 씬에서 옮기세요.");
        }

        /// <summary>점 위 3 m에서 아래로 쏴 바닥을 찾는다. 콜라이더가 없으면 y=0.</summary>
        private static Vector3 GroundBelow(Vector3 point)
        {
            var origin = point + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return new Vector3(point.x, 0f, point.z);
        }

        /// <summary>카메라 정면을 가로지르는 한 줄. 자리마다 카메라를 향해 서게 회전한다.</summary>
        private static Transform CreateSlotRow(Transform parent, string rootName, string prefix, Vector3 center, float facingYaw, Vector3 forward)
        {
            var row = new GameObject(rootName).transform;
            row.SetParent(parent, false);
            row.position = center;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            const float spacing = 1.1f;
            for (var i = 0; i < EndingSlotCount; i++)
            {
                var slot = new GameObject(prefix + (i + 1)).transform;
                slot.SetParent(row, false);
                slot.position = center + right * ((i - (EndingSlotCount - 1) * 0.5f) * spacing);
                slot.rotation = Quaternion.Euler(0f, facingYaw, 0f);
            }

            return row;
        }

        // ------------------------------------------------------------------ 내부

        private static int RemoveDemoObjects(Scene scene)
        {
            var removed = 0;
            foreach (var name in RemoveDemoRoots)
            {
                var root = FindRoot(scene, name);
                if (root == null)
                {
                    continue;
                }

                Object.DestroyImmediate(root);
                removed++;
            }

            return removed;
        }

        // ------------------------------------------------------------------ 4. 원본 데모 배경 복원

        /// <summary>
        /// 배경을 원본 데모와 똑같이 되돌린다: 데모 후처리 볼륨을 켜고(이름도 원래대로), 마트에서 복사된 밝은 볼륨은 지운다.
        /// 안개·환경광·스카이박스는 데모 씬을 복제했을 때 이미 같다. 이전 setup이 볼륨을 꺼 둔 씬을 고칠 때 쓴다.
        /// </summary>
        [MenuItem(MenuRoot + "4. Restore Demo Look (Volume)")]
        public static void RestoreDemoLookMenu()
        {
            var mansion = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var summary = RestoreDemoLook(mansion);
            EditorSceneManager.MarkSceneDirty(mansion);
            EditorSceneManager.SaveScene(mansion);
            Debug.Log($"[Mansion] 원본 데모 배경 복원: {summary}");
        }

        private static string RestoreDemoLook(Scene scene)
        {
            var notes = new List<string>();
            var demoVolume = FindRoot(scene, DemoVolumeName) ?? FindRoot(scene, DisabledDemoVolumeName);
            if (demoVolume != null)
            {
                if (!demoVolume.activeSelf)
                {
                    demoVolume.SetActive(true);
                    notes.Add("데모 볼륨 켬");
                }

                demoVolume.name = DemoVolumeName;
            }
            else
            {
                notes.Add("데모 볼륨 없음(직접 확인)");
            }

            foreach (var name in new[] { MartVolumeName, "Mart Post Volume" })
            {
                var martVolume = FindRoot(scene, name);
                if (martVolume != null)
                {
                    Object.DestroyImmediate(martVolume);
                    notes.Add($"'{name}' 제거");
                }
            }

            return notes.Count == 0 ? "바꿀 것 없음(이미 원본과 같음)" : string.Join(", ", notes);
        }

        /// <summary>스폰·대기 스폰·파쇄기·카메라를 앞뜰/뒤뜰 바닥 위 격자에 임시로 놓는다.</summary>
        private static void PlacePlaceholders(Scene scene)
        {
            var frontGround = GroundAt(FrontYardAnchor);
            var backGround = GroundAt(BackYardAnchor);

            PlaceGrid(FindRoot(scene, "SpawnPoints"), frontGround, "SpawnPoint_");
            PlaceGrid(FindRoot(scene, "WaitingSpawnPoints"), backGround, "WaitingSpawnPoint_");

            var shredderA = FindRoot(scene, "Shredder_A");
            if (shredderA != null)
            {
                shredderA.transform.SetPositionAndRotation(frontGround + new Vector3(-8f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            }

            var shredderB = FindRoot(scene, "Shredder_B");
            if (shredderB != null)
            {
                shredderB.transform.SetPositionAndRotation(frontGround + new Vector3(8f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f));
            }

            var camera = FindRoot(scene, "Main Camera");
            if (camera != null)
            {
                // 앞뜰에서 본관 정면(남쪽, -z)을 바라보는 시점. 맵 미리보기 캡처 기준이기도 하다.
                camera.transform.SetPositionAndRotation(frontGround + new Vector3(0f, 2.2f, 10f), Quaternion.Euler(8f, 180f, 0f));
            }
        }

        private static void PlaceGrid(GameObject root, Vector3 ground, string childPrefix)
        {
            if (root == null)
            {
                return;
            }

            root.transform.SetPositionAndRotation(ground, Quaternion.identity);
            var children = Enumerable.Range(0, root.transform.childCount)
                .Select(i => root.transform.GetChild(i))
                .Where(t => t.name.StartsWith(childPrefix))
                .ToList();
            const int columns = 5;
            for (var i = 0; i < children.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var local = new Vector3((column - (columns - 1) * 0.5f) * GridSpacing, SpawnEyeOffset, row * GridSpacing);
                children[i].SetLocalPositionAndRotation(local, Quaternion.Euler(0f, 180f, 0f));
            }
        }

        /// <summary>앵커 위에서 아래로 쏴 바닥을 찾는다. 콜라이더가 없으면 y=0 평면으로 본다.</summary>
        private static Vector3 GroundAt(Vector3 anchor)
        {
            if (Physics.Raycast(anchor, Vector3.down, out var hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return new Vector3(anchor.x, 0f, anchor.z);
        }

        /// <summary>마트의 <c>MartEnvironment</c>와 같은 역할의 묶음. 경계·전등·대기 구역 도구가 이 아래를 본다.</summary>
        private static void EnsureEnvironmentRoot(Scene scene)
        {
            if (FindRoot(scene, EnvironmentRootName) != null)
            {
                return;
            }

            var root = new GameObject(EnvironmentRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var group in new[] { "Boundary", "Lights", "WaitingArea" })
            {
                var child = new GameObject(group);
                child.transform.SetParent(root.transform, false);
            }
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
    }
}
