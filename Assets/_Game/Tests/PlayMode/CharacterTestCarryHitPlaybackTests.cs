#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Game.Client;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class CharacterTestCarryHitPlaybackTests
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

        [UnityTest]
        public IEnumerator CarryTwoHandsHitStatesPlayOnPreviewController()
        {
            const string directory = "Assets/_Game/Content/Characters/SmoothBear/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directory + "SmoothBear.fbx");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                directory + "SmoothBearPreview.controller");
            var character = Object.Instantiate(prefab);
            try
            {
                var animator = character.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var driver = character.AddComponent<CharacterTestPreviewDriver>();
                driver.ConfigureMotions(Motions);
                yield return null;
                foreach (var name in Motions)
                {
                    Assert.That(driver.GetComponent<CharacterTestPreviewDriver>(), Is.Not.Null);
                    animator.Play(name, 0, 0f);
                    animator.Update(0f);
                    Assert.That(
                        animator.GetCurrentAnimatorStateInfo(0).IsName(name),
                        Is.True,
                        name);
                    animator.Update(0.1f);
                    yield return null;
                }

                Assert.That(CharacterTestPreviewDriver.CategoryOf(Motions[0]), Is.EqualTo("들고 Hit"));
            }
            finally
            {
                Object.Destroy(character);
            }
        }
    }
}
#endif
