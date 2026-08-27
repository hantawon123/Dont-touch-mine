using System.Collections.Generic;
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
            var keyGuide = GetOrCreateSlot(root, "KeyGuideButton", new Color(0.2f, 0.22f, 0.28f, 0.85f));
            var playerList = GetOrCreateSlot(root, "PlayerListRoot", new Color(0.15f, 0.16f, 0.2f, 0.75f));
            var chat = GetOrCreateSlot(root, "ChatRoot", new Color(0.15f, 0.16f, 0.2f, 0.75f));
            var voice = GetOrCreateSlot(root, "VoiceButton", new Color(0.25f, 0.25f, 0.28f, 0.9f));

            Place(settings, Anchor.TopLeft, new Vector2(24f, -24f), new Vector2(72f, 72f));
            Place(playSettings, Anchor.TopLeft, new Vector2(112f, -24f), new Vector2(160f, 72f));
            Place(keyGuide, Anchor.MiddleLeft, new Vector2(24f, 40f), new Vector2(180f, 96f));
            Place(playerList, Anchor.TopRight, new Vector2(-24f, -24f), new Vector2(300f, 420f));
            // Lifted well clear of the floor and narrowed. The characters stand
            // low and centre, which is where the camera looks, and a 720-wide
            // panel on the bottom edge ran straight across them.
            Place(chat, Anchor.BottomLeft, new Vector2(24f, 380f), new Vector2(640f, 240f));
            Place(voice, Anchor.BottomRight, new Vector2(-24f, 24f), new Vector2(72f, 72f));

            SetLabel(settings, "설정");
            SetLabel(playSettings, "플레이 설정");
            SetLabel(keyGuide, "키 세팅 가이드");
            SetLabel(playerList, string.Empty);
            SetLabel(chat, string.Empty);
            SetLabel(voice, "MIC");
            EnsurePlayerListContent(playerList);
            EnsureChatContent(chat);

            EnsureButton(keyGuide.gameObject);
            EnsureButton(playSettings.gameObject);

            // Start and Leave used to sit up here, where a captured cursor could
            // not reach them. They are in the Esc menu now, so the old slots go
            // rather than stay behind as dead buttons.
            DestroyIfExists(root, "StartButton");
            DestroyIfExists(root, "LeaveButton");

            var keyGuideView = hud.GetComponent<KeyGuideView>();
            if (keyGuideView == null)
            {
                keyGuideView = Undo.AddComponent<KeyGuideView>(hud.gameObject);
            }

            var playerListView = playerList.GetComponent<LobbyPlayerListView>();
            if (playerListView == null)
            {
                playerListView = Undo.AddComponent<LobbyPlayerListView>(playerList.gameObject);
            }

            var chatView = chat.GetComponent<LobbyChatView>();
            if (chatView == null)
            {
                chatView = Undo.AddComponent<LobbyChatView>(chat.gameObject);
            }

            var keyGuidePanel = EnsureKeyGuidePanel(root);
            var keyGuideClose = keyGuidePanel.Find("CloseButton") as RectTransform;
            var keyGuideBody = keyGuidePanel.Find("BodyText");
            EnsureButton(keyGuideClose.gameObject);

            var playSettingsView = EnsurePlaySettingsView(root, playSettings);
            var kickConfirm = EnsureConfirmView<KickConfirmView>(
                root,
                "KickConfirmPanel",
                "강퇴 확인");
            var transferConfirm = EnsureConfirmView<HostTransferConfirmView>(
                root,
                "HostTransferConfirmPanel",
                "방장 위임 확인");
            var chatBubbleView = EnsureChatBubbleWorld(scope.transform);
            var pauseMenuView = EnsurePauseMenuView(root, hud);

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("settingsButton").objectReferenceValue = settings;
            hudSo.FindProperty("playSettingsButton").objectReferenceValue = playSettings;
            hudSo.FindProperty("keyGuideButton").objectReferenceValue = keyGuide;
            hudSo.FindProperty("playerListRoot").objectReferenceValue = playerList;
            hudSo.FindProperty("chatRoot").objectReferenceValue = chat;
            hudSo.FindProperty("voiceButton").objectReferenceValue = voice;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var keyGuideSo = new SerializedObject(keyGuideView);
            keyGuideSo.FindProperty("openButton").objectReferenceValue =
                keyGuide.GetComponent<Button>();
            keyGuideSo.FindProperty("closeButton").objectReferenceValue =
                keyGuideClose.GetComponent<Button>();
            keyGuideSo.FindProperty("panel").objectReferenceValue = keyGuidePanel.gameObject;
            keyGuideSo.FindProperty("bodyText").objectReferenceValue =
                keyGuideBody.GetComponent<Text>();
            keyGuideSo.ApplyModifiedPropertiesWithoutUndo();

            var playerListSo = new SerializedObject(playerListView);
            playerListSo.FindProperty("titleText").objectReferenceValue =
                playerList.Find("Title")?.GetComponent<Text>();
            playerListSo.FindProperty("rowRoot").objectReferenceValue =
                playerList.Find("RowRoot");
            playerListSo.FindProperty("uiFont").objectReferenceValue = ResolveLobbyFont();
            playerListSo.ApplyModifiedPropertiesWithoutUndo();

            var title = playerList.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                ApplyText(title, "참가자 목록", 22, TextAnchor.UpperCenter);
            }

            var chatSo = new SerializedObject(chatView);
            chatSo.FindProperty("messageRoot").objectReferenceValue =
                chat.Find("HistoryViewport/HistoryContent");
            chatSo.FindProperty("inputField").objectReferenceValue =
                chat.Find("InputField")?.GetComponent<InputField>();
            chatSo.FindProperty("sendButton").objectReferenceValue =
                chat.Find("SendButton")?.GetComponent<Button>();
            chatSo.FindProperty("scrollRect").objectReferenceValue =
                chat.Find("HistoryViewport")?.GetComponent<ScrollRect>();
            chatSo.FindProperty("uiFont").objectReferenceValue = ResolveLobbyFont();
            chatSo.ApplyModifiedPropertiesWithoutUndo();

            var chatInput = chat.Find("InputField")?.GetComponent<InputField>();
            if (chatInput != null)
            {
                chatInput.characterLimit = 80;
                chatInput.lineType = InputField.LineType.SingleLine;
            }

            var scopeSo = new SerializedObject(scope);
            scopeSo.FindProperty("hudView").objectReferenceValue = hud;
            scopeSo.FindProperty("keyGuideView").objectReferenceValue = keyGuideView;
            scopeSo.FindProperty("pauseMenuView").objectReferenceValue = pauseMenuView;
            scopeSo.FindProperty("playerListView").objectReferenceValue = playerListView;
            scopeSo.FindProperty("playSettingsView").objectReferenceValue = playSettingsView;
            scopeSo.FindProperty("kickConfirmView").objectReferenceValue = kickConfirm;
            scopeSo.FindProperty("transferConfirmView").objectReferenceValue = transferConfirm;
            scopeSo.FindProperty("chatView").objectReferenceValue = chatView;
            scopeSo.FindProperty("chatBubbleView").objectReferenceValue = chatBubbleView;
            scopeSo.ApplyModifiedPropertiesWithoutUndo();

            keyGuidePanel.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud.gameObject;
            EditorUtility.DisplayDialog(
                "Lobby HUD",
                "HUD·키 가이드·참가자 목록·방장 UI·채팅/말풍선·Esc 메뉴를 배치·연결했습니다." +
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

            Place(panel, Anchor.Center, Vector2.zero, new Vector2(760f, 580f));
            EnsurePlaySettingsChildren(panel);

            // Keep the view on LobbyHud (always active). Hiding the panel must not
            // disable the component or it will wipe HUD button listeners.
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

            var mapScroll = panel.Find("MapScroll")?.GetComponent<ScrollRect>();
            var mapContent = panel.Find("MapScroll/Viewport/MapContent") as RectTransform;

            var so = new SerializedObject(view);
            so.FindProperty("openButton").objectReferenceValue =
                openButtonSlot.GetComponent<Button>();
            so.FindProperty("closeButton").objectReferenceValue =
                panel.Find("CloseButton")?.GetComponent<Button>();
            so.FindProperty("copyRoomCodeButton").objectReferenceValue =
                panel.Find("CopyRoomCodeButton")?.GetComponent<Button>();
            so.FindProperty("inviteButton").objectReferenceValue =
                panel.Find("InviteButton")?.GetComponent<Button>();
            so.FindProperty("copyPasswordButton").objectReferenceValue =
                panel.Find("CopyPasswordButton")?.GetComponent<Button>();
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("titleText").objectReferenceValue =
                panel.Find("TitleText")?.GetComponent<Text>();
            so.FindProperty("roomCodeText").objectReferenceValue =
                panel.Find("RoomCodeText")?.GetComponent<Text>();
            so.FindProperty("passwordMaskedText").objectReferenceValue =
                panel.Find("PasswordMaskedText")?.GetComponent<Text>();
            so.FindProperty("maxPlayersText").objectReferenceValue =
                panel.Find("MaxPlayersText")?.GetComponent<Text>();
            so.FindProperty("maxPlayersMinusButton").objectReferenceValue =
                panel.Find("MaxPlayersMinus")?.GetComponent<Button>();
            so.FindProperty("maxPlayersPlusButton").objectReferenceValue =
                panel.Find("MaxPlayersPlus")?.GetComponent<Button>();
            so.FindProperty("destructionLimitText").objectReferenceValue =
                panel.Find("DestructionText")?.GetComponent<Text>();
            so.FindProperty("destructionMinusButton").objectReferenceValue =
                panel.Find("DestructionMinus")?.GetComponent<Button>();
            so.FindProperty("destructionPlusButton").objectReferenceValue =
                panel.Find("DestructionPlus")?.GetComponent<Button>();
            so.FindProperty("mapNameText").objectReferenceValue =
                panel.Find("MapNameText")?.GetComponent<Text>();
            so.FindProperty("mapPrevButton").objectReferenceValue =
                panel.Find("MapPrevButton")?.GetComponent<Button>();
            so.FindProperty("mapNextButton").objectReferenceValue =
                panel.Find("MapNextButton")?.GetComponent<Button>();
            so.FindProperty("mapScroll").objectReferenceValue = mapScroll;
            so.FindProperty("mapContent").objectReferenceValue = mapContent;

            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return view;
        }

        private static void EnsurePlaySettingsChildren(RectTransform panel)
        {
            DisableIfExists(panel, "TitleInput");
            DisableIfExists(panel, "PasswordToggle");
            DisableIfExists(panel, "PasswordInput");
            DisableIfExists(panel, "MapIdInput");
            DisableIfExists(panel, "ApplyButton");
            DisableIfExists(panel, "CopyButton");
            for (var i = 0; i < 8; i++)
            {
                DisableIfExists(panel, $"MapSlot{i}");
            }

            HideSlotLabel(panel);

            // Left-edge aligned columns (MiddleLeft), panel width 760.
            const float labelLeft = 40f;
            const float valueLeft = 200f;
            const float actionLeft = 460f;
            const float action2Left = 580f;
            const float labelWidth = 150f;

            EnsureFormLabel(panel, "TitleLabel", "방제목", labelLeft, 220f, labelWidth);
            EnsureFormValue(panel, "TitleText", "초보방", valueLeft, 220f, 240f);

            EnsureFormLabel(panel, "RoomCodeLabel", "방코드", labelLeft, 155f, labelWidth);
            EnsureFormValue(panel, "RoomCodeText", "K7M2QF", valueLeft, 155f, 140f);
            EnsureTextButtonLeft(panel, "CopyRoomCodeButton", "복사", actionLeft, 155f, new Vector2(96f, 36f));
            EnsureTextButtonLeft(panel, "InviteButton", "초대", action2Left, 155f, new Vector2(96f, 36f));

            EnsureFormLabel(panel, "PasswordLabel", "비밀번호", labelLeft, 90f, labelWidth);
            EnsureFormValue(panel, "PasswordMaskedText", "****", valueLeft, 90f, 140f);
            EnsureTextButtonLeft(panel, "CopyPasswordButton", "복사", actionLeft, 90f, new Vector2(96f, 36f));

            EnsureFormLabel(panel, "MaxPlayersLabel", "인원", labelLeft, 25f, labelWidth);
            EnsureStepButtonLeft(panel, "MaxPlayersMinus", isPlus: false, valueLeft, 25f);
            EnsureFormValue(panel, "MaxPlayersText", "6", valueLeft + 52f, 25f, 48f, TextAnchor.MiddleCenter);
            EnsureStepButtonLeft(panel, "MaxPlayersPlus", isPlus: true, valueLeft + 112f, 25f);

            EnsureFormLabel(panel, "DestructionLabel", "파괴 가능 횟수", labelLeft, -40f, labelWidth);
            EnsureStepButtonLeft(panel, "DestructionMinus", isPlus: false, valueLeft, -40f);
            EnsureFormValue(panel, "DestructionText", "5", valueLeft + 52f, -40f, 48f, TextAnchor.MiddleCenter);
            EnsureStepButtonLeft(panel, "DestructionPlus", isPlus: true, valueLeft + 112f, -40f);

            EnsureFormLabel(panel, "MapLabel", "맵", labelLeft, -105f, labelWidth);
            EnsureFormValue(panel, "MapNameText", "시장 골목", valueLeft, -105f, 320f);

            const float mapSide = 40f;
            const float mapMargin = 40f;
            EnsureTextButtonLeft(panel, "MapPrevButton", "<", mapMargin, -210f, new Vector2(mapSide, 96f));
            EnsureTextButtonLeft(
                panel,
                "MapNextButton",
                ">",
                760f - mapMargin - mapSide,
                -210f,
                new Vector2(mapSide, 96f));
            EnsureMapScroll(panel, mapMargin + mapSide + 12f, 760f - ((mapMargin + mapSide + 12f) * 2f));

            var close = EnsureTextButton(panel, "CloseButton", "×", new Vector2(0f, 0f), new Vector2(40f, 40f));
            Place(close, Anchor.TopRight, new Vector2(-10f, -10f), new Vector2(40f, 40f));
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

        private static TConfirm EnsureConfirmView<TConfirm>(
            RectTransform root,
            string panelName,
            string title)
            where TConfirm : LobbyConfirmView
        {
            var panel = root.Find(panelName) as RectTransform;
            if (panel == null)
            {
                panel = GetOrCreateSlot(root, panelName, new Color(0.12f, 0.13f, 0.18f, 0.96f));
                Place(panel, Anchor.Center, Vector2.zero, new Vector2(460f, 220f));
                SetLabel(panel, string.Empty);
            }

            var messageTransform = panel.Find("MessageText");
            if (messageTransform == null)
            {
                messageTransform = CreateTextChild(panel, "MessageText", title, 22, TextAnchor.MiddleCenter).transform;
            }

            var messageRect = messageTransform.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0f, 0f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(24f, 88f);
            messageRect.offsetMax = new Vector2(-24f, -24f);

            EnsureButtonSlot(panel, "ConfirmButton", "예", new Vector2(-80f, -58f), new Vector2(120f, 40f));
            EnsureButtonSlot(panel, "CancelButton", "아니오", new Vector2(80f, -58f), new Vector2(120f, 40f));

            var view = panel.GetComponent<TConfirm>();
            if (view == null)
            {
                view = Undo.AddComponent<TConfirm>(panel.gameObject);
            }

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("messageText").objectReferenceValue =
                panel.Find("MessageText")?.GetComponent<Text>();
            so.FindProperty("confirmButton").objectReferenceValue =
                panel.Find("ConfirmButton")?.GetComponent<Button>();
            so.FindProperty("cancelButton").objectReferenceValue =
                panel.Find("CancelButton")?.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
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

            var title = chat.Find("Title");
            if (title == null)
            {
                var titleGo = CreateTextChild(chat, "Title", "채팅", 20, TextAnchor.MiddleLeft);
                var titleRect = titleGo.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0f, 1f);
                titleRect.sizeDelta = new Vector2(-16f, 28f);
                titleRect.anchoredPosition = new Vector2(12f, -8f);
            }
            else
            {
                ApplyText(title.GetComponent<Text>(), "채팅", 20, TextAnchor.MiddleLeft);
            }

            var viewport = chat.Find("HistoryViewport") as RectTransform;
            if (viewport == null)
            {
                var vpGo = new GameObject(
                    "HistoryViewport",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Mask),
                    typeof(ScrollRect));
                Undo.RegisterCreatedObjectUndo(vpGo, "Create HistoryViewport");
                vpGo.transform.SetParent(chat, false);
                viewport = vpGo.GetComponent<RectTransform>();
                vpGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
                vpGo.GetComponent<Mask>().showMaskGraphic = false;
            }

            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(10f, 52f);
            viewport.offsetMax = new Vector2(-10f, -40f);

            var history = viewport.Find("HistoryText") as RectTransform;
            if (history != null)
            {
                history.gameObject.SetActive(false);
            }

            var content = viewport.Find("HistoryContent") as RectTransform;
            if (content == null)
            {
                var contentGo = new GameObject(
                    "HistoryContent",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                Undo.RegisterCreatedObjectUndo(contentGo, "Create HistoryContent");
                contentGo.transform.SetParent(viewport, false);
                content = contentGo.GetComponent<RectTransform>();
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 160f);

            var vertical = content.GetComponent<VerticalLayoutGroup>();
            vertical.childAlignment = TextAnchor.UpperLeft;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.spacing = 2f;
            vertical.padding = new RectOffset(8, 8, 8, 8);

            var contentFitter = content.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.content = content;
            scroll.viewport = viewport;

            EnsureInputField(chat, "InputField", new Vector2(-40f, 22f), new Vector2(600f, 36f));
            var input = chat.Find("InputField") as RectTransform;
            if (input != null)
            {
                Place(input, Anchor.BottomLeft, new Vector2(12f, 10f), new Vector2(600f, 36f));
            }

            EnsureTextButton(chat, "SendButton", "전송", new Vector2(0f, 0f), new Vector2(80f, 36f));
            var send = chat.Find("SendButton") as RectTransform;
            if (send != null)
            {
                Place(send, Anchor.BottomRight, new Vector2(-12f, 10f), new Vector2(80f, 36f));
            }
        }

        private static LobbyChatBubbleView EnsureChatBubbleWorld(Transform parent)
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

            var view = rootGo.GetComponent<LobbyChatBubbleView>();
            if (view == null)
            {
                view = Undo.AddComponent<LobbyChatBubbleView>(rootGo);
            }

            var samples = new[]
            {
                ("host-1", new Vector3(-1.5f, 1.6f, 2f)),
                ("player-2", new Vector3(0f, 1.6f, 2f)),
                ("player-3", new Vector3(1.5f, 1.6f, 2f)),
            };

            var anchors = new List<LobbyChatBubbleAnchor>();
            for (var i = 0; i < samples.Length; i++)
            {
                var (playerId, position) = samples[i];
                var head = rootGo.transform.Find($"Head_{playerId}");
                if (head == null)
                {
                    var headGo = new GameObject($"Head_{playerId}");
                    Undo.RegisterCreatedObjectUndo(headGo, $"Create Head_{playerId}");
                    headGo.transform.SetParent(rootGo.transform, false);
                    head = headGo.transform;
                }

                head.position = position;

                var canvas = head.Find("BubbleCanvas") as RectTransform;
                if (canvas == null)
                {
                    var canvasGo = new GameObject(
                        "BubbleCanvas",
                        typeof(RectTransform),
                        typeof(Canvas),
                        typeof(CanvasScaler),
                        typeof(GraphicRaycaster));
                    Undo.RegisterCreatedObjectUndo(canvasGo, "Create BubbleCanvas");
                    canvasGo.transform.SetParent(head, false);
                    canvas = canvasGo.GetComponent<RectTransform>();
                    var worldCanvas = canvasGo.GetComponent<Canvas>();
                    worldCanvas.renderMode = RenderMode.WorldSpace;
                    worldCanvas.worldCamera = Camera.main;
                    canvas.sizeDelta = new Vector2(440f, 240f);
                    canvas.localScale = Vector3.one * 0.01f;
                }
                else
                {
                    canvas.sizeDelta = new Vector2(440f, 240f);
                }

                var bubble = canvas.Find("Bubble") as RectTransform;
                if (bubble == null)
                {
                    bubble = GetOrCreateSlot(canvas, "Bubble", new Color(0.1f, 0.1f, 0.12f, 0.92f));
                    SetLabel(bubble, string.Empty);
                }

                Place(bubble, Anchor.Center, Vector2.zero, new Vector2(220f, 72f));
                var label = bubble.Find("Label")?.GetComponent<Text>();
                if (label != null)
                {
                    ApplyText(label, string.Empty, 18, TextAnchor.MiddleCenter);
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Overflow;
                    var labelRect = label.rectTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(12f, 10f);
                    labelRect.offsetMax = new Vector2(-12f, -10f);
                }

                // 이름표는 말풍선과 같은 캔버스를 쓰되 늘 켜져 있다. 말풍선이
                // 뜨는 자리를 비켜 아래에 둔다.
                var nameplate = canvas.Find("Nameplate") as RectTransform;
                if (nameplate == null)
                {
                    var nameplateGo = new GameObject("Nameplate", typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(nameplateGo, "Create Nameplate");
                    nameplateGo.transform.SetParent(canvas, false);
                    nameplate = nameplateGo.GetComponent<RectTransform>();
                }

                var nameLabel = nameplate.GetComponent<Text>();
                if (nameLabel == null)
                {
                    nameLabel = Undo.AddComponent<Text>(nameplate.gameObject);
                }

                // 캔버스 중심은 이제 머리에서 2.6 위다. 캔버스 스케일이 0.01
                // 이므로 -70 은 월드 -0.7, 즉 머리 바로 위에 놓인다. 말풍선은
                // 중심에서 자라 위로 올라가고 이름표는 그 아래에 남는다.
                Place(nameplate, Anchor.Center, new Vector2(0f, -70f), new Vector2(320f, 40f));
                ApplyText(nameLabel, string.Empty, 22, TextAnchor.MiddleCenter);
                nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                nameLabel.verticalOverflow = VerticalWrapMode.Overflow;
                nameLabel.raycastTarget = false;

                bubble.gameObject.SetActive(false);
                anchors.Add(new LobbyChatBubbleAnchor
                {
                    playerId = playerId,
                    headAnchor = head,
                    bubbleRoot = bubble,
                    bubbleText = label,
                    nameText = nameLabel,
                });
            }

            var so = new SerializedObject(view);
            so.FindProperty("visibleSeconds").floatValue = 3.5f;

            // 머리 위로 확실히 올린다. headAnchor 는 캐릭터의 Visual 루트여서
            // 발밑에 가깝고, 1.35 로는 말풍선이 얼굴을 덮었다.
            so.FindProperty("heightOffset").floatValue = 2.6f;
            so.FindProperty("uiFont").objectReferenceValue = ResolveLobbyFont();
            var anchorsProp = so.FindProperty("anchors");
            anchorsProp.arraySize = anchors.Count;
            for (var i = 0; i < anchors.Count; i++)
            {
                var element = anchorsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("playerId").stringValue = anchors[i].playerId;
                element.FindPropertyRelative("headAnchor").objectReferenceValue = anchors[i].headAnchor;
                element.FindPropertyRelative("bubbleRoot").objectReferenceValue = anchors[i].bubbleRoot;
                element.FindPropertyRelative("bubbleText").objectReferenceValue = anchors[i].bubbleText;
                element.FindPropertyRelative("nameText").objectReferenceValue = anchors[i].nameText;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void EnsurePlayerListContent(RectTransform playerList)
        {
            var leftoverLabel = playerList.Find("Label");
            if (leftoverLabel != null)
            {
                leftoverLabel.gameObject.SetActive(false);
            }

            var title = playerList.Find("Title");
            if (title == null)
            {
                var titleGo = CreateTextChild(playerList, "Title", "참가자 목록", 22, TextAnchor.UpperCenter);
                var titleRect = titleGo.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.sizeDelta = new Vector2(-16f, 36f);
                titleRect.anchoredPosition = new Vector2(0f, -10f);
            }
            else
            {
                ApplyText(title.GetComponent<Text>(), "참가자 목록", 22, TextAnchor.UpperCenter);
            }

            if (playerList.Find("BodyText") == null)
            {
                var bodyGo = CreateTextChild(playerList, "BodyText", string.Empty, 20, TextAnchor.UpperLeft);
                var bodyRect = bodyGo.GetComponent<RectTransform>();
                bodyRect.anchorMin = Vector2.zero;
                bodyRect.anchorMax = Vector2.one;
                bodyRect.offsetMin = new Vector2(16f, 16f);
                bodyRect.offsetMax = new Vector2(-16f, -48f);
                var bodyText = bodyGo.GetComponent<Text>();
                bodyText.alignment = TextAnchor.UpperLeft;
                bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                bodyText.verticalOverflow = VerticalWrapMode.Overflow;
                bodyText.lineSpacing = 1.2f;
                bodyGo.SetActive(false);
            }

            if (playerList.Find("RowRoot") == null)
            {
                var rowRootGo = new GameObject("RowRoot", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(rowRootGo, "Create RowRoot");
                rowRootGo.transform.SetParent(playerList, false);
                var rowRoot = rowRootGo.GetComponent<RectTransform>();
                rowRoot.anchorMin = Vector2.zero;
                rowRoot.anchorMax = Vector2.one;
                rowRoot.offsetMin = new Vector2(12f, 12f);
                rowRoot.offsetMax = new Vector2(-12f, -48f);
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

            Place(panel, Anchor.Center, Vector2.zero, new Vector2(420f, 360f));

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

            var buttonSize = new Vector2(260f, 56f);
            var start = EnsureButtonSlot(
                panel, "StartButton", "게임 시작", new Vector2(0f, 52f), buttonSize);
            var leave = EnsureButtonSlot(
                panel, "LeaveButton", "게임 나가기", new Vector2(0f, -20f), buttonSize);
            var resume = EnsureButtonSlot(
                panel, "ResumeButton", "돌아가기", new Vector2(0f, -92f), buttonSize);
            EnsureImage(start.gameObject, new Color(1f, 0.85f, 0.2f, 0.95f));

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
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            return view;
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
            var korean = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/_Game/Content/Fonts/Cafe24Ssurround-v2.0.ttf");
            if (korean != null)
            {
                return korean;
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
