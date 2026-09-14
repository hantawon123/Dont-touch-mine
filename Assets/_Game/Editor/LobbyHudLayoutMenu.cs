using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client;
using Game.Client.Lobby;
using Game.Client.Rooms;
using Game.Client.Match;
using Game.Client.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class LobbyHudLayoutMenu
    {
        private const string MenuPath = "Game/Lobby/Build HUD Layout";

        /// <summary>
        /// Handed to the lobby scope so the talk key can be read without going
        /// through the camera rig, which keeps its own copy private.
        /// </summary>
        private const string InputActionsPath =
            "Assets/InputSystem_Actions.inputactions";

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
            var playerList = GetOrCreateSlot(root, "PlayerListRoot", new Color(0.15f, 0.16f, 0.2f, 0.75f));
            var chat = GetOrCreateSlot(root, "ChatRoot", new Color(0.15f, 0.16f, 0.2f, 0.75f));

            // Hidden on the HUD; 2 reparents this into the overlay.
            Place(
                playerList,
                Anchor.Center,
                Vector2.zero,
                LobbyPlayerListView.ModalSize);
            playerList.gameObject.SetActive(false);
            Place(
                chat,
                Anchor.BottomLeft,
                new Vector2(MatchChatView.Margin, MatchChatView.Margin),
                new Vector2(MatchChatView.InputWidth, 306f));

            SetLabel(playerList, string.Empty);
            SetLabel(chat, string.Empty);
            EnsurePlayerListContent(playerList);
            EnsureChatContent(chat);

            // Every button the lobby used to keep in a corner is an Esc menu
            // entry now: a captured cursor reports from the centre of the screen
            // and cannot reach a corner at all. The old slots go rather than
            // stay behind as dead buttons.
            DestroyIfExists(root, "StartButton");
            DestroyIfExists(root, "LeaveButton");
            DestroyIfExists(root, "SettingsButton");
            DestroyIfExists(root, "PlaySettingsButton");
            DestroyIfExists(root, "KeyGuideButton");
            DestroyIfExists(root, "KeyGuidePanel");
            DestroyIfExists(root, "VoiceButton");

            var playerListView = playerList.GetComponent<LobbyPlayerListView>();
            if (playerListView == null)
            {
                playerListView = Undo.AddComponent<LobbyPlayerListView>(playerList.gameObject);
            }

            var chatView = chat.GetComponent<MatchChatView>();
            if (chatView == null)
            {
                chatView = Undo.AddComponent<MatchChatView>(chat.gameObject);
            }

            chatView.SetKeepChromeVisible(true);

            // Built before the play settings screen it leads to: that screen
            // takes its open button from this panel now.
            var pauseMenuView = EnsurePauseMenuView(root, hud);
            var pauseMenuPanel = root.Find("PauseMenuPanel") as RectTransform;
            DestroyIfExists(pauseMenuPanel, "KeyGuideButton");
            var pausePlaySettings = pauseMenuPanel.Find("PlaySettingsButton") as RectTransform;
            var playSettingsView = EnsurePlaySettingsView(root, pausePlaySettings);
            var kickConfirm = EnsureKickConfirmView(root);
            DestroyIfExists(root, "HostTransferConfirmPanel");

            var chatBubbleView = EnsureChatBubbleWorld(scope.transform);
            KeySettingGuideView.Ensure(root);
            var shortcutGuide = LobbyShortcutGuideView.Ensure(root);
            var voiceView = hud.GetComponent<VoiceView>();
            if (voiceView == null)
            {
                voiceView = Undo.AddComponent<VoiceView>(hud.gameObject);
            }

            var voiceSo = new SerializedObject(voiceView);
            voiceSo.FindProperty("muteButton").objectReferenceValue =
                shortcutGuide != null ? shortcutGuide.VoiceMuteButton : null;
            voiceSo.FindProperty("background").objectReferenceValue =
                shortcutGuide != null ? shortcutGuide.VoiceBackground : null;
            voiceSo.FindProperty("icon").objectReferenceValue =
                shortcutGuide != null ? shortcutGuide.VoiceIcon : null;
            voiceSo.FindProperty("label").objectReferenceValue = null;
            voiceSo.FindProperty("tmpLabel").objectReferenceValue = null;
            voiceSo.ApplyModifiedPropertiesWithoutUndo();
            var shortcutOverlay = LobbyShortcutOverlayView.Ensure(root);
            shortcutOverlay?.BindPlayerList(playerListView);
            shortcutOverlay?.Hide();
            LobbyMatchInfoView.Ensure(root);
            LobbyPlayerCountView.Ensure(root);

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("playerListRoot").objectReferenceValue = playerList;
            hudSo.FindProperty("chatRoot").objectReferenceValue = chat;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            playerListView.EnsureLayout();

            var scopeSo = new SerializedObject(scope);
            scopeSo.FindProperty("hudView").objectReferenceValue = hud;
            scopeSo.FindProperty("pauseMenuView").objectReferenceValue = pauseMenuView;
            scopeSo.FindProperty("playerListView").objectReferenceValue = playerListView;
            scopeSo.FindProperty("playSettingsView").objectReferenceValue = playSettingsView;
            scopeSo.FindProperty("kickConfirmView").objectReferenceValue = kickConfirm;
            scopeSo.FindProperty("chatView").objectReferenceValue = chatView;
            scopeSo.FindProperty("chatBubbleView").objectReferenceValue = chatBubbleView;
            scopeSo.FindProperty("voiceView").objectReferenceValue = voiceView;
            scopeSo.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            scopeSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud.gameObject;
            EditorUtility.DisplayDialog(
                "Lobby HUD",
                "HUD·참가자 목록·방장 UI·채팅/말풍선·Esc 메뉴를 배치·연결했습니다." +
                "\n씬을 저장하세요 (Ctrl+S).",
                "OK");
        }

        private static PlaySettingsView EnsurePlaySettingsView(
            RectTransform root,
            RectTransform openButtonSlot)
        {
            var panel = root.Find("PlaySettingsPanel") as RectTransform;
            if (panel == null)
            {
                panel = GetOrCreateSlot(root, "PlaySettingsPanel", new Color(0.12f, 0.13f, 0.18f, 0.96f));
                SetLabel(panel, string.Empty);
            }

            Place(panel, Anchor.Center, Vector2.zero, PlaySettingsStyle.ModalSize);

            var staleOnPanel = panel.GetComponent<PlaySettingsView>();
            if (staleOnPanel != null)
            {
                Undo.DestroyObjectImmediate(staleOnPanel);
            }

            var view = root.GetComponent<PlaySettingsView>();
            if (view == null)
            {
                view = Undo.AddComponent<PlaySettingsView>(root.gameObject);
            }

            EnsurePlaySettingsChildren(panel);
            var back = EnsureBackButton(root);
            Place(back, Anchor.TopLeft, RoomBrowserStyle.Layout.BackButtonPosition, RoomBrowserStyle.Layout.BackButtonSize);

            var so = new SerializedObject(view);
            so.FindProperty("openButton").objectReferenceValue =
                openButtonSlot.GetComponent<Button>();
            so.FindProperty("closeButton").objectReferenceValue = back.GetComponent<Button>();
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;

            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return view;
        }

        private static void EnsurePlaySettingsChildren(RectTransform panel)
        {
            for (var i = panel.childCount - 1; i >= 0; i--)
            {
                DisableIfExists(panel, panel.GetChild(i).name);
            }
        }

        private static RectTransform EnsureBackButton(RectTransform parent)
        {
            DisableIfExists(parent, "CloseButton");

            var slot = parent.Find("BackButton") as RectTransform;
            if (slot == null)
            {
                slot = GetOrCreateSlot(parent, "BackButton", Color.clear);
            }

            EnsureButton(slot.gameObject);
            EnsureLabel(slot.gameObject);
            SetLabel(slot, "← 이전", Mathf.RoundToInt(RoomBrowserStyle.FontSize.Back));
            var label = slot.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
            }

            return slot;
        }

        private static void EnsureMapScroll(RectTransform panel, float left, float width)
        {
            var scroll = panel.Find("MapScroll") as RectTransform;
            if (scroll == null)
            {
                var go = new GameObject("MapScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                Undo.RegisterCreatedObjectUndo(go, "Create MapScroll");
                go.transform.SetParent(panel, false);
                scroll = go.GetComponent<RectTransform>();
                go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            }

            Place(scroll, Anchor.MiddleLeft, new Vector2(left, -210f), new Vector2(width, 100f));

            var viewport = scroll.Find("Viewport") as RectTransform;
            if (viewport == null)
            {
                var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                Undo.RegisterCreatedObjectUndo(vpGo, "Create Viewport");
                vpGo.transform.SetParent(scroll, false);
                viewport = vpGo.GetComponent<RectTransform>();
                vpGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
                vpGo.GetComponent<Mask>().showMaskGraphic = false;
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            var content = viewport.Find("MapContent") as RectTransform;
            if (content == null)
            {
                var contentGo = new GameObject("MapContent", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(contentGo, "Create MapContent");
                contentGo.transform.SetParent(viewport, false);
                content = contentGo.GetComponent<RectTransform>();
            }

            content.anchorMin = new Vector2(0f, 0.5f);
            content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(800f, 90f);

            var scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontalScrollbar = null;
            scrollRect.verticalScrollbar = null;
        }

        private static void EnsureFormLabel(
            RectTransform parent,
            string name,
            string label,
            float left,
            float y,
            float width)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
            {
                existing = CreateTextChild(parent, name, label, 18, TextAnchor.MiddleLeft)
                    .GetComponent<RectTransform>();
            }

            Place(existing, Anchor.MiddleLeft, new Vector2(left, y), new Vector2(width, 32f));
            ApplyText(existing.GetComponent<Text>(), label, 18, TextAnchor.MiddleLeft);
            existing.gameObject.SetActive(true);
        }

        private static void EnsureFormValue(
            RectTransform parent,
            string name,
            string value,
            float left,
            float y,
            float width,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
            {
                existing = CreateTextChild(parent, name, value, 18, alignment)
                    .GetComponent<RectTransform>();
            }

            Place(existing, Anchor.MiddleLeft, new Vector2(left, y), new Vector2(width, 32f));
            ApplyText(existing.GetComponent<Text>(), value, 18, alignment);
            existing.gameObject.SetActive(true);
        }

        private static RectTransform EnsureTextButtonLeft(
            RectTransform parent,
            string name,
            string label,
            float left,
            float y,
            Vector2 size)
        {
            var slot = parent.Find(name) as RectTransform;
            if (slot == null)
            {
                slot = GetOrCreateSlot(parent, name, new Color(0.35f, 0.35f, 0.4f, 1f));
            }

            Place(slot, Anchor.MiddleLeft, new Vector2(left, y), size);
            EnsureButton(slot.gameObject);
            EnsureLabel(slot.gameObject);
            SetLabel(slot, label, Mathf.Clamp(Mathf.RoundToInt(size.y * 0.55f), 16, 24));
            return slot;
        }

        private static void EnsureStepButtonLeft(
            RectTransform parent,
            string name,
            bool isPlus,
            float left,
            float y)
        {
            var slot = parent.Find(name) as RectTransform;
            if (slot == null)
            {
                slot = GetOrCreateSlot(parent, name, new Color(0.35f, 0.35f, 0.4f, 1f));
            }

            Place(slot, Anchor.MiddleLeft, new Vector2(left, y), new Vector2(40f, 40f));
            EnsureButton(slot.gameObject);
            HideSlotLabel(slot);
            EnsureStepIcon(slot, isPlus);
        }

        private static RectTransform EnsureTextButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var slot = parent.Find(name) as RectTransform;
            if (slot == null)
            {
                slot = GetOrCreateSlot(parent, name, new Color(0.35f, 0.35f, 0.4f, 1f));
            }

            Place(slot, Anchor.Center, anchoredPosition, size);
            EnsureButton(slot.gameObject);
            EnsureLabel(slot.gameObject);
            SetLabel(slot, label, Mathf.Clamp(Mathf.RoundToInt(size.y * 0.55f), 16, 24));
            return slot;
        }

        private static void EnsureStepButton(
            RectTransform parent,
            string name,
            bool isPlus,
            Vector2 anchoredPosition)
        {
            var slot = parent.Find(name) as RectTransform;
            if (slot == null)
            {
                slot = GetOrCreateSlot(parent, name, new Color(0.35f, 0.35f, 0.4f, 1f));
            }

            Place(slot, Anchor.Center, anchoredPosition, new Vector2(40f, 40f));
            EnsureButton(slot.gameObject);
            HideSlotLabel(slot);
            EnsureStepIcon(slot, isPlus);
        }

        private static void EnsureStepIcon(RectTransform slot, bool isPlus)
        {
            var iconRoot = slot.Find("Icon") as RectTransform;
            if (iconRoot == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create Icon");
                go.transform.SetParent(slot, false);
                iconRoot = go.GetComponent<RectTransform>();
            }

            iconRoot.anchorMin = new Vector2(0.5f, 0.5f);
            iconRoot.anchorMax = new Vector2(0.5f, 0.5f);
            iconRoot.pivot = new Vector2(0.5f, 0.5f);
            iconRoot.sizeDelta = new Vector2(40f, 40f);
            iconRoot.anchoredPosition = Vector2.zero;

            var horizontal = EnsureIconBar(iconRoot, "Horizontal", new Vector2(18f, 3f));
            PlaceLocal(horizontal, Vector2.zero);

            var vertical = iconRoot.Find("Vertical") as RectTransform;
            if (isPlus)
            {
                if (vertical == null)
                {
                    vertical = EnsureIconBar(iconRoot, "Vertical", new Vector2(3f, 18f));
                }

                PlaceLocal(vertical, Vector2.zero);
                vertical.gameObject.SetActive(true);
            }
            else if (vertical != null)
            {
                vertical.gameObject.SetActive(false);
            }
        }

        private static RectTransform EnsureIconBar(RectTransform parent, string name, Vector2 size)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                existing.sizeDelta = size;
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            return rect;
        }

        private static void PlaceLocal(RectTransform rect, Vector2 anchoredPosition)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
        }

        private static void HideSlotLabel(RectTransform slot)
        {
            var label = slot.Find("Label");
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Removes a slot the layout no longer owns, rather than leaving it
        /// behind for the next pass to re-place.
        /// </summary>
        private static void DestroyIfExists(RectTransform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void DisableIfExists(RectTransform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static KickConfirmView EnsureKickConfirmView(RectTransform root)
        {
            DestroyIfExists(root, "KickConfirmPanel");
            var view = root.GetComponent<KickConfirmView>();
            if (view == null)
            {
                view = Undo.AddComponent<KickConfirmView>(root.gameObject);
            }

            return view;
        }

        private static RectTransform EnsureButtonSlot(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var slot = parent.Find(name) as RectTransform;
            if (slot == null)
            {
                slot = GetOrCreateSlot(parent, name, new Color(0.35f, 0.35f, 0.4f, 1f));
            }

            Place(slot, Anchor.Center, anchoredPosition, size);
            EnsureButton(slot.gameObject);
            EnsureLabel(slot.gameObject);
            SetLabel(slot, label);
            return slot;
        }

        private static void EnsureInputField(
            RectTransform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                Place(existing, Anchor.Center, anchoredPosition, size);
                return;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Place(rect, Anchor.Center, anchoredPosition, size);
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 1f);

            var textGo = CreateTextChild(go.transform, "Text", string.Empty, 18, TextAnchor.MiddleLeft);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            var placeholderGo = CreateTextChild(
                go.transform,
                "Placeholder",
                string.Empty,
                18,
                TextAnchor.MiddleLeft);
            var placeholder = placeholderGo.GetComponent<Text>();
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            var placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 4f);
            placeholderRect.offsetMax = new Vector2(-8f, -4f);

            var input = go.GetComponent<InputField>();
            input.textComponent = textGo.GetComponent<Text>();
            input.placeholder = placeholder;
        }

        private static void EnsureToggle(RectTransform parent, string name, string label, Vector2 anchoredPosition)
        {
            if (parent.Find(name) != null)
            {
                return;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Place(rect, Anchor.Center, anchoredPosition, new Vector2(180f, 32f));

            var bg = GetOrCreateSlot(rect, "Background", new Color(0.25f, 0.25f, 0.3f, 1f));
            Place(bg, Anchor.MiddleLeft, Vector2.zero, new Vector2(28f, 28f));
            var check = GetOrCreateSlot(bg, "Checkmark", new Color(1f, 0.85f, 0.2f, 1f));
            Place(check, Anchor.Center, Vector2.zero, new Vector2(18f, 18f));
            var labelGo = CreateTextChild(rect, "Label", label, 18, TextAnchor.MiddleLeft);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(36f, 0f);
            labelRect.offsetMax = Vector2.zero;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
        }

        private static void EnsureLabeledField(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            float width = 200f)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                Place(existing, Anchor.Center, anchoredPosition, new Vector2(width, 32f));
                ApplyText(existing.GetComponent<Text>(), label, 18, TextAnchor.MiddleLeft);
                existing.gameObject.SetActive(true);
                return;
            }

            var go = CreateTextChild(parent, name, label, 18, TextAnchor.MiddleLeft);
            Place(go.GetComponent<RectTransform>(), Anchor.Center, anchoredPosition, new Vector2(width, 32f));
        }

        private static void EnsurePlainText(
            RectTransform parent,
            string name,
            string value,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                Place(existing, Anchor.Center, anchoredPosition, size);
                ApplyText(existing.GetComponent<Text>(), value, 18, alignment);
                existing.gameObject.SetActive(true);
                return;
            }

            var go = CreateTextChild(parent, name, value, 18, alignment);
            Place(go.GetComponent<RectTransform>(), Anchor.Center, anchoredPosition, size);
        }

        private static void EnsureChatContent(RectTransform chat)
        {
            var leftoverLabel = chat.Find("Label");
            if (leftoverLabel != null)
            {
                leftoverLabel.gameObject.SetActive(false);
            }

            DestroyIfExists(chat, "Title");
            DestroyIfExists(chat, "HistoryViewport");
            DestroyIfExists(chat, "InputField");
            DestroyIfExists(chat, "SendButton");

            var image = chat.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
                image.raycastTarget = false;
            }
        }

        private static MatchChatBubbleView EnsureChatBubbleWorld(Transform parent)
        {
            var root = parent.Find("ChatBubbleWorld");
            GameObject rootGo;
            if (root == null)
            {
                rootGo = new GameObject("ChatBubbleWorld");
                Undo.RegisterCreatedObjectUndo(rootGo, "Create ChatBubbleWorld");
                rootGo.transform.SetParent(parent, false);
            }
            else
            {
                rootGo = root.gameObject;
            }

            for (var index = rootGo.transform.childCount - 1; index >= 0; index--)
            {
                var child = rootGo.transform.GetChild(index);
                if (child.name.StartsWith("Head_", System.StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            var view = rootGo.GetComponent<MatchChatBubbleView>();
            if (view == null)
            {
                view = Undo.AddComponent<MatchChatBubbleView>(rootGo);
            }

            return view;
        }

        private static void EnsurePlayerListContent(RectTransform playerList)
        {
            var leftoverLabel = playerList.Find("Label");
            if (leftoverLabel != null)
            {
                leftoverLabel.gameObject.SetActive(false);
            }

            var leftoverTitle = playerList.Find("Title");
            if (leftoverTitle != null)
            {
                leftoverTitle.gameObject.SetActive(false);
            }

            var leftoverBody = playerList.Find("BodyText");
            if (leftoverBody != null)
            {
                leftoverBody.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The Esc menu: the three things the player reaches with the mouse
        /// while the rest of the lobby keeps the cursor captured.
        /// </summary>
        /// <remarks>
        /// The view component goes on the HUD canvas, not on the panel, so its
        /// buttons are wired whether or not the panel has ever been shown.
        /// </remarks>
        private static LobbyPauseMenuView EnsurePauseMenuView(
            RectTransform root,
            LobbyHudView hud)
        {
            var panel = root.Find("PauseMenuPanel") as RectTransform;
            if (panel == null)
            {
                panel = GetOrCreateSlot(
                    root, "PauseMenuPanel", new Color(0.12f, 0.13f, 0.18f, 0.96f));
                SetLabel(panel, string.Empty);
            }

            Place(panel, Anchor.Center, Vector2.zero, new Vector2(420f, 560f));

            if (panel.Find("Title") == null)
            {
                var titleGo = CreateTextChild(panel, "Title", "메뉴", 28, TextAnchor.UpperCenter);
                var titleRect = titleGo.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.sizeDelta = new Vector2(-40f, 48f);
                titleRect.anchoredPosition = new Vector2(0f, -20f);
            }

            // Five rows 72 apart: what changes the room, then settings, then
            // the way out. Leaving sits above returning so the button that
            // ends the visit is not the one under the thumb.
            var buttonSize = new Vector2(260f, 56f);
            var start = EnsureButtonSlot(
                panel, "StartButton", "게임 시작", new Vector2(0f, 160f), buttonSize);
            var playSettings = EnsureButtonSlot(
                panel, "PlaySettingsButton", "플레이 설정", new Vector2(0f, 88f), buttonSize);
            var settings = EnsureButtonSlot(
                panel, "SettingsButton", "설정", new Vector2(0f, 16f), buttonSize);
            var leave = EnsureButtonSlot(
                panel, "LeaveButton", "게임 나가기", new Vector2(0f, -56f), buttonSize);
            var resume = EnsureButtonSlot(
                panel, "ResumeButton", "돌아가기", new Vector2(0f, -128f), buttonSize);
            EnsureImage(start.gameObject, new Color(1f, 0.85f, 0.2f, 0.95f));

            var settingsButton = settings.GetComponent<Button>();
            settingsButton.interactable = true;

            var view = hud.GetComponent<LobbyPauseMenuView>();
            if (view == null)
            {
                view = Undo.AddComponent<LobbyPauseMenuView>(hud.gameObject);
            }

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("startButton").objectReferenceValue = start.GetComponent<Button>();
            so.FindProperty("leaveButton").objectReferenceValue = leave.GetComponent<Button>();
            so.FindProperty("resumeButton").objectReferenceValue = resume.GetComponent<Button>();
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("playSettingsButton").objectReferenceValue =
                playSettings.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            return view;
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
            ApplyText(go.GetComponent<Text>(), content, fontSize, alignment);
            return go;
        }

        private static void EnsureButton(GameObject go)
        {
            if (go.GetComponent<Button>() == null)
            {
                Undo.AddComponent<Button>(go);
            }
        }

        private static Font ResolveLobbyFont()
        {
            var paperlogy = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/_Game/Content/Resources/Fonts/Paperlogy-6SemiBold.ttf")
                ?? AssetDatabase.LoadAssetAtPath<Font>(
                    "Assets/_Game/Content/Fonts/Paperlogy-6SemiBold.ttf");
            if (paperlogy != null)
            {
                return paperlogy;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void ApplyText(Text text, string content, int fontSize, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            text.text = content ?? string.Empty;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.font = ResolveLobbyFont();
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        private static void EnsureLabel(GameObject go)
        {
            var labelTransform = go.transform.Find("Label");
            if (labelTransform == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGo.transform.SetParent(go.transform, false);
                labelTransform = labelGo.transform;
            }

            labelTransform.gameObject.SetActive(true);
            var rect = labelTransform.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 2f);
            rect.offsetMax = new Vector2(-4f, -2f);

            var text = labelTransform.GetComponent<Text>();
            if (text == null)
            {
                text = labelTransform.gameObject.AddComponent<Text>();
            }

            ApplyText(text, text.text, 18, TextAnchor.MiddleCenter);
        }

        private static void SetLabel(RectTransform slot, string label, int fontSize = 18)
        {
            EnsureLabel(slot.gameObject);
            var text = slot.Find("Label")?.GetComponent<Text>();
            ApplyText(text, label, fontSize, TextAnchor.MiddleCenter);
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

            Game.Client.Common.HudScreenScale.Apply(canvasGo.GetComponent<CanvasScaler>());

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
