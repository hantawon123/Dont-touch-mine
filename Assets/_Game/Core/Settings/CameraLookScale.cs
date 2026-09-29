namespace Game.Core.Settings
{
    public static class CameraLookScale
    {
        // 50 preserves the existing turn speed; 0 stops that input and 100 doubles it.
        // Third-person mouse and camera sliders multiply, independently of first person.
        public static (float x, float y) From(ControlSettings settings, bool firstPerson)
        {
            var mouse = firstPerson ? ControlSensitivity.FirstPersonMouse : ControlSensitivity.ThirdPersonMouse;
            var scale = Ratio(settings.Get(mouse));
            if (!firstPerson) scale *= Ratio(settings.Get(ControlSensitivity.ThirdPersonCamera));
            var invertX = firstPerson ? ControlToggle.FirstPersonInvertX : ControlToggle.ThirdPersonInvertX;
            var invertY = firstPerson ? ControlToggle.FirstPersonInvertY : ControlToggle.ThirdPersonInvertY;
            return (settings.Get(invertX) == InterfaceCatalog.On ? -scale : scale,
                settings.Get(invertY) == InterfaceCatalog.On ? -scale : scale);
        }

        private static float Ratio(int value) =>
            System.Math.Clamp(value < 0 ? ControlCatalog.DefaultSensitivity : value, 0, 100) / 50f;
    }
}
