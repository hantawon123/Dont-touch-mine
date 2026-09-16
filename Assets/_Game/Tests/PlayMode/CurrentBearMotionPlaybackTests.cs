#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class CurrentBearMotionPlaybackTests
    {
        [UnityTest]
        public IEnumerator AllCharacterTestStatesEvaluateOnCurrentBear()
        {
            const string directory = "Assets/_Game/Content/Characters/SmoothBear/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(directory + "SmoothBear.fbx");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(directory + "SmoothBearPreview.controller");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            var character = Object.Instantiate(prefab);
            try
            {
                var animator = character.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
                Assert.That(states.Length, Is.EqualTo(139));
                var body = character.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "Body");
                Assert.That(body.sharedMesh.blendShapeCount, Is.EqualTo(2));
                var transforms = character.GetComponentsInChildren<Transform>();
                yield return null;
                foreach (var state in states)
                {
                    Assert.That(state.motion, Is.InstanceOf<AnimationClip>(), state.name);
                    var clip = (AnimationClip)state.motion;
                    animator.Rebind();
                    animator.Play(state.name, 0, 0f);
                    animator.Update(0f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                        Is.EqualTo(Animator.StringToHash(state.name)), state.name);
                    var frames = Mathf.CeilToInt(clip.length * 30) + 2;
                    for (var f = 0; f < frames; f++)
                    {
                        animator.Update(1f / 30f);
                        foreach (var bone in transforms)
                        {
                            var p = bone.localPosition;
                            var q = bone.localRotation;
                            Assert.That(float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude), Is.False, state.name);
                            Assert.That(float.IsNaN(q.x + q.y + q.z + q.w), Is.False, state.name);
                        }
                    }
                    Debug.Log("CURRENT_BEAR_PLAYED " + state.name);
                    yield return null;
                }
            }
            finally
            {
                Object.Destroy(character);
            }
        }
    }
}
#endif
