using UnityEngine;

namespace Game.Bootstrap
{
    public static class DesktopResolutionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyDesktopResolution()
        {
            var width = Display.main.systemWidth;
            var height = Display.main.systemHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }
    }
}
