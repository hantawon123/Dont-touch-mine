using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SmoothBearPackage
{
    const string Root = "Assets/_Game/Content/Characters/SmoothBear";
    [Serializable] public class Motion { public string file, name, guid; public float end; public bool loop; }
    [Serializable] public class Manifest { public Motion[] motions; }
    [Serializable] public class ClipCheck { public string name; public int frames; public float loopError, torsoDrift; }
    [Serializable] public class Report
    {
        public int clips, states, vertices, coreVertices, sampledFrames, volumeBones;
        public List<ClipCheck> checkedClips = new();
        public List<string> errors = new();
    }
    public static void Diagnose()
    {
        var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/SmoothBear.fbx"));
        instance.GetComponent<Animator>().enabled=false;
        var body=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="Body");
        foreach(var name in new[]{"Crawl_Forward","Idle"})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+name+".anim");
            var transforms=instance.GetComponentsInChildren<Transform>();
            clip.SampleAnimation(instance,0);
            var p=transforms.Select(t=>t.localPosition).ToArray();var q=transforms.Select(t=>t.localRotation).ToArray();
            var s=transforms.Select(t=>t.localScale).ToArray();
            Debug.Log("DIAG "+name+" length "+clip.length+" shapes "+body.GetBlendShapeWeight(0)+","+body.GetBlendShapeWeight(1));
            clip.SampleAnimation(instance,clip.length);
            Debug.Log("DIAG end shapes "+body.GetBlendShapeWeight(0)+","+body.GetBlendShapeWeight(1));
            for(int i=0;i<transforms.Length;i++)
                if(Vector3.Distance(p[i],transforms[i].localPosition)>.00001||Quaternion.Angle(q[i],transforms[i].localRotation)>.01||Vector3.Distance(s[i],transforms[i].localScale)>.00001)
                    Debug.Log("DIAG bone "+transforms[i].name+" position "+Vector3.Distance(p[i],transforms[i].localPosition)+" rotation "+Quaternion.Angle(q[i],transforms[i].localRotation)+" scale "+Vector3.Distance(s[i],transforms[i].localScale));
            foreach(var b in AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.StartsWith("blendShape")))
            {var curve=AnimationUtility.GetEditorCurve(clip,b);Debug.Log("DIAG curve "+b.path+" "+b.propertyName+" "+curve.Evaluate(0)+" -> "+curve.Evaluate(clip.length));}
        }
        EditorApplication.Exit(0);
    }
    public static void DiagnoseVolume()
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/SmoothBear.fbx");
        var instance=UnityEngine.Object.Instantiate(model);instance.GetComponent<Animator>().enabled=false;
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Carry_Crawl_Back.anim");
        foreach(int frame in new[]{0,5,10,15,20,30})
        {
            clip.SampleAnimation(instance,frame/clip.frameRate);
            var child=instance.GetComponentsInChildren<Transform>().Single(t=>t.name=="Leg.R");
            var helper=child.parent.Find("Volume_Leg.R");
            var rest=model.transform.Find(AnimationUtility.CalculateTransformPath(helper,instance.transform)).localRotation;
            var expected=rest*Quaternion.Slerp(Quaternion.identity,Quaternion.Inverse(rest)*child.localRotation,.5f);
            Debug.Log("VOLUME_DIAG frame="+frame+" error="+Quaternion.Angle(expected,helper.localRotation)+" expected="+expected.ToString("F7")+" actual="+helper.localRotation.ToString("F7")+" rest="+rest.ToString("F7")+" child="+child.localRotation.ToString("F7"));
        }
        EditorApplication.Exit(0);
    }
    public static void Run()
    {
        var report = new Report();
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("motion-manifest.json"));
            var modelPath = Root + "/SmoothBear.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation = false;
            importer.importBlendShapes = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            var animator = instance.GetComponent<Animator>(); animator.enabled = false;
            var body = instance.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "Body");
            var bodyPath = AnimationUtility.CalculateTransformPath(body.transform, instance.transform);
            if (body.sharedMesh.subMeshCount != 1) throw new Exception("Body must have exactly one material slot");
            report.vertices = body.sharedMesh.vertexCount;
            var mapping = new Dictionary<string,string>();
            foreach (var entry in manifest.motions)
            {
                var source = AssetDatabase.LoadAllAssetsAtPath("Assets/Source/" + entry.file).OfType<AnimationClip>()
                    .Single(c => c.name == entry.name);
                // Build an editable native clip from curves, without retaining
                // the FBX importer's duplicate internal animation payload.
                var clip = new AnimationClip { name=entry.name, frameRate=source.frameRate, legacy=false };
                AnimationUtility.SetAnimationClipSettings(clip,AnimationUtility.GetAnimationClipSettings(source));
                var sourceBindings=AnimationUtility.GetCurveBindings(source).Where(b=>
                    b.type!=typeof(Transform)||instance.transform.Find(b.path)?.GetComponent<Renderer>()==null).ToArray();
                AnimationUtility.SetEditorCurves(clip,sourceBindings,sourceBindings.Select(b=>CompactCurve(
                    AnimationUtility.GetEditorCurve(source,b),source.length,source.frameRate,b.propertyName.StartsWith("blendShape.")?.001f:.00001f)).ToArray());
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.propertyName.StartsWith("blendShape.Crawl_")) AnimationUtility.SetEditorCurve(clip,binding,null);
                foreach (var shape in new[] { "Belly_Breath", "Crouch_Groin_Flat" })
                {
                    var binding = EditorCurveBinding.FloatCurve(bodyPath,typeof(SkinnedMeshRenderer),"blendShape."+shape);
                    if (AnimationUtility.GetEditorCurve(clip,binding)==null)
                        AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,clip.length,0));
                }
                if(entry.loop) CloseLoop(clip);
                var dest = Root + "/Animations/" + entry.name + ".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);
                if(existing!=null){EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(clip);}
                else AssetDatabase.CreateAsset(clip,dest);
                mapping[entry.guid] = AssetDatabase.AssetPathToGUID(dest);
            }
            // Sample imported native curves, after Unity has rebuilt its playback
            // data. Sampling a newly edited in-memory clip can use stale curves.
            AssetDatabase.SaveAssets();
            foreach(var entry in manifest.motions)
            {
                var path=Root+"/Animations/"+entry.name+".anim";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                BakeVolumeBones(saved,instance,model);EditorUtility.SetDirty(saved);
            }
            AssetDatabase.SaveAssets();
            foreach (var name in new[] { "SmoothBearPreview.controller", "PlayerAnimator.controller" })
            {
                var yaml = File.ReadAllText(name+".source");
                yaml = Regex.Replace(yaml,@"m_Motion: \{fileID: [-\d]+, guid: ([a-f0-9]+), type: \d+\}", m =>
                    mapping.TryGetValue(m.Groups[1].Value,out var guid) ? "m_Motion: {fileID: 7400000, guid: "+guid+", type: 2}" : m.Value);
                var path = name.StartsWith("Player") ? "Assets/_Game/Content/Animations/"+name : Root+"/"+name;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path,yaml);
                File.Copy(name+".meta.source",path+".meta",true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            var spine = body.bones.Single(b => b.name == "Spine");
            int spineIndex = Array.IndexOf(body.bones,spine);
            var weights = body.sharedMesh.boneWeights;
            var core = Enumerable.Range(0,weights.Length).Where(i=>weights[i].boneIndex0==spineIndex&&weights[i].weight0>.99999f).ToArray();
            report.coreVertices=core.Length;
            if(core.Length<1000) throw new Exception("Missing stable torso core");
            var baked = new Mesh(); var vertices = new List<Vector3>();
            var volumeBones=instance.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Volume_")).ToArray();
            report.volumeBones=volumeBones.Length;
            foreach(var entry in manifest.motions)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+entry.name+".anim");
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var target=string.IsNullOrEmpty(binding.path)?instance.transform:instance.transform.Find(binding.path);
                    if(target==null) report.errors.Add(entry.name+": missing binding "+binding.path);
                    if(binding.propertyName.StartsWith("blendShape.") && body.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11))<0)
                        report.errors.Add(entry.name+": missing shape "+binding.propertyName);
                }
                var result=new ClipCheck{name=entry.name};
                Vector3[] first=null, firstCore=null;
                int count=Mathf.CeilToInt(clip.length*clip.frameRate);
                for(int frame=0;frame<=count;frame++)
                {
                    clip.SampleAnimation(instance,Mathf.Min(frame/clip.frameRate,clip.length));
                    foreach(var helper in volumeBones)
                    {
                        var child=helper.parent.Find(helper.name.Substring(7));
                        var path=AnimationUtility.CalculateTransformPath(helper,instance.transform);
                        var rest=model.transform.Find(path).localRotation;
                        var expected=rest*Quaternion.Slerp(Quaternion.identity,Quaternion.Inverse(rest)*child.localRotation,.5f);
                        if(Quaternion.Angle(expected,helper.localRotation)>.2f)
                            throw new Exception(entry.name+": volume bone rotation mismatch "+helper.name+" frame "+frame);
                    }
                    body.BakeMesh(baked); baked.GetVertices(vertices);
                    if(vertices.Any(p=>float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude)||p.sqrMagnitude>100))
                        throw new Exception(entry.name+": invalid mesh at "+frame);
                    var matrix=spine.worldToLocalMatrix*body.transform.localToWorldMatrix;
                    if(frame==0){first=vertices.ToArray();firstCore=core.Select(i=>matrix.MultiplyPoint3x4(vertices[i])).ToArray();}
                    if(entry.name.Contains("Crawl"))
                        for(int i=0;i<core.Length;i++) result.torsoDrift=Mathf.Max(result.torsoDrift,Vector3.Distance(firstCore[i],matrix.MultiplyPoint3x4(vertices[core[i]])));
                    if(frame==count&&clip.isLooping)
                        for(int i=0;i<vertices.Count;i++)result.loopError=Mathf.Max(result.loopError,Vector3.Distance(first[i],vertices[i]));
                    result.frames++;report.sampledFrames++;
                }
                if(result.loopError>.002f)report.errors.Add(entry.name+": loop error "+result.loopError);
                if(result.torsoDrift>.0002f)report.errors.Add(entry.name+": torso drift "+result.torsoDrift);
                report.checkedClips.Add(result);
                Debug.Log("SMOOTH_BEAR_CLIP "+entry.name);
            }
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/SmoothBearPreview.controller");
            report.states=controller.layers[0].stateMachine.states.Length;
            foreach(var state in controller.layers[0].stateMachine.states)
                if(!(state.state.motion is AnimationClip))report.errors.Add("Missing state clip "+state.state.name);
            report.clips=manifest.motions.Length;
            UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(instance);
        }
        catch(Exception e){report.errors.Add(e.ToString());Debug.LogException(e);}
        File.WriteAllText("package-report.json",JsonUtility.ToJson(report,true));
        Debug.Log("SMOOTH_BEAR_DONE clips="+report.clips+" frames="+report.sampledFrames+" errors="+report.errors.Count);
        EditorApplication.Exit(report.errors.Count==0?0:1);
    }
    static void CloseLoop(AnimationClip clip)
    {
        float length=clip.length;
        float width=Mathf.Min(length*.2f,8f/clip.frameRate);
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve=AnimationUtility.GetEditorCurve(clip,binding);
            float delta=curve.Evaluate(0)-curve.Evaluate(length);
            if(Mathf.Abs(delta)<.000001f)continue;
            var keys=new List<Keyframe>();
            int count=Mathf.CeilToInt(length*clip.frameRate);
            for(int i=0;i<=count;i++)
            {
                float time=Mathf.Min(i/clip.frameRate,length);
                float t=Mathf.Clamp01((time-(length-width))/width);
                keys.Add(new Keyframe(time,curve.Evaluate(time)+delta*t*t*(3-2*t)));
            }
            var output=new AnimationCurve(keys.ToArray());
            for(int i=0;i<output.length;i++)
            { AnimationUtility.SetKeyLeftTangentMode(output,i,AnimationUtility.TangentMode.Linear);
              AnimationUtility.SetKeyRightTangentMode(output,i,AnimationUtility.TangentMode.Linear); }
            AnimationUtility.SetEditorCurve(clip,binding,output);
        }
        clip.EnsureQuaternionContinuity();
    }
    static void BakeVolumeBones(AnimationClip clip,GameObject instance,GameObject model)
    {
        var supports=instance.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Volume_")).ToArray();
        if(supports.Length==0)return;
        var channels=new[]{"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w",
            "m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
        int count=Mathf.CeilToInt(clip.length*clip.frameRate);
        var keys=supports.Select(_=>channels.Select(__=>new List<Keyframe>()).ToArray()).ToArray();
        var previous=new Quaternion[supports.Length];
        for(int frame=0;frame<=count;frame++)
        {
            float time=Mathf.Min(frame/clip.frameRate,clip.length);clip.SampleAnimation(instance,time);
            for(int j=0;j<supports.Length;j++)
            {
                var helper=supports[j];var child=helper.parent.Find(helper.name.Substring(7));
                if(child==null)throw new Exception("Missing volume joint "+helper.name);
                var path=AnimationUtility.CalculateTransformPath(helper,instance.transform);
                var rest=model.transform.Find(path).localRotation;
                var delta=Quaternion.Inverse(rest)*child.localRotation;
                var q=rest*Quaternion.Slerp(Quaternion.identity,delta,.5f);
                if(frame>0&&Quaternion.Dot(previous[j],q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                previous[j]=q;var p=child.localPosition;var s=child.localScale;
                float[] values={q.x,q.y,q.z,q.w,p.x,p.y,p.z,s.x,s.y,s.z};
                for(int k=0;k<channels.Length;k++)keys[j][k].Add(new Keyframe(time,values[k]));
            }
        }
        for(int j=0;j<supports.Length;j++)for(int k=0;k<channels.Length;k++)
        {
            var curve=new AnimationCurve(keys[j][k].ToArray());
            for(int i=0;i<curve.length;i++)
            {AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);
             AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(
                AnimationUtility.CalculateTransformPath(supports[j],instance.transform),typeof(Transform),channels[k]),
                CompactCurve(curve,clip.length,clip.frameRate,.00001f));
        }
        clip.EnsureQuaternionContinuity();
    }
    static AnimationCurve CompactCurve(AnimationCurve source,float length,float frameRate,float tolerance)
    {
        int count=Mathf.CeilToInt(length*frameRate);
        var keys=Enumerable.Range(0,count+1).Select(i=>{float t=Mathf.Min(i/frameRate,length);return new Keyframe(t,source.Evaluate(t));}).ToArray();
        var keep=new SortedSet<int>{0,count};
        void Split(int a,int b)
        {
            float error=tolerance;int at=-1;
            for(int i=a+1;i<b;i++)
            {
                float t=(keys[i].time-keys[a].time)/(keys[b].time-keys[a].time);
                float distance=Mathf.Abs(keys[i].value-Mathf.Lerp(keys[a].value,keys[b].value,t));
                if(distance>error){error=distance;at=i;}
            }
            if(at<0)return;keep.Add(at);Split(a,at);Split(at,b);
        }
        Split(0,count);
        var result=new AnimationCurve(keep.Select(i=>keys[i]).ToArray());
        for(int i=0;i<result.length;i++)
        {AnimationUtility.SetKeyLeftTangentMode(result,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(result,i,AnimationUtility.TangentMode.Linear);}
        return result;
    }
}
