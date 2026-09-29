using Game.Client.Common;
using NUnit.Framework;
using UnityEditor;

namespace Game.Architecture.Tests
{
    public sealed class LoadingSceneTests
    {
        [Test]
        public void LoadingScene_IsNamedAndListedInTheBuild()
        {
            Assert.That(LoadingScene.IsLoading("Loading"), Is.True);
            Assert.That(LoadingScene.IsLoading("Home"), Is.False);

            var listed = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && scene.path == LoadingScene.Path)
                {
                    listed = true;
                    break;
                }
            }

            Assert.That(listed, Is.True);
        }
    }
}
