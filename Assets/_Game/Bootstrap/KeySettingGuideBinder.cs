using Game.Client;
using Game.Client.Interactions;
using Game.Client.Voice;
using Game.Core.Settings;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Hands the 컨트롤 tab's applied keys to the on-screen guide, the
    /// lobby/match voice plates, and world interaction prompts for as long as
    /// the application lives.
    /// </summary>
    public sealed class KeySettingGuideBinder : IStartable, System.IDisposable
    {
        private readonly ControlSettingsSystem settings;

        public KeySettingGuideBinder(ControlSettingsSystem settings) => this.settings = settings;

        public void Start()
        {
            KeySettingGuideView.UseSettings(settings);
            VoiceView.UseSettings(settings);
            PlayerInteractor.UseSettings(settings);
        }

        public void Dispose()
        {
            KeySettingGuideView.UseSettings(null);
            VoiceView.UseSettings(null);
            PlayerInteractor.UseSettings(null);
        }
    }
}
