using Game.Client.Interactions;
using Game.Client.Lobby;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LobbyPlanBoardInteractableTests
    {
        private GameObject boardObject;
        private GameObject propObject;

        [TearDown]
        public void TearDown()
        {
            if (boardObject != null) Object.DestroyImmediate(boardObject);
            if (propObject != null) Object.DestroyImmediate(propObject);
        }

        [Test]
        public void Bind_ShowsOutlineOnExternalProp_UnbindHidesIt()
        {
            propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var propRenderer = propObject.GetComponent<MeshRenderer>();

            boardObject = new GameObject("LobbyPlanBoard");
            boardObject.AddComponent<BoxCollider>();
            var outline = boardObject.AddComponent<InteractableFocusOutline>();
            using (var serialized = new SerializedObject(outline))
            {
                var sources = serialized.FindProperty("sourceRenderers");
                sources.arraySize = 1;
                sources.GetArrayElementAtIndex(0).objectReferenceValue = propRenderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var board = boardObject.AddComponent<LobbyPlanBoardInteractable>();
            using (var serialized = new SerializedObject(board))
            {
                serialized.FindProperty("outline").objectReferenceValue = outline;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Assert.That(board.IsOutlineVisible, Is.False);

            board.Bind(() => true, () => { });

            Assert.That(board.IsOutlineVisible, Is.True);
            var generated = FindGeneratedOutline(propObject.transform);
            Assert.That(generated, Is.Not.Null, "실루엣은 지정한 소품 렌더러 밑에 생겨야 한다.");
            Assert.That(generated.enabled, Is.True);
            Assert.That(boardObject.transform.childCount, Is.EqualTo(0),
                "판 오브젝트 자체에는 실루엣이 생기지 않아야 한다.");

            board.Unbind();

            Assert.That(board.IsOutlineVisible, Is.False);
            Assert.That(generated.enabled, Is.False);
        }

        // 외부 소품에 붙인 실루엣을 컴포넌트 파괴 시 함께 치우는 동작은 OnDestroy에 있어
        // 에디트 모드 테스트로는 검증할 수 없다(에디트 모드에서는 OnDestroy가 호출되지 않음).

        [Test]
        public void StoredSourceMesh_IsUsedInsteadOfFilterMesh()
        {
            // 정적 배칭된 소품은 플레이 중 MeshFilter가 결합 메시를 가리키므로,
            // 에디터에서 저장한 원본 메시가 복사본에 쓰여야 한다.
            propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var storedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(sphere);

            boardObject = new GameObject("LobbyPlanBoard");
            var outline = boardObject.AddComponent<InteractableFocusOutline>();
            using (var serialized = new SerializedObject(outline))
            {
                var sources = serialized.FindProperty("sourceRenderers");
                sources.arraySize = 1;
                sources.GetArrayElementAtIndex(0).objectReferenceValue =
                    propObject.GetComponent<MeshRenderer>();
                var meshes = serialized.FindProperty("sourceMeshes");
                meshes.arraySize = 1;
                meshes.GetArrayElementAtIndex(0).objectReferenceValue = storedMesh;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            outline.SetVisible(true);

            var generated = FindGeneratedOutline(propObject.transform);
            Assert.That(generated, Is.Not.Null);
            Assert.That(generated.GetComponent<MeshFilter>().sharedMesh, Is.EqualTo(storedMesh));
        }

        [Test]
        public void Bind_ShowsLabel_UnbindHidesIt()
        {
            boardObject = new GameObject("LobbyPlanBoard");
            boardObject.AddComponent<BoxCollider>();
            var label = new GameObject("Label");
            label.transform.SetParent(boardObject.transform, false);
            label.SetActive(false);
            var board = boardObject.AddComponent<LobbyPlanBoardInteractable>();
            using (var serialized = new SerializedObject(board))
            {
                serialized.FindProperty("label").objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Assert.That(board.IsLabelVisible, Is.False);

            board.Bind(() => true, () => { });
            Assert.That(board.IsLabelVisible, Is.True);
            Assert.That(label.activeSelf, Is.True);

            board.Unbind();
            Assert.That(board.IsLabelVisible, Is.False);
            Assert.That(label.activeSelf, Is.False);
        }

        [Test]
        public void Prompt_UsesDeskCenterAndBlackRoomSettingsLabel()
        {
            boardObject = new GameObject("LobbyPlanBoard");
            boardObject.transform.position = new Vector3(-2.7f, 1.35f, -3f);
            var box = boardObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.8f, 1.08f, 1.7f);
            var board = boardObject.AddComponent<LobbyPlanBoardInteractable>();

            Assert.That(board.InteractionPrompt, Is.EqualTo("방 설정"));
            Assert.That(board.InteractionPromptColor, Is.EqualTo(Color.black));
            Assert.That(board.TryGetInteractionPromptWorldPosition(out var world), Is.True);
            Assert.That(world.x, Is.EqualTo(box.bounds.center.x).Within(0.001f));
            Assert.That(world.z, Is.EqualTo(box.bounds.center.z).Within(0.001f));
            Assert.That(world.y, Is.EqualTo(box.bounds.min.y + InteractionPromptView.WorldLift).Within(0.001f));
        }

        [Test]
        public void Prompt_FollowsTheAppliedLanguage()
        {
            boardObject = new GameObject("LobbyPlanBoard");
            boardObject.AddComponent<BoxCollider>();
            var board = boardObject.AddComponent<LobbyPlanBoardInteractable>();
            using (var serialized = new SerializedObject(board))
            {
                // 로비 씬에 구워져 있는 값 그대로.
                serialized.FindProperty("prompt").stringValue = "방 설정";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            using var locale = new UiLocale(new GeneralSettingsSystem(store));

            Assert.That(board.InteractionPrompt, Is.EqualTo("Room Settings"));

            board.Bind(() => false, () => { });
            Assert.That(board.InteractionPrompt, Is.EqualTo("View Room Settings"));
        }

        [Test]
        public void WithoutOutline_BindStillWorks()
        {
            boardObject = new GameObject("LobbyPlanBoard");
            boardObject.AddComponent<BoxCollider>();
            var board = boardObject.AddComponent<LobbyPlanBoardInteractable>();

            Assert.DoesNotThrow(() => board.Bind(() => true, () => { }));
            Assert.That(board.IsBound, Is.True);
            Assert.That(board.IsOutlineVisible, Is.False);
        }

        private static Renderer FindGeneratedOutline(Transform prop)
        {
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject != prop.gameObject && renderer.name.StartsWith("["))
                {
                    return renderer;
                }
            }

            return null;
        }
    }
}
