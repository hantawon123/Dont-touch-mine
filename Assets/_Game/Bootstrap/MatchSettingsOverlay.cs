using System;
using Game.Client.Cameras;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Client.Players;
using Game.Client.Settings;
using Game.Core.Match;
using Game.Network.Session;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class MatchSettingsOverlay : IStartable, ITickable, IDisposable
    {
        private readonly SettingsView view;
        private readonly SettingsPresenter presenter;
        private readonly LobbyExitPresenter exit;
        private readonly MatchChatView chat;
        private readonly NetworkRunnerService network;
        private PlayerCameraController camera;
        private bool chatWasEnabled;
        private bool layoutConfigured;
        private int dismissedFrame = -1;
        private int restoreCursorFrame = -1;
        private bool ownsGameplayCursor;
        private float nextCursorDiagnostic;
        public bool IsOpen { get; private set; }

        public MatchSettingsOverlay(SettingsView view, SettingsPresenter presenter,
            LobbyExitPresenter exit, MatchChatView chat, NetworkRunnerService network)
        {
            this.view = view;
            this.presenter = presenter;
            this.exit = exit;
            this.chat = chat;
            this.network = network;
        }

        public static bool BlocksEscapeDuringPresentation(MatchPhase phase)
        {
            return phase == MatchPhase.Highlight || phase == MatchPhase.Result;
        }

        public static bool ShouldHandleEscape(
            bool textFocused,
            bool capturing,
            bool modalBlocking,
            bool consumedEscape,
            bool presentationBlocks = false)
        {
            return !textFocused &&
                   !capturing &&
                   !modalBlocking &&
                   !consumedEscape &&
                   !presentationBlocks;
        }

        public void Start()
        {
            presenter.LeaveGameConfirmed += Leave;
            view.Closed += OnClosed;
            view.ConfirmDismissed += OnPanelDismissed;
            view.FeedbackDismissed += OnPanelDismissed;
        }

        public void Tick()
        {
            if (ownsGameplayCursor && !IsOpen && network.IsRuntimeReady &&
                !network.IsWaitingForMatch && !network.IsHighlightInProgress && !network.IsResultSceneLoaded &&
                Application.isFocused && !PlayerMovement.IsTextInputFocused() &&
                (Keyboard.current == null || !Keyboard.current.escapeKey.isPressed) &&
                (Cursor.lockState != CursorLockMode.Locked || Cursor.visible) && camera != null)
            {
                camera.SetCursorCaptureEnabled(true);
                if (Time.unscaledTime >= nextCursorDiagnostic)
                {
                    nextCursorDiagnostic = Time.unscaledTime + 1f;
                    Debug.Log($"[QA-Cursor] maintained gameplay capture frame={Time.frameCount} lock={Cursor.lockState} visible={Cursor.visible}");
                }
            }
            if (restoreCursorFrame >= 0 && Time.frameCount > restoreCursorFrame &&
                (Keyboard.current == null || !Keyboard.current.escapeKey.isPressed))
            {
                restoreCursorFrame = -1;
                if (!IsOpen && Application.isFocused && !network.IsResultSceneLoaded &&
                    !network.IsHighlightInProgress && !PlayerMovement.IsTextInputFocused())
                {
                    if (camera != null)
                    {
                        camera.SetEscapeReleasesCursor(false);
                        // Force a fresh native capture after the Escape event has finished.
                        Cursor.lockState = CursorLockMode.None;
                        camera.SetCursorCaptureEnabled(true);
                    }
                    Debug.Log($"[QA-Cursor] deferred restore frame={Time.frameCount} lock={Cursor.lockState} focus={Application.isFocused} rig={(camera == null ? 0 : camera.GetInstanceID())}");
                }
            }
            if (network.IsResultSceneLoaded || network.IsHighlightInProgress || network.IsWaitingForMatch)
            {
                if (IsOpen) view.gameObject.SetActive(false);
                return;
            }

            if (IsOpen)
            {
                if (dismissedFrame != Time.frameCount && Keyboard.current != null &&
                    Keyboard.current.escapeKey.wasPressedThisFrame) view.RequestBack();
                return;
            }
            if (!network.IsRuntimeReady ||
                Keyboard.current == null ||
                !Keyboard.current.escapeKey.wasPressedThisFrame ||
                !ShouldHandleEscape(
                    PlayerMovement.IsTextInputFocused() ||
                    (chat != null && chat.ConsumedEscapeThisFrame),
                    false,
                    view.BlocksEscape,
                    view.ConsumedEscapeThisFrame))
            {
                return;
            }

            restoreCursorFrame = -1;
            ownsGameplayCursor = false;
            IsOpen = true;
            chatWasEnabled = chat.enabled;
            chat.enabled = false;
            camera = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
            if (camera != null)
            {
                camera.SetEscapeReleasesCursor(false);
                camera.SetCursorCaptureEnabled(false);
            }
            view.gameObject.SetActive(true);
            ConfigureLayout();
            MatchTransitionDiagnostics.Dump("settings-open");
        }

        private void OnPanelDismissed() => dismissedFrame = Time.frameCount;

        private void ConfigureLayout()
        {
            if (layoutConfigured) return;
            var canvas = view.GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;
            ConfigureCanvas(canvas);
            layoutConfigured = true;
        }

        internal static void ConfigureCanvas(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvas.sortingOrder = 10000;
            var content = new GameObject("Match Settings Content", typeof(RectTransform))
                .GetComponent<RectTransform>();
            content.SetParent(canvas.transform, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(1920f, 1080f);
            // Preserve sibling order: the background must remain behind the menu.
            while (canvas.transform.GetChild(0) != content)
                canvas.transform.GetChild(0).SetParent(content, false);
        }

        private void OnClosed()
        {
            if (!IsOpen) return;
            IsOpen = false;
            presenter.Open();
            if (chat != null) chat.enabled = chatWasEnabled;
            if (camera != null && !network.IsResultSceneLoaded && !network.IsHighlightInProgress)
                camera.SetCursorCaptureEnabled(true);
            ownsGameplayCursor = true;
            restoreCursorFrame = Time.frameCount;
            MatchTransitionDiagnostics.Dump("settings-closed");
        }

        private void Leave()
        {
            if (!IsOpen) return;
            view.gameObject.SetActive(false);
            restoreCursorFrame = -1;
            ownsGameplayCursor = false;
            exit.RequestLeave();
        }

        public void Dispose()
        {
            if (camera != null) camera.SetEscapeReleasesCursor(true);
            presenter.LeaveGameConfirmed -= Leave;
            view.Closed -= OnClosed;
            view.ConfirmDismissed -= OnPanelDismissed;
            view.FeedbackDismissed -= OnPanelDismissed;
            if (IsOpen && chat != null) chat.enabled = chatWasEnabled;
        }
    }
}
