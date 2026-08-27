using System;
using Game.Core.Settings;

namespace Game.Client.Graphics
{
    public static class GraphicsSettingsOutput
    {
        public static GraphicsSettingsState Current { get; private set; }

        public static event Action<GraphicsSettingsState> Changed;

        internal static void Publish(GraphicsSettingsState settings)
        {
            Current = settings ?? throw new ArgumentNullException(nameof(settings));
            Changed?.Invoke(settings);
        }
    }
}
