#if UNITY_EDITOR
using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    public class EditorGraphicsResolutionTests
    {
        [Test]
        public void SelectedResolutionReachesEditorEvenWhenBuildTargetIsWeb()
        {
            Assert.That(Application.isPlaying, Is.True);
            var previous = UnityGraphicsSettingsApplier.ApplyEditorGameView;
            var fps = Application.targetFrameRate;
            var sync = QualitySettings.vSyncCount;
            var mip = QualitySettings.globalTextureMipmapLimit;
            var received = Vector2Int.zero;
            try
            {
                UnityGraphicsSettingsApplier.ApplyEditorGameView = (w, h) => received = new Vector2Int(w, h);
                new UnityGraphicsSettingsApplier().Apply(GraphicsCatalog.Shipped.Defaults
                    .With(GraphicsOption.Resolution, "1280x720"));
                Assert.That(received, Is.EqualTo(new Vector2Int(1280, 720)));
            }
            finally
            {
                UnityGraphicsSettingsApplier.ApplyEditorGameView = previous;
                Application.targetFrameRate = fps;
                QualitySettings.vSyncCount = sync;
                QualitySettings.globalTextureMipmapLimit = mip;
            }
        }
    }
}
#endif
