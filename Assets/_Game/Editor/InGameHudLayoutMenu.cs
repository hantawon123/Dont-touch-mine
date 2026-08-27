using System.IO;
using Game.Bootstrap;
using Game.Client.Match;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class InGameHudLayoutMenu
    {
        private const string MenuPath = "Game/InGame/Build HUD Layout";
        private const string ScenePath = "Assets/_Game/Content/Scenes/Playground.unity";
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
            Place(phaseText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, -58f), new Vector2(360f, 54f));
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
