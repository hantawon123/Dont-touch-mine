using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Common
{
    /// <summary>
    /// 게임 전반의 UI 버튼을 눌렀을 때 한 번 나는 클릭 효과음.
    /// </summary>
    /// <remarks>
    /// Lives on the project root so every screen hears the same cue without
    /// each view wiring its own clip. The click is a HUD sound (2D, no
    /// attenuation/panning/doppler) and follows Effects volume. Playback
    /// matches <see cref="Button.onClick"/>: press and release on the same
    /// interactable button, ignoring locked-cursor gameplay and clicks that
    /// land on a non-button that is sitting on top. The approved clip is the
    /// store pack's thick mouse click, assigned on
    /// <c>ProjectLifetimeScope</c> and loaded lazily so edit-mode tests can
    /// still inspect the component.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UiButtonClickAudio : MonoBehaviour
    {
        public const string RootName = "UiButtonClickAudio";
        public const string ClipAssetPath =
            "Assets/Free UI Click Sound Effects Pack/AUDIO/Button/SFX_UI_Button_Mouse_Thick_Generic_2.wav";

        /// <summary>효과음 볼륨. 버튼 클릭음에 적용된다.</summary>
        public static float EffectsVolume { get; set; } = 1f;

        private readonly List<RaycastResult> hits = new();
        private AudioSource clickSource;
        private AudioClip clickClip;
        private PointerEventData pointer;
        private GameObject pressedHandler;
        private bool sourceReady;

        /// <summary>위에 가린 비버튼이 없고, 같은 버튼을 눌렀다 뗐을 때만 친다.</summary>
        public static bool ShouldPlay(GameObject handler, bool pointerLocked = false) =>
            !pointerLocked &&
            handler != null &&
            handler.TryGetComponent<Button>(out var button) &&
            button.IsActive() &&
            button.IsInteractable();

        /// <summary>
        /// EventSystem과 같이 맨 위 히트의 부모 사슬에서 클릭 핸들러를 찾는다.
        /// 위에 있는 그래픽이 버튼을 가리면 그 아래 버튼은 쓰지 않는다.
        /// </summary>
        public static GameObject ResolveClickHandler(IReadOnlyList<RaycastResult> results)
        {
            if (results == null || results.Count == 0 || results[0].gameObject == null)
            {
                return null;
            }

            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject);
        }

        public static UiButtonClickAudio Create(Transform parent, AudioClip clip)
        {
            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(parent, false);
            var audio = rootObject.AddComponent<UiButtonClickAudio>();
            audio.clickClip = clip;
            return audio;
        }

        private void Update()
        {
            if (WasPrimaryPressed())
            {
                pressedHandler = HandlerUnderPointer();
            }

            if (!WasPrimaryReleased())
            {
                return;
            }

            var released = HandlerUnderPointer();
            var pressed = pressedHandler;
            pressedHandler = null;
            if (pressed == released)
            {
                PlayIfButton(pressed);
            }
        }

        private void PlayIfButton(GameObject handler)
        {
            if (!ShouldPlay(handler, WebPointerInput.IsLocked))
            {
                return;
            }

            EnsureSource();
            if (clickSource == null || clickClip == null)
            {
                return;
            }

            clickSource.volume = Mathf.Clamp01(EffectsVolume);
            clickSource.PlayOneShot(clickClip);
        }

        private GameObject HandlerUnderPointer()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            if (pointer == null || pointer.currentInputModule != eventSystem.currentInputModule)
            {
                pointer = new PointerEventData(eventSystem);
            }

            pointer.Reset();
            pointer.position = PointerPosition();
            hits.Clear();
            eventSystem.RaycastAll(pointer, hits);
            return ResolveClickHandler(hits);
        }

        private void EnsureSource()
        {
            if (sourceReady)
            {
                return;
            }

            sourceReady = true;
            var audioObject = new GameObject("Click");
            audioObject.transform.SetParent(transform, false);
            clickSource = audioObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.loop = false;
            // A HUD cue, so no attenuation, panning or doppler.
            clickSource.spatialBlend = 0f;
            clickSource.dopplerLevel = 0f;
        }

        private static Vector2 PointerPosition()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            if (Touchscreen.current != null)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }

            return Vector2.zero;
        }

        private static bool WasPrimaryPressed() =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame ||
            Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        private static bool WasPrimaryReleased() =>
            Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame ||
            Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
    }
}
