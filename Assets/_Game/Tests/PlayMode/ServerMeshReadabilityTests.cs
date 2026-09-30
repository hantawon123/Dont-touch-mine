#if UNITY_EDITOR && UNITY_SERVER
using System.Collections.Generic;
using System.Linq;
using Game.Client.Interactions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class ServerMeshReadabilityTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ReleasingVisualCpuCopiesPreservesAnimatedBonesAndHoldPoint(bool stripBlendShapes)
        {
            var modelPaths = new[] {
                "Assets/_Game/Content/Characters/SmoothBear/SmoothBear.fbx",
                "Assets/_Game/Content/Characters/SmoothBear/Hoods/AnimalHoods.fbx"
            };
            var actors = new GameObject[2];
            var clones = new Dictionary<Mesh,Mesh>();
            var removedBlendShapes = 0;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
                for (var i=0;i<2;i++) {
                    actors[i]=Object.Instantiate(prefab);
                    foreach (var script in actors[i].GetComponentsInChildren<MonoBehaviour>(true)) script.enabled=false;
                    actors[i].GetComponentInChildren<Animator>().enabled=false;
                }
                Mesh Unreadable(Mesh source) {
                    if (source == null || !modelPaths.Contains(AssetDatabase.GetAssetPath(source))) return source;
                    if (clones.TryGetValue(source,out var found)) return found;
                    var copy=Object.Instantiate(source);
                    var bounds=copy.bounds; var count=copy.vertexCount;
                    if (stripBlendShapes) {
                        removedBlendShapes += copy.blendShapeCount;
                        copy.ClearBlendShapes();
                        Assert.That(copy.blendShapeCount,Is.Zero);
                    }
                    copy.UploadMeshData(true);
                    Assert.That(copy.isReadable,Is.False);
                    Assert.That(copy.bounds,Is.EqualTo(bounds));
                    Assert.That(copy.vertexCount,Is.EqualTo(count));
                    clones.Add(source,copy); return copy;
                }
                foreach (var collider in actors[1].GetComponentsInChildren<MeshCollider>(true))
                    Assert.That(modelPaths.Contains(AssetDatabase.GetAssetPath(collider.sharedMesh)),Is.False,
                        "An audited visual model must not be used by a collider.");
                foreach (var filter in actors[1].GetComponentsInChildren<MeshFilter>(true)) filter.sharedMesh=Unreadable(filter.sharedMesh);
                foreach (var skin in actors[1].GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.sharedMesh=Unreadable(skin.sharedMesh);
                Assert.That(clones.Count,Is.GreaterThan(0),"Positive control: the candidate must release actual visual mesh copies.");
                var animators=actors.Select(x=>x.GetComponentInChildren<Animator>()).ToArray();
                var interactors=actors.Select(x=>x.GetComponent<PlayerInteractor>()).ToArray();
                var transforms=animators.Select(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
                Assert.That(transforms[0].Length,Is.EqualTo(transforms[1].Length));
                var clips=animators[0].runtimeAnimatorController.animationClips.Distinct().ToArray();
                var holdPositions=new HashSet<Vector3>();
                var poses=0;
                foreach (var clip in clips) foreach (var fraction in new[] {0f,.37f,.83f}) {
                    for (var i=0;i<2;i++) {
                        clip.SampleAnimation(animators[i].gameObject,clip.length*fraction);
                        interactors[i].RefreshHoldPoint();
                    }
                    for (var i=0;i<transforms[0].Length;i++) {
                        Assert.That(Vector3.Distance(transforms[0][i].localPosition,transforms[1][i].localPosition),Is.LessThan(.000001f),clip.name);
                        var a=transforms[0][i].localRotation; var b=transforms[1][i].localRotation;
                        Assert.That(Mathf.Abs(a.x-b.x)+Mathf.Abs(a.y-b.y)+Mathf.Abs(a.z-b.z)+Mathf.Abs(a.w-b.w),Is.LessThan(.000001f),clip.name);
                        Assert.That(Vector3.Distance(transforms[0][i].localScale,transforms[1][i].localScale),Is.LessThan(.000001f),clip.name);
                    }
                    Assert.That(Vector3.Distance(interactors[0].HoldPoint.position,interactors[1].HoldPoint.position),Is.LessThan(.000001f),clip.name);
                    holdPositions.Add(interactors[0].HoldPoint.position); poses++;
                }
                if (stripBlendShapes) Assert.That(removedBlendShapes,Is.GreaterThan(0));
                Assert.That(clips.Length,Is.GreaterThan(10));
                Assert.That(holdPositions.Count,Is.GreaterThan(1),"Positive control: animation must actually move the gameplay hold point.");
                LogAssert.NoUnexpectedReceived();
                TestContext.WriteLine($"removedBlendShapes={removedBlendShapes} meshes={clones.Count} clips={clips.Length} sampledPoses={poses} distinctHoldPositions={holdPositions.Count}");
            }
            finally {
                foreach (var actor in actors) if (actor != null) Object.DestroyImmediate(actor);
                foreach (var copy in clones.Values) Object.DestroyImmediate(copy);
            }
        }
    }
}
#endif
