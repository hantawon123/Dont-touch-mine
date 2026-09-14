using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Client.Common
{
    public static class WebPointerInput
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GamePointerArm(int enabled);
        [DllImport("__Internal")] private static extern int GamePointerIsLocked();
        [DllImport("__Internal")] private static extern void GamePointerRelease(int allowResume);
        [DllImport("__Internal")] private static extern int GamePointerConsumeBrowserRelease();
        private static int releaseFrame = -1;
        private static bool browserReleased;
#endif
        // Browser acquisition does not set Unity's requested Cursor.lockState.
        // All gameplay gates must observe the same actual capture state.
        public static bool IsLocked
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return GamePointerIsLocked() != 0;
#else
                return Cursor.lockState == CursorLockMode.Locked;
#endif
            }
        }

        // Both lobby and match presenters may tick in one frame. Give them the
        // same event snapshot; their existing phase guards select the active UI.
        public static bool ReleasedByBrowserThisFrame
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (releaseFrame != Time.frameCount)
                {
                    releaseFrame = Time.frameCount;
                    browserReleased = GamePointerConsumeBrowserRelease() != 0;
                }
                return browserReleased;
#else
                return false;
#endif
            }
        }

        public static void Release(bool allowFullscreenResume = false)
        {
            Arm(false);
#if UNITY_WEBGL && !UNITY_EDITOR
            GamePointerRelease(allowFullscreenResume ? 1 : 0);
#endif
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Arm(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GamePointerArm(enabled ? 1 : 0);
#endif
        }
    }
}
