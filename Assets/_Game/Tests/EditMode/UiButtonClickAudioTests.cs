using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Common;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests.EditMode
{
    public sealed class UiButtonClickAudioTests
    {
        [Test]
        public void ProjectUsesTheApprovedThickMouseClick()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiButtonClickAudio.ClipAssetPath);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.name, Is.EqualTo("SFX_UI_Button_Mouse_Thick_Generic_2"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Bootstrap/ProjectLifetimeScope.prefab");
            var assigned = new SerializedObject(prefab.GetComponent<ProjectLifetimeScope>())
                .FindProperty("_uiButtonClick").objectReferenceValue as AudioClip;
            Assert.That(assigned, Is.SameAs(clip));
        }

        [Test]
        public void EffectsVolumeFollowsSoundSettings()
        {
            var previous = UiButtonClickAudio.EffectsVolume;
            try
            {
                new UnitySoundSettingsApplier().Apply(
                    SoundCatalog.Defaults.With(SoundVolume.Effects, 50));
                Assert.That(UiButtonClickAudio.EffectsVolume, Is.EqualTo(.5f).Within(.001f));
            }
            finally
            {
                UiButtonClickAudio.EffectsVolume = previous;
            }
        }

        [Test]
        public void InteractableButtonPlaysUnlessThePointerIsLocked()
        {
            var button = NewButton("UiClickTestButton");
            try
            {
                Assert.That(UiButtonClickAudio.ShouldPlay(button.gameObject), Is.True);
                Assert.That(UiButtonClickAudio.ShouldPlay(button.gameObject, pointerLocked: true), Is.False);
                button.interactable = false;
                Assert.That(UiButtonClickAudio.ShouldPlay(button.gameObject), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(button.gameObject);
            }
        }

        [Test]
        public void ANonButtonDoesNotPlay()
        {
            var decoy = new GameObject("UiClickTestDecoy", typeof(Image));
            try
            {
                Assert.That(UiButtonClickAudio.ShouldPlay(decoy), Is.False);
                Assert.That(UiButtonClickAudio.ShouldPlay(null), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(decoy);
            }
        }

        [Test]
        public void OnlyTheTopmostClickHandlerCounts()
        {
            var button = NewButton("UiClickTestUnder");
            var blocker = new GameObject("UiClickTestBlocker", typeof(Image));
            try
            {
                var throughChild = new List<RaycastResult>
                {
                    new() { gameObject = button.transform.GetChild(0).gameObject }
                };
                Assert.That(
                    UiButtonClickAudio.ResolveClickHandler(throughChild),
                    Is.EqualTo(button.gameObject));

                var blocked = new List<RaycastResult>
                {
                    new() { gameObject = blocker },
                    new() { gameObject = button.gameObject }
                };
                Assert.That(UiButtonClickAudio.ResolveClickHandler(blocked), Is.Null);
                Assert.That(UiButtonClickAudio.ResolveClickHandler(new List<RaycastResult>()), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(blocker);
                Object.DestroyImmediate(button.gameObject);
            }
        }

        private static Button NewButton(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var graphic = new GameObject("Graphic", typeof(RectTransform), typeof(Image));
            graphic.transform.SetParent(root.transform, false);
            return root.GetComponent<Button>();
        }
    }
}
