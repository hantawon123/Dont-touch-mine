using System.Linq;
using Game.Client;
using Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class CharacterTestCarryHitPreviewTests
    {
        private static readonly string[] Motions =
        {
            "Carry_TwoHands_Hit",
            "Carry_TwoHands_Hit_Walk",
            "Carry_TwoHands_Hit_Run",
            "Carry_TwoHands_Hit_Crouch",
            "Carry_TwoHands_Hit_Crouch_Walk",
            "Hit_Prone",
            "Hit_Crawl",
            "Carry_TwoHands_Hit_Prone",
            "Carry_TwoHands_Hit_Crawl",
        };

        [Test]
        public void CarryTwoHandsHitClipsExistWithCurves()
        {
            foreach (var name in Motions)
            {
                var clip = SmoothBearAssets.LoadClip(name);
                if (clip == null || clip.length < 0.2f || AnimationUtility.GetCurveBindings(clip).Length < 8)
                {
                    CarryTwoHandsHitClipBaker.Bake();
                    clip = SmoothBearAssets.LoadClip(name);
                }

                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.length, Is.GreaterThan(0.2f), name);
                Assert.That(AnimationUtility.GetCurveBindings(clip).Length, Is.GreaterThan(8), name);
            }
        }

        [Test]
        public void ControllersBindCarryTwoHandsHitMotions()
        {
            if (Motions.Any(name => SmoothBearAssets.LoadClip(name) == null))
            {
                CarryTwoHandsHitClipBaker.Bake();
            }

            AssertController(SmoothBearAssets.PreviewControllerPath);
            AssertController(SmoothBearAssets.PlayerControllerPath);
        }

        [Test]
        public void ApplyCharacterTestPreviewSerializesTheFiveMotionsAndSaves()
        {
            Assert.That(CharacterTestPreviewSetup.ApplyBlenderPreview(), Is.True);
            var scene = EditorSceneManager.OpenScene(CharacterTestPreviewSetup.ScenePath);
            var preview = scene.GetRootGameObjects().First(root => root.name == "SmoothBear");
            var driver = preview.GetComponent<CharacterTestPreviewDriver>();
            Assert.That(driver, Is.Not.Null);
            var serialized = new SerializedObject(driver).FindProperty("stateNames");
            var names = Enumerable.Range(0, serialized.arraySize)
                .Select(index => serialized.GetArrayElementAtIndex(index).stringValue)
                .ToArray();
            Assert.That(names, Is.SupersetOf(Motions));
            Assert.That(CharacterTestPreviewDriver.CategoryOf("Carry_TwoHands_Hit"), Is.EqualTo("들고 Hit"));
        }

        [Test]
        public void PreviewDriverExposesCarryHitButtons()
        {
            var names = typeof(CharacterTestPreviewDriver)
                .GetField("DefaultMotions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .GetValue(null) as string[];
            Assert.That(names, Is.SupersetOf(Motions));
        }

        private static void AssertController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.That(controller, Is.Not.Null, path);
            var states = controller.layers[0].stateMachine.states.Select(entry => entry.state).ToArray();
            foreach (var name in Motions)
            {
                var state = states.FirstOrDefault(entry => entry.name == name);
                Assert.That(state, Is.Not.Null, path + " " + name);
                Assert.That(state.motion, Is.InstanceOf<AnimationClip>(), path + " " + name);
                Assert.That(state.motion.name, Is.EqualTo(name), path + " " + name);
            }
        }
    }
}
