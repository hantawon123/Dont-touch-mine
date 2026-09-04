using System.IO;
using Game.Bootstrap;
using Game.Client.Match;
using Game.Client.Voice;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class InGameHudLayoutMenu
    {
        private const string MenuPath = "Game/InGame/Build HUD Layout";

        /// <summary>
        /// Handed to the scope so the talk keys can be read without going
        /// through a character, which Fusion spawns after the scope is built.
        /// </summary>
        private const string InputActionsPath =
            "Assets/InputSystem_Actions.inputactions";
        private const string ScenePath = "Assets/_Game/Content/Scenes/Playground.unity";
        private const int WaitingSpawnPointCount = 6;
        private const string RequestPath =
            "Assets/_Game/Editor/InGameHudInstallRequest.txt";
        private const string HudName = "InGameHud";
        private const string FontPath =
            "Assets/_Game/Content/Fonts/Cafe24Ssurround SDF.asset";

        static InGameHudLayoutMenu()
        {
            if (File.Exists(RequestPath))
            {
                EditorApplication.delayCall += InstallRequestedLayout;
            }
        }

        [MenuItem(MenuPath)]
        public static void BuildHudLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Play 모드를 종료한 뒤 인게임 HUD를 생성하세요.");
                return;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var hud = FindHud(scene);
                if (hud == null)
                {
                    hud = CreateHud(scene);
                }

                EnsureAssignedItem(hud);
                EnsureHighlightTitle(hud);
                EnsureHidingIntro(hud);
                EnsureVoiceButton(hud);
                EnsureWaitingSpawnPoints(scene);

                ConnectLifetimeScope(scene, hud);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Selection.activeGameObject = hud.gameObject;
                Debug.Log("[InGame HUD] UI 생성과 런타임 연결을 완료했습니다.", hud);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static void InstallRequestedLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += InstallRequestedLayout;
                return;
            }

            try
            {
                BuildHudLayout();
                AssetDatabase.DeleteAsset(RequestPath);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[InGame HUD] 자동 설치 실패: {exception}");
            }
        }

        private static NetworkMatchHudView FindHud(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var hud = root.GetComponentInChildren<NetworkMatchHudView>(true);
                if (hud != null)
                {
                    return hud;
                }
            }

            return null;
        }

        private static NetworkMatchHudView CreateHud(Scene scene)
        {
            var canvasObject = new GameObject(
                HudName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(NetworkMatchHudView));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var phaseText = CreateText(
                canvasObject.transform,
                "PhaseText",
                "숨기는 중",
                38f,
                TextAlignmentOptions.Center);
            // Wider than the phase names need, because hiding now reads
            // "<이름>이 숨기는 중" and a nickname can run to its full length.
            Place(phaseText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, -58f), new Vector2(620f, 54f));
            var phaseView = phaseText.gameObject.AddComponent<MatchPhaseView>();
            Assign(phaseView, "phaseText", phaseText);

            var timerText = CreateText(
                canvasObject.transform,
                "TimerText",
                "03:00",
                52f,
                TextAlignmentOptions.Center);
            Place(timerText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, -112f), new Vector2(360f, 68f));
            var timerView = timerText.gameObject.AddComponent<MatchTimerView>();
            Assign(timerView, "timerText", timerText);

            var noticeRoot = CreatePanel(
                canvasObject.transform,
                "DestructionNotice",
                new Color(0.08f, 0.08f, 0.08f, 0.82f));
            Place(noticeRoot, new Vector2(0.5f, 1f),
                new Vector2(0f, -192f), new Vector2(760f, 72f));
            var noticeText = CreateText(
                noticeRoot,
                "NoticeText",
                "플레이어가 물건을 파괴했습니다!",
                30f,
                TextAlignmentOptions.Center);
            Stretch(noticeText.rectTransform, 18f);

            var marker = CreatePanel(
                canvasObject.transform,
                "ShredderMarker",
                new Color(0.75f, 0.08f, 0.08f, 0.9f));
            marker.sizeDelta = new Vector2(150f, 52f);
            var markerText = CreateText(
                marker,
                "Label",
                "파쇄기",
                26f,
                TextAlignmentOptions.Center);
            Stretch(markerText.rectTransform, 8f);

            var hud = canvasObject.GetComponent<NetworkMatchHudView>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("phaseView").objectReferenceValue = phaseView;
            serialized.FindProperty("timerView").objectReferenceValue = timerView;
            serialized.FindProperty("destructionNoticeRoot").objectReferenceValue =
                noticeRoot.gameObject;
            serialized.FindProperty("destructionNoticeText").objectReferenceValue = noticeText;
            serialized.FindProperty("shredderMarker").objectReferenceValue = marker;
            serialized.FindProperty("rootCanvas").objectReferenceValue = canvas;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            noticeRoot.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            return hud;
        }

        /// <summary>
        /// Puts the microphone button in the corner of the match HUD.
        /// </summary>
        /// <remarks>
        /// Same corner and size as the lobby's, because it is the same button
        /// doing the same job and a player crossing from one screen to the other
        /// should not have to look for it again.
        /// </remarks>
        private static VoiceView EnsureVoiceButton(NetworkMatchHudView hud)
        {
            var view = hud.GetComponent<VoiceView>();
            if (view == null)
            {
                view = hud.gameObject.AddComponent<VoiceView>();
            }

            var slot = hud.transform.Find("VoiceButton") as RectTransform;
            if (slot == null)
            {
                slot = CreatePanel(
                    hud.transform,
                    "VoiceButton",
                    new Color(0.25f, 0.25f, 0.28f, 0.9f));
            }

            Place(
                slot,
                new Vector2(1f, 0f),
                new Vector2(-60f, 60f),
                new Vector2(72f, 72f));

            var button = slot.GetComponent<Button>();
            if (button == null)
            {
                button = slot.gameObject.AddComponent<Button>();
            }

            var caption = slot.Find("Label")?.GetComponent<TMP_Text>();
            if (caption == null)
            {
                caption = CreateText(
                    slot,
                    "Label",
                    "MIC",
                    24f,
                    TextAlignmentOptions.Center);
                Stretch(caption.rectTransform, 6f);
            }

            var serialized = new SerializedObject(view);
            serialized.FindProperty("muteButton").objectReferenceValue = button;
            serialized.FindProperty("background").objectReferenceValue =
                slot.GetComponent<Image>();
            serialized.FindProperty("tmpLabel").objectReferenceValue = caption;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void ConnectLifetimeScope(Scene scene, NetworkMatchHudView hud)
        {
            PlaygroundLifetimeScope scope = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                scope = root.GetComponentInChildren<PlaygroundLifetimeScope>(true);
                if (scope != null)
                {
                    break;
                }
            }

            if (scope == null)
            {
                throw new System.InvalidOperationException(
                    "Playground 씬에 PlaygroundLifetimeScope가 없습니다.");
            }

            Assign(scope, "matchHudView", hud);
            Assign(scope, "voiceView", hud.GetComponent<VoiceView>());
            Assign(
                scope,
                "inputActions",
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
        }

        private static void EnsureHidingIntro(NetworkMatchHudView hud)
        {
            var serialized = new SerializedObject(hud);
            var property = serialized.FindProperty("hidingIntroView");
            var view = property.objectReferenceValue as HidingIntroView;
            if (view == null)
            {
                view = hud.GetComponentInChildren<HidingIntroView>(true);
            }

            if (view == null)
            {
                view = HidingIntroView.Create(hud.transform);
            }

            property.objectReferenceValue = view;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("previewOnAwake").boolValue = true;
            viewSerialized.FindProperty("previewItemName").stringValue = "탄산음료";
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();
            view.Show("탄산음료");
        }

        private static void EnsureAssignedItem(NetworkMatchHudView hud)
        {
            var serialized = new SerializedObject(hud);
            var property = serialized.FindProperty("assignedItemText");
            if (property.objectReferenceValue != null)
            {
                return;
            }

            var text = hud.transform.Find("AssignedItemText")?.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = CreateText(
                    hud.transform,
                    "AssignedItemText",
                    "내 물건: 탄산음료",
                    34f,
                    TextAlignmentOptions.Left);
                Place(
                    text.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(250f, -72f),
                    new Vector2(460f, 56f));
            }

            property.objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            text.gameObject.SetActive(false);
        }

        private static void EnsureHighlightTitle(NetworkMatchHudView hud)
        {
            var serialized = new SerializedObject(hud);
            var property = serialized.FindProperty("highlightTitleText");
            if (property.objectReferenceValue != null)
            {
                return;
            }

            var text = hud.transform.Find("HighlightTitleText")?.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = CreateText(
                    hud.transform,
                    "HighlightTitleText",
                    "FIRST BLOOD",
                    38f,
                    TextAlignmentOptions.Right);
                Place(
                    text.rectTransform,
                    Vector2.one,
                    new Vector2(-270f, -72f),
                    new Vector2(500f, 64f));
            }

            property.objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            text.gameObject.SetActive(false);
        }

        private static void EnsureWaitingSpawnPoints(Scene scene)
        {
            Transform root = null;
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                if (gameObject.name == "WaitingSpawnPoints")
                {
                    root = gameObject.transform;
                    break;
                }
            }

            if (root == null)
            {
                var rootObject = new GameObject("WaitingSpawnPoints");
                SceneManager.MoveGameObjectToScene(rootObject, scene);
                root = rootObject.transform;
            }

            for (var index = 0; index < WaitingSpawnPointCount; index++)
            {
                var point = root.Find($"WaitingSpawnPoint_{index + 1}");
                if (point == null)
                {
                    point = new GameObject($"WaitingSpawnPoint_{index + 1}").transform;
                    point.SetParent(root, false);
                }

                point.localPosition = new Vector3(9.5f, 0.7f, -3f + (index * 1.2f));
                point.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);

            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return text;
        }

        private static RectTransform CreatePanel(
            Transform parent,
            string name,
            Color color)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return gameObject.GetComponent<RectTransform>();
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
