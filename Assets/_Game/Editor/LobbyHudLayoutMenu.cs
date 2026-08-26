using Game.Bootstrap;
using Game.Client.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class LobbyHudLayoutMenu
    {
        private const string MenuPath = "Game/Lobby/Build HUD Layout";

        [MenuItem(MenuPath)]
        public static void BuildHudLayout()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog(
                    "Lobby HUD",
                    "Lobby 씬을 연 뒤 다시 실행하세요.",
                    "OK");
                return;
            }

            var scope = Object.FindFirstObjectByType<LobbyLifetimeScope>();
            if (scope == null)
            {
                var scopeGo = new GameObject("LobbyLifetimeScope");
                scope = scopeGo.AddComponent<LobbyLifetimeScope>();
                Undo.RegisterCreatedObjectUndo(scopeGo, "Create LobbyLifetimeScope");
            }

            var hud = Object.FindFirstObjectByType<LobbyHudView>();
            if (hud == null)
            {
                var canvasGo = CreateCanvas(scope.transform);
                hud = canvasGo.AddComponent<LobbyHudView>();
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create LobbyHud");
            }

            EnsureEventSystem();

            var root = hud.transform as RectTransform;
            var settings = GetOrCreateSlot(root, "SettingsButton", new Color(0.25f, 0.25f, 0.28f, 0.9f));
            var playSettings = GetOrCreateSlot(root, "PlaySettingsButton", new Color(0.25f, 0.25f, 0.28f, 0.9f));
            var start = GetOrCreateSlot(root, "StartButton", new Color(1f, 0.85f, 0.2f, 0.95f));
            var leave = GetOrCreateSlot(root, "LeaveButton", new Color(0.25f, 0.25f, 0.28f, 0.9f));
            var keyGuide = GetOrCreateSlot(root, "KeyGuideButton", new Color(0.2f, 0.22f, 0.28f, 0.85f));
            var playerList = GetOrCreateSlot(root, "PlayerListRoot", new Color(0.15f, 0.16f, 0.2f, 0.75f));
            var chat = GetOrCreateSlot(root, "ChatRoot", new Color(0.15f, 0.16f, 0.2f, 0.75f));
            var voice = GetOrCreateSlot(root, "VoiceButton", new Color(0.25f, 0.25f, 0.28f, 0.9f));

            Place(settings, Anchor.TopLeft, new Vector2(24f, -24f), new Vector2(72f, 72f));
            Place(playSettings, Anchor.TopLeft, new Vector2(112f, -24f), new Vector2(160f, 72f));
            Place(start, Anchor.TopCenter, new Vector2(0f, -24f), new Vector2(220f, 72f));
            Place(leave, Anchor.TopRight, new Vector2(-24f, -24f), new Vector2(180f, 72f));
            Place(keyGuide, Anchor.MiddleLeft, new Vector2(24f, 40f), new Vector2(180f, 96f));
            Place(playerList, Anchor.TopRight, new Vector2(-24f, -120f), new Vector2(240f, 420f));
            Place(chat, Anchor.BottomLeft, new Vector2(24f, 24f), new Vector2(420f, 220f));
            Place(voice, Anchor.BottomRight, new Vector2(-24f, 24f), new Vector2(72f, 72f));

            SetLabel(settings, "설정");
            SetLabel(playSettings, "플레이 설정");
            SetLabel(start, "START");
            SetLabel(leave, "게임 나가기");
            SetLabel(keyGuide, "키 세팅 가이드");
            SetLabel(playerList, "참가자 목록");
            SetLabel(chat, "채팅");
            SetLabel(voice, "MIC");

            EnsureButton(keyGuide.gameObject);

            var keyGuideView = hud.GetComponent<KeyGuideView>();
            if (keyGuideView == null)
            {
                keyGuideView = Undo.AddComponent<KeyGuideView>(hud.gameObject);
            }

            var panel = EnsureKeyGuidePanel(root);
            var closeButton = panel.Find("CloseButton") as RectTransform;
            var body = panel.Find("BodyText");
            EnsureButton(closeButton.gameObject);

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("settingsButton").objectReferenceValue = settings;
            hudSo.FindProperty("playSettingsButton").objectReferenceValue = playSettings;
            hudSo.FindProperty("startButton").objectReferenceValue = start;
            hudSo.FindProperty("leaveButton").objectReferenceValue = leave;
            hudSo.FindProperty("keyGuideButton").objectReferenceValue = keyGuide;
            hudSo.FindProperty("playerListRoot").objectReferenceValue = playerList;
            hudSo.FindProperty("chatRoot").objectReferenceValue = chat;
            hudSo.FindProperty("voiceButton").objectReferenceValue = voice;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var keyGuideSo = new SerializedObject(keyGuideView);
            keyGuideSo.FindProperty("openButton").objectReferenceValue =
                keyGuide.GetComponent<Button>();
            keyGuideSo.FindProperty("closeButton").objectReferenceValue =
                closeButton.GetComponent<Button>();
            keyGuideSo.FindProperty("panel").objectReferenceValue = panel.gameObject;
            keyGuideSo.FindProperty("bodyText").objectReferenceValue =
                body.GetComponent<Text>();
            keyGuideSo.ApplyModifiedPropertiesWithoutUndo();

            var scopeSo = new SerializedObject(scope);
            scopeSo.FindProperty("hudView").objectReferenceValue = hud;
            scopeSo.FindProperty("keyGuideView").objectReferenceValue = keyGuideView;
            scopeSo.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud.gameObject;
            EditorUtility.DisplayDialog(
                "Lobby HUD",
                "HUD 자리와 키 세팅 가이드 패널을 배치·연결했습니다.\n씬을 저장하세요 (Ctrl+S).",
                "OK");
        }

        private static RectTransform EnsureKeyGuidePanel(RectTransform root)
        {
            var existing = root.Find("KeyGuidePanel") as RectTransform;
            if (existing != null)
            {
                EnsureKeyGuidePanelChildren(existing);
                Place(existing, Anchor.Center, Vector2.zero, new Vector2(560f, 420f));
                return existing;
            }

            var panel = GetOrCreateSlot(root, "KeyGuidePanel", new Color(0.12f, 0.13f, 0.18f, 0.96f));
            Place(panel, Anchor.Center, Vector2.zero, new Vector2(560f, 420f));
            EnsureKeyGuidePanelChildren(panel);
            SetLabel(panel, string.Empty);
            return panel;
        }

        private static void EnsureKeyGuidePanelChildren(RectTransform panel)
        {
            var title = panel.Find("Title");
            if (title == null)
            {
                var titleGo = CreateTextChild(panel, "Title", "조작키 목록", 28, TextAnchor.UpperCenter);
                var titleRect = titleGo.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.sizeDelta = new Vector2(-40f, 48f);
                titleRect.anchoredPosition = new Vector2(0f, -20f);
            }

            var body = panel.Find("BodyText");
            if (body == null)
            {
                var bodyGo = CreateTextChild(panel, "BodyText", string.Empty, 24, TextAnchor.UpperLeft);
                var bodyRect = bodyGo.GetComponent<RectTransform>();
                bodyRect.anchorMin = new Vector2(0f, 0f);
                bodyRect.anchorMax = new Vector2(1f, 1f);
                bodyRect.offsetMin = new Vector2(28f, 80f);
                bodyRect.offsetMax = new Vector2(-28f, -72f);
                var bodyText = bodyGo.GetComponent<Text>();
                bodyText.alignment = TextAnchor.UpperLeft;
                bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            var close = panel.Find("CloseButton") as RectTransform;
            if (close == null)
            {
                close = GetOrCreateSlot(panel, "CloseButton", new Color(0.35f, 0.35f, 0.4f, 1f));
                Place(close, Anchor.BottomCenter, new Vector2(0f, 24f), new Vector2(160f, 48f));
                SetLabel(close, "닫기");
            }
        }

        private static GameObject CreateTextChild(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            return go;
        }

        private static void EnsureButton(GameObject go)
        {
            if (go.GetComponent<Button>() == null)
            {
                Undo.AddComponent<Button>(go);
            }
        }

        private static GameObject CreateCanvas(Transform parent)
        {
            var canvasGo = new GameObject(
                "LobbyHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return canvasGo;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        private static RectTransform GetOrCreateSlot(Transform parent, string name, Color color)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                EnsureImage(existing.gameObject, color);
                EnsureLabel(existing.gameObject);
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            EnsureImage(go, color);
            EnsureLabel(go);
            return go.GetComponent<RectTransform>();
        }

        private static void EnsureImage(GameObject go, Color color)
        {
            var image = go.GetComponent<Image>();
            if (image == null)
            {
                image = go.AddComponent<Image>();
            }

            image.color = color;
        }

        private static void EnsureLabel(GameObject go)
        {
            if (go.transform.Find("Label") != null)
            {
                return;
            }

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);

            var text = labelGo.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 22;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void SetLabel(RectTransform slot, string label)
        {
            var text = slot.Find("Label")?.GetComponent<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private enum Anchor
        {
            TopLeft,
            TopCenter,
            TopRight,
            MiddleLeft,
            BottomLeft,
            BottomRight,
            Center,
            BottomCenter
        }

        private static void Place(RectTransform rect, Anchor anchor, Vector2 anchoredPos, Vector2 size)
        {
            switch (anchor)
            {
                case Anchor.TopLeft:
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    break;
                case Anchor.TopCenter:
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    break;
                case Anchor.TopRight:
                    rect.anchorMin = new Vector2(1f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    break;
                case Anchor.MiddleLeft:
                    rect.anchorMin = new Vector2(0f, 0.5f);
                    rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    break;
                case Anchor.BottomLeft:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(0f, 0f);
                    rect.pivot = new Vector2(0f, 0f);
                    break;
                case Anchor.BottomRight:
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(1f, 0f);
                    break;
                case Anchor.Center:
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case Anchor.BottomCenter:
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    break;
            }

            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
        }
    }
}
