using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    public static class SmoothBearProjectValidation
    {
        [Serializable] class Report
        {
            public int clips, previewStates, playerStates, animatorStatesChecked, volumeBones;
            public List<string> errors = new();
            public bool sceneSaved, prefabMaterialsValid, noLegacyDependencies;
        }
        public static void Run() => Validate(true);
        [MenuItem("Game/Setup/Validate SmoothBear")]
        public static void RunInEditor() => Validate(false);
        [InitializeOnLoadMethod]
        static void WatchRequestedValidation()
        {
            EditorApplication.update += CheckRequest;
        }
        static void CheckRequest()
        {
            const string request="artifacts/smooth-bear-unity/run-project-validation.request";
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||Application.isPlaying||!File.Exists(request))return;
            string command=File.ReadAllText(request);
            bool render=command.StartsWith("render",StringComparison.Ordinal);
            File.Delete(request);
            if(command.StartsWith("retarget",StringComparison.Ordinal))EditorApplication.delayCall += RetargetAttachments;
            else if(command.StartsWith("customize",StringComparison.Ordinal))EditorApplication.delayCall += SmoothBearCustomizationSetup.Run;
            else if(render)EditorApplication.delayCall += RenderInEditor;
            else EditorApplication.delayCall += RunInEditor;
        }
        public static void RetargetAttachments()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.ModelPath);
            int removed=0;
            foreach(var clip in SmoothBearAssets.LoadClips())
            {
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var target=model.transform.Find(binding.path);
                    if(binding.type==typeof(Transform)&&target!=null&&target.GetComponent<Renderer>()!=null)
                    {
                        AnimationUtility.SetEditorCurve(clip,binding,null);removed++;
                    }
                }
                EditorUtility.SetDirty(clip);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("artifacts/smooth-bear-unity/attachment-retarget.txt","Removed old mesh transform curves: "+removed);
            RunInEditor();RenderInEditor();
        }
        public static void RenderInEditor()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Application.isPlaying)
            {
                File.WriteAllText("artifacts/smooth-bear-unity/run-project-validation.request","render");
                return;
            }
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/CharacterTest.unity")throw new Exception("Open CharacterTest for rendering");
            var original=scene.GetRootGameObjects().Single(o=>o.name=="SmoothBear");
            var renderers=original.GetComponentsInChildren<Renderer>();
            var enabled=renderers.Select(r=>r.enabled).ToArray();
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
            var cameraObject=new GameObject("SmoothBear_RenderCamera");
            var camera=cameraObject.AddComponent<Camera>();
            camera.CopyFrom(Camera.main);camera.enabled=false;
            var rt=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try
            {
                foreach(var r in renderers)r.enabled=false;
                instance.transform.position=original.transform.position;
                instance.GetComponent<Animator>().enabled=false;
                camera.transform.position=original.transform.position+new Vector3(2.5f,2.0f,4.8f);
                camera.transform.LookAt(original.transform.position+new Vector3(0,1.0f,0));
                camera.fieldOfView=32;
                foreach(var name in new[]{"Idle","Walk_Forward","Crouch_Idle","Crawl_Forward","Carry_TwoHands"})
                {
                    camera.transform.position=original.transform.position+(name=="Idle"?new Vector3(0,1.05f,5):
                        name=="Crawl_Forward"?new Vector3(4,1.35f,1.5f):new Vector3(2.5f,2.0f,4.8f));
                    camera.transform.LookAt(original.transform.position+new Vector3(0,name=="Crawl_Forward"?.65f:1.0f,0));
                    SmoothBearAssets.LoadClip(name).SampleAnimation(instance,.3f);
                    // Bake each pose explicitly: multiple render requests in one editor
                    // frame can otherwise reuse the GPU skinning data of the first pose.
                    var posedMeshes=new List<GameObject>();
                    var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach(var skin in skins)
                    {
                        var posed=new GameObject("Pose_"+skin.name);
                        posed.transform.SetParent(skin.transform,false);
                        var mesh=new Mesh();skin.BakeMesh(mesh);
                        posed.AddComponent<MeshFilter>().sharedMesh=mesh;
                        posed.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                        skin.enabled=false;posedMeshes.Add(posed);
                    }
                    var visible=instance.GetComponentsInChildren<MeshRenderer>();
                    var bounds=visible[0].bounds;
                    foreach(var renderer in visible.Skip(1))bounds.Encapsulate(renderer.bounds);
                    var direction=(camera.transform.position-original.transform.position).normalized;
                    camera.transform.position=bounds.center+direction*5;
                    camera.transform.LookAt(bounds.center);
                    camera.orthographic=true;camera.orthographicSize=bounds.extents.magnitude*1.05f;
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                    RenderTexture.active=rt;
                    var image=new Texture2D(768,768,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
                    File.WriteAllBytes("artifacts/smooth-bear-unity/unity-"+name+".png",image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                    foreach(var posed in posedMeshes)
                    {
                        UnityEngine.Object.DestroyImmediate(posed.GetComponent<MeshFilter>().sharedMesh);
                        UnityEngine.Object.DestroyImmediate(posed);
                    }
                    foreach(var skin in skins)skin.enabled=true;
                }
                File.WriteAllText("artifacts/smooth-bear-unity/render-complete.txt","Five poses rendered from the installed Unity prefab.");
            }
            finally
            {
                RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(instance);
                for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];
                if(!Application.isPlaying)EditorSceneManager.SaveScene(scene);
            }
        }
        static void Validate(bool exit)
        {
            if(!exit&&(EditorApplication.isPlayingOrWillChangePlaymode||Application.isPlaying))
            {
                File.WriteAllText("artifacts/smooth-bear-unity/run-project-validation.request","validate");
                return;
            }
            var report = new Report();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                FirstInGameSetup.ApplyFromBatch();
                var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if(scene.path!="Assets/Scenes/CharacterTest.unity")scene=EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity");
                if(!CharacterTestPreviewSetup.ApplyBlenderPreview())throw new Exception("CharacterTest setup failed");
                EditorSceneManager.SaveScene(scene);report.sceneSaved=true;
                var roots=new[] { "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab",
                    "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab", SmoothBearAssets.PrefabPath };
                foreach(var path in roots)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(prefab==null)throw new Exception("Missing prefab "+path);
                    var body=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="Body");
                    var hood=prefab.GetComponentsInChildren<Renderer>(true).Single(s=>s.name=="Hood");
                    if(body.sharedMaterials.Length!=1||body.sharedMaterial.name!="MAT_Capsule_Character")throw new Exception(path+": wrong body material");
                    if(body.sharedMesh.blendShapeCount!=2)throw new Exception(path+": old corrective mesh");
                    report.volumeBones=body.bones.Count(b=>b.name.StartsWith("Volume_"));
                    if(report.volumeBones!=8)throw new Exception(path+": missing volume support bones");
                    CheckColor(body.sharedMaterial,"#F3D646"); CheckColor(hood.sharedMaterial,"#D4ECFF");
                    var dependencies=AssetDatabase.GetDependencies(path,true);
                    if(dependencies.Any(p=>p.StartsWith("Assets/Scenes/CharacterTest/")))throw new Exception(path+": legacy dependency");
                    if(!dependencies.Contains(SmoothBearAssets.ModelPath))throw new Exception(path+": wrong model source");
                }
                report.prefabMaterialsValid=report.noLegacyDependencies=true;
                report.clips=SmoothBearAssets.LoadClips().Length;
                if(report.clips!=128)throw new Exception("Expected 128 unique native clips");
                foreach(var clip in SmoothBearAssets.LoadClips())
                {
                    int supportPaths=AnimationUtility.GetCurveBindings(clip)
                        .Where(b=>b.propertyName=="m_LocalRotation.w"&&b.path.Split('/').Last().StartsWith("Volume_"))
                        .Select(b=>b.path).Distinct().Count();
                    if(supportPaths!=8)throw new Exception(clip.name+": incomplete volume animation");
                }
                foreach(var path in new[]{SmoothBearAssets.PreviewControllerPath,SmoothBearAssets.PlayerControllerPath})
                {
                    var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                    var states=controller.layers[0].stateMachine.states;
                    if(path==SmoothBearAssets.PreviewControllerPath)report.previewStates=states.Length;else report.playerStates=states.Length;
                    var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
                    try
                    {
                        var animator=instance.GetComponentInChildren<Animator>();
                        animator.runtimeAnimatorController=controller;
                        var body=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="Body");
                        var baked=new Mesh();
                        foreach(var entry in states)
                        {
                            if(entry.state.motion is not AnimationClip clip)throw new Exception("Missing state "+entry.state.name);
                            if(!AssetDatabase.GetAssetPath(clip).StartsWith(SmoothBearAssets.Root+"/Animations/"))throw new Exception("Old state "+entry.state.name);
                            animator.Rebind();animator.Play(entry.state.name,0,0);animator.Update(0);
                            if(animator.GetCurrentAnimatorStateInfo(0).shortNameHash!=Animator.StringToHash(entry.state.name))throw new Exception("Animator failed "+entry.state.name);
                            animator.Update(clip.length*.5f);body.BakeMesh(baked);
                            if(baked.vertices.Any(v=>float.IsNaN(v.sqrMagnitude)||float.IsInfinity(v.sqrMagnitude)))throw new Exception("Invalid animator mesh "+entry.state.name);
                            report.animatorStatesChecked++;
                        }
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(instance);}
                }
                AssetDatabase.SaveAssets();
            }
            catch(Exception e){report.errors.Add(e.ToString());Debug.LogException(e);}
            Directory.CreateDirectory("artifacts/smooth-bear-unity");
            File.WriteAllText("artifacts/smooth-bear-unity/project-report.json",JsonUtility.ToJson(report,true));
            Debug.Log("SMOOTH_BEAR_PROJECT_DONE errors="+report.errors.Count);
            if(exit)EditorApplication.Exit(report.errors.Count==0?0:1);
        }
        static void CheckColor(Material material,string hex)
        {
            ColorUtility.TryParseHtmlString(hex,out var expected);
            var actual=material.GetColor("_BaseColor");
            if(Mathf.Abs(actual.r-expected.r)+Mathf.Abs(actual.g-expected.g)+Mathf.Abs(actual.b-expected.b)>.001f)
                throw new Exception(material.name+": wrong color");
        }
    }
}
