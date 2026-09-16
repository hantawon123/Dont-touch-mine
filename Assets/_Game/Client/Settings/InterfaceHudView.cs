using System;
using System.Collections.Generic;
using Game.Client.Common;
using Game.Client.Home;
using Game.Client.Lobby;
using Game.Client.Match;
using Game.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    // Attached only to gameplay HUDs; settings and lobby menus remain reachable.
    [DefaultExecutionOrder(1000)]
    public sealed class InterfaceHudView : MonoBehaviour
    {
        private InterfaceSettingsSystem settings;
        private CanvasScaler scaler;
        private CanvasGroup visibility;
        private NetworkMatchHudView match;
        private MatchChatView chat;
        private bool chatSuppressed, chatWasEnabled;
        private Func<double?> ping;
        private Func<string> category;
        private TMP_Text counters;
        private GameObject counterRoot;
        private DestroyedItemsHudView destroyedItems;
        private readonly Dictionary<TMP_Text, (float baseline, float applied)> fonts = new();
        private readonly Dictionary<Text, (int baseline, int applied)> legacyFonts = new();
        private KeySettingGuideView[] guides = Array.Empty<KeySettingGuideView>();
        private double nextScan, nextCounter;
        private float elapsed;
        private int frames;

        public const float CategoryFontSize = DestroyedItemsHudView.CategoryFontSize;
        private const float CounterFontSize = 20f;

        public static string FormatCounters(string fpsText, string pingText)
        {
            var fps = string.IsNullOrEmpty(fpsText) ? "" : fpsText;
            var ping = string.IsNullOrEmpty(pingText) ? "" : pingText;
            return fps + (fps.Length > 0 && ping.Length > 0 ? "\n" : "") + ping;
        }

        public static int PerformanceLineCount(string fpsText, string pingText)
        {
            return (string.IsNullOrEmpty(fpsText) ? 0 : 1) + (string.IsNullOrEmpty(pingText) ? 0 : 1);
        }

        public void Bind(InterfaceSettingsSystem value, Func<double?> readPing, Func<string> readCategory = null)
        {
            settings = value; ping = readPing; category = readCategory;
            var canvas = GetComponentInParent<Canvas>();
            scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
            HudScreenScale.EnsureOn(canvas);
            match = GetComponent<NetworkMatchHudView>();
            chat = GetComponentInChildren<MatchChatView>(true);
            if (match != null) visibility = gameObject.AddComponent<CanvasGroup>();
            counterRoot = new GameObject("Performance counters", typeof(Canvas), typeof(CanvasScaler));
            counterRoot.transform.SetParent(transform.parent, false);
            var counterCanvas = counterRoot.GetComponent<Canvas>();
            counterCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            counterCanvas.overrideSorting = true;
            counterCanvas.sortingOrder = 150;
            HudScreenScale.Ensure(counterRoot);
            var text = new GameObject("FPS Ping", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(counterRoot.transform, false);
            counters = text.GetComponent<TextMeshProUGUI>();
            counters.font = HomeUiFonts.Apply(); counters.fontSize = CounterFontSize; counters.raycastTarget = false;
            var rect = counters.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-16, -16); rect.sizeDelta = new Vector2(240, 60);
            counters.alignment = TextAlignmentOptions.TopRight;
        }
        public static float Scale(string code) => code == InterfaceCatalog.Small ? 0.85f : code == InterfaceCatalog.Large ? 1.15f : 1f;

        public static float HudScale(string code) =>
            Scale(code) * HudScreenScale.DefaultScale;

        private void ApplyLobbyMatchInfoSize()
        {
            var lobby = GetComponent<LobbyHudView>();
            if (lobby == null)
            {
                return;
            }

            var info = lobby.transform.Find(LobbyMatchInfoView.RootName) as RectTransform;
            if (info != null)
            {
                info.localScale = Vector3.one / HudScreenScale.DefaultScale;
            }

            var count = lobby.transform.Find(LobbyPlayerCountView.RootName)
                ?.GetComponent<LobbyPlayerCountView>();
            count?.RefreshPlacement(1f / HudScreenScale.DefaultScale);
        }

        private void LateUpdate()
        {
            if (settings == null) return;
            var current = settings.Current;
            if (scaler != null)
            {
                HudScreenScale.Apply(scaler);
                scaler.referenceResolution =
                    HudScreenScale.ScaledReference / HudScale(current.Get(InterfaceOption.UiScale));
                ApplyLobbyMatchInfoSize();
            }
            if (visibility != null)
            {
                var visible = current.IsOn(InterfaceOption.InGameUi) || match.HasEssentialPresentation;
                visibility.alpha = visible ? 1 : 0;
                visibility.blocksRaycasts = visible;
                visibility.interactable = visible;
                if (chat != null && !visible && !chatSuppressed)
                {
                    chatWasEnabled = chat.enabled;
                    chat.enabled = false; // OnDisable releases hidden text-input focus.
                    chatSuppressed = true;
                }
                else if (chat != null && visible && chatSuppressed)
                {
                    chat.enabled = chatWasEnabled;
                    chatSuppressed = false;
                }
            }
            if (Time.unscaledTimeAsDouble >= nextScan)
            {
                nextScan = Time.unscaledTimeAsDouble + 0.5;
                foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                    if (text != counters && !fonts.ContainsKey(text)) fonts[text] = (text.fontSize, text.fontSize);
                foreach (var text in GetComponentsInChildren<Text>(true))
                    if (!legacyFonts.ContainsKey(text)) legacyFonts[text] = (text.fontSize, text.fontSize);
                guides = GetComponentsInChildren<KeySettingGuideView>(true);
                // Remove destroyed dynamic rows, so repeated list refreshes do not retain them.
                deadTmp.Clear(); foreach (var text in fonts.Keys) if (text == null) deadTmp.Add(text);
                foreach (var text in deadTmp) fonts.Remove(text);
                deadLegacy.Clear(); foreach (var text in legacyFonts.Keys) if (text == null) deadLegacy.Add(text);
                foreach (var text in deadLegacy) legacyFonts.Remove(text);
            }
            var scale = Scale(current.Get(InterfaceOption.FontScale));
            tmpKeys.Clear(); tmpKeys.AddRange(fonts.Keys);
            foreach (var text in tmpKeys)
            {
                if (text == null) continue;
                var state = fonts[text];
                if (!Mathf.Approximately(text.fontSize, state.applied)) state.baseline = text.fontSize;
                var size = state.baseline * scale;
                if (!Mathf.Approximately(text.fontSize, size)) text.fontSize = size;
                fonts[text] = (state.baseline, size);
            }
            legacyKeys.Clear(); legacyKeys.AddRange(legacyFonts.Keys);
            foreach (var text in legacyKeys)
            {
                if (text == null) continue;
                var state = legacyFonts[text];
                if (text.fontSize != state.applied) state.baseline = text.fontSize;
                var size = Mathf.RoundToInt(state.baseline * scale);
                if (text.fontSize != size) text.fontSize = size;
                legacyFonts[text] = (state.baseline, size);
            }
            foreach (var guide in guides) if (guide != null) guide.AlwaysVisible = current.IsOn(InterfaceOption.BeginnerGuide);
            ApplyCategory();
            elapsed += Time.unscaledDeltaTime; frames++;
            if (Time.unscaledTimeAsDouble < nextCounter) return;
            nextCounter = Time.unscaledTimeAsDouble + 0.5;
            var fpsText = current.IsOn(InterfaceOption.FpsCounter) ? $"{frames / Mathf.Max(elapsed, 0.001f):F0} FPS" : "";
            var rtt = current.IsOn(InterfaceOption.PingCounter) ? ping?.Invoke() : null;
            var pingText = current.IsOn(InterfaceOption.PingCounter) ? rtt.HasValue ? $"{rtt.Value:F0} ms" : "Ping —" : "";
            counters.text = FormatCounters(fpsText, pingText);
            counters.fontSize = CounterFontSize * scale;
            elapsed = 0; frames = 0;
        }

        private void ApplyCategory()
        {
            if (category == null)
            {
                return;
            }

            if (destroyedItems == null && match != null)
            {
                destroyedItems = match.GetComponentInChildren<DestroyedItemsHudView>(true);
            }

            destroyedItems?.SetCategory(category.Invoke()?.Trim() ?? "");
        }

        private readonly List<TMP_Text> deadTmp = new(), tmpKeys = new();
        private readonly List<Text> deadLegacy = new(), legacyKeys = new();
        private void OnDestroy() { if (counterRoot != null) Destroy(counterRoot); }
    }
}
