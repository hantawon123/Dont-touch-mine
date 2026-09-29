// Copied into an isolated Unity QA project by validate_crawl_torso.py.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CrawlTorsoUnityValidation
{
    [Serializable] public class ClipResult
    {
        public string clip;
        public int frames;
        public float torsoDrift;
        public float oldTorsoDrift;
        public float legTravel;
    }
    [Serializable] public class Report
    {
        public int coreVertices;
        public List<ClipResult> clips = new List<ClipResult>();
        public List<string> errors = new List<string>();
    }

    public static void Run()
    {
        var report = new Report();
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var target = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/New/FirstPlayerCapsule_Idle.fbx"));
            var old = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Old/FirstPlayerCapsule_Idle.fbx"));
            foreach (var root in new[] { target, old })
                foreach (var animator in root.GetComponentsInChildren<Animator>()) animator.enabled = false;
            var body = target.GetComponentsInChildren<SkinnedMeshRenderer>().Single(x => x.name == "Body");
            var spine = body.bones.Single(x => x.name == "Spine");
            int spineIndex = Array.IndexOf(body.bones, spine);
            var weights = body.sharedMesh.boneWeights;
            var core = Enumerable.Range(0, weights.Length).Where(i => weights[i].boneIndex0 == spineIndex && weights[i].weight0 > .99999f).ToArray();
            report.coreVertices = core.Length;
            if (core.Length < 100) throw new Exception("Missing rigid torso core");
            foreach (var file in Directory.GetFiles("Assets/New", "*Crawl*.fbx"))
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(file.Replace('\\', '/')).OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview__"));
                var result = Sample(target, clip, core);
                result.clip = Path.GetFileNameWithoutExtension(file);
                result.oldTorsoDrift = Sample(old, clip, core).torsoDrift;
                report.clips.Add(result);
                if (result.torsoDrift > .0001f) report.errors.Add(result.clip + ": torso drift " + result.torsoDrift);
                if (result.legTravel < .001f) report.errors.Add(result.clip + ": leg animation missing");
                Debug.Log("CRAWL_QA " + JsonUtility.ToJson(result));
            }
            if (report.clips.Count < 12) throw new Exception("Missing crawl directions");
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(old);
        }
        catch (Exception error) { report.errors.Add(error.ToString()); }
        File.WriteAllText("unity-report.json", JsonUtility.ToJson(report, true));
        Debug.Log("CRAWL_QA_FINISHED " + report.clips.Count + " errors=" + report.errors.Count);
        EditorApplication.Exit(report.errors.Count == 0 ? 0 : 1);
    }

    private static ClipResult Sample(GameObject target, AnimationClip clip, int[] core)
    {
        var body = target.GetComponentsInChildren<SkinnedMeshRenderer>().Single(x => x.name == "Body");
        var spine = body.bones.Single(x => x.name == "Spine");
        var foot = body.bones.Single(x => x.name == "Foot.L");
        var baked = new Mesh();
        var result = new ClipResult();
        Vector3[] first = null;
        Vector3 firstFoot = default;
        int count = Mathf.CeilToInt(clip.length * clip.frameRate);
        for (int frame = 0; frame <= count; frame++)
        {
            for (int i = 0; i < body.sharedMesh.blendShapeCount; i++) body.SetBlendShapeWeight(i, 0);
            clip.SampleAnimation(target, Mathf.Min(frame / clip.frameRate, clip.length));
            body.BakeMesh(baked);
            var vertices = baked.vertices;
            var intoSpine = spine.worldToLocalMatrix * body.transform.localToWorldMatrix;
            var positions = core.Select(i => intoSpine.MultiplyPoint3x4(vertices[i])).ToArray();
            var footPosition = spine.InverseTransformPoint(foot.position);
            if (first == null) { first = positions; firstFoot = footPosition; }
            for (int i = 0; i < positions.Length; i++)
                result.torsoDrift = Mathf.Max(result.torsoDrift, Vector3.Distance(first[i], positions[i]));
            result.legTravel = Mathf.Max(result.legTravel, Vector3.Distance(firstFoot, footPosition));
            result.frames++;
        }
        UnityEngine.Object.DestroyImmediate(baked);
        return result;
    }
}
