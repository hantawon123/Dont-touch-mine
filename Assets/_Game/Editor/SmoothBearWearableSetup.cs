using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Client.Character;
using Game.Core.Players;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class SmoothBearWearableSetup
    {
        const string Root = SmoothBearAssets.Root + "/Wearables";
        const string Output = "artifacts/smooth-bear-unity/wearables";
        const string Request = Output + "/setup.request";
        [Serializable] class Manifest { public Part[] parts; }
        [Serializable] class Part { public string name,bone; public Mat[] materials; }
        [Serializable] class Mat { public string name,texture; public float[] color; public float roughness; public int tint; }
        static SmoothBearWearableSetup() { EditorApplication.update += Check; }
        static void Check()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying=false; return; }
            var command=File.ReadAllText(Request).Trim();File.Delete(Request);
            if(command=="palette")RefreshPalette();else if(command=="brown-eyes")AddBrownEyes();else Run();
        }
        [MenuItem("Game/Setup/Pastel customization palette")]
        public static void RefreshPalette()
        {
            Directory.CreateDirectory(Output);
            try
            {
                SmoothBearPastelPalette.Apply();
                var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
                ValidateAndRender(catalog);AssetDatabase.SaveAssets();
                File.WriteAllText(Output+"/palette-result.json","{\"success\":true,\"bodyColors\":"+catalog.Find(AvatarPartCategory.BodyColor).Parts.Count+",\"hoodColors\":"+catalog.Find(AvatarPartCategory.HoodColor).Parts.Count+",\"shoeColors\":"+catalog.Find(AvatarPartCategory.Shoes).Parts.Count+"}");
            }
            catch(Exception e){File.WriteAllText(Output+"/palette-result.json",JsonUtility.ToJson(new Failure{error=e.ToString()},true));Debug.LogException(e);}
        }
        [MenuItem("Game/Setup/Approved shoes and expressions")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            try
            {
                var keys=new[]{"CompactShoes","Sparkle","Sleepy","Fierce","Brown"};
                var wearables=keys.Select(Build).ToArray();
                SmoothBearPastelPalette.Apply();
                var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
                var so=new SerializedObject(catalog);var groups=so.FindProperty("groups");
                for(int g=0;g<groups.arraySize;g++)
                {
                    var group=groups.GetArrayElementAtIndex(g);var parts=group.FindPropertyRelative("parts");
                    var category=(AvatarPartCategory)group.FindPropertyRelative("category").intValue;
                    if(category==AvatarPartCategory.Shoes)
                    {
                        for(int i=0;i<parts.arraySize;i++)
                        {
                            var part=parts.GetArrayElementAtIndex(i);
                            part.FindPropertyRelative("wearable").objectReferenceValue=wearables[0];
                        }
                    }
                    if(category==AvatarPartCategory.Face)
                    {
                        var ids=new[]{"default","sparkle","sleepy","fierce","brown"};var labels=new[]{"기본","반짝반짝","나른한 눈","사나운 눈","갈색 눈망울"};
                        parts.arraySize=ids.Length;
                        for(int i=0;i<ids.Length;i++)
                        {
                            var part=parts.GetArrayElementAtIndex(i);
                            part.FindPropertyRelative("id").stringValue="face_"+ids[i];part.FindPropertyRelative("label").stringValue=labels[i];
                            part.FindPropertyRelative("swatch").colorValue=Color.white;
                            foreach(var field in new[]{"material","texture","mesh","thumbnail"})part.FindPropertyRelative(field).objectReferenceValue=null;
                            part.FindPropertyRelative("wearable").objectReferenceValue=i==0?null:wearables[i];
                        }
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                SmoothBearAssets.SaveCharacterPrefab();FirstInGameSetup.ApplyFromBatch();
                var preview=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
                try {preview.name="AvatarPreview";PrefabUtility.SaveAsPrefabAsset(preview,"Assets/_Game/Content/Resources/AvatarPreview.prefab");}
                finally {UnityEngine.Object.DestroyImmediate(preview);}
                ValidateAndRender(catalog);
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output+"/result.json","{\"success\":true,\"expressions\":4,\"shoeColors\":"+catalog.Find(AvatarPartCategory.Shoes).Parts.Count+"}");
            }
            catch(Exception e) {File.WriteAllText(Output+"/result.json",JsonUtility.ToJson(new Failure{error=e.ToString()},true));Debug.LogException(e);}
        }
        [MenuItem("Game/Setup/Brown glossy eyes")]
        public static void AddBrownEyes()
        {
            Directory.CreateDirectory(Output);
            try
            {
                var wearable=Build("Brown");
                var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
                var so=new SerializedObject(catalog);var groups=so.FindProperty("groups");bool added=false;
                for(int g=0;g<groups.arraySize;g++)
                {
                    var group=groups.GetArrayElementAtIndex(g);
                    if((AvatarPartCategory)group.FindPropertyRelative("category").intValue!=AvatarPartCategory.Face)continue;
                    var parts=group.FindPropertyRelative("parts");int index=-1;
                    for(int i=0;i<parts.arraySize;i++)if(parts.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue=="face_brown")index=i;
                    if(index<0){index=parts.arraySize;parts.arraySize++;}
                    var part=parts.GetArrayElementAtIndex(index);
                    part.FindPropertyRelative("id").stringValue="face_brown";
                    part.FindPropertyRelative("label").stringValue="갈색 눈망울";
                    part.FindPropertyRelative("swatch").colorValue=Color.white;
                    foreach(var field in new[]{"material","texture","mesh","thumbnail"})part.FindPropertyRelative(field).objectReferenceValue=null;
                    part.FindPropertyRelative("wearable").objectReferenceValue=wearable;added=true;
                }
                if(!added)throw new Exception("Face category missing");
                so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);
                ValidateAndRender(catalog);AssetDatabase.SaveAssets();
                File.WriteAllText(Output+"/brown-eyes-result.json","{\"success\":true,\"faceId\":\"face_brown\",\"totalFaces\":"+catalog.Find(AvatarPartCategory.Face).Parts.Count+"}");
            }
            catch(Exception e){File.WriteAllText(Output+"/brown-eyes-result.json",JsonUtility.ToJson(new Failure{error=e.ToString()},true));Debug.LogException(e);}
        }
        [Serializable] class Failure { public string error; }
        static T Save<T>(T obj,string path) where T:UnityEngine.Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing==null){AssetDatabase.CreateAsset(obj,path);return obj;}
            EditorUtility.CopySerialized(obj,existing);UnityEngine.Object.DestroyImmediate(obj);EditorUtility.SetDirty(existing);return existing;
        }
        static AvatarWearable Build(string key)
        {
            string path=Root+"/"+key+".fbx";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.isReadable=true;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var result=ScriptableObject.CreateInstance<AvatarWearable>();result.name=key;
            try
            {
                var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/"+key+".json"));
                var attachments=new List<AvatarWearable.Attachment>();
                foreach(var part in manifest.parts)
                {
                    var filter=source.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name==part.name);
                    var bone=source.GetComponentsInChildren<Transform>(true).Single(t=>t.name==part.bone);
                    var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=key+"_"+part.name;
                    var matrix=bone.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    mesh.vertices=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                    mesh.normals=mesh.normals.Select(n=>matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                    if(mesh.tangents.Length>0)mesh.tangents=mesh.tangents.Select(t=>{var v=matrix.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;return new Vector4(v.x,v.y,v.z,t.w);}).ToArray();
                    mesh.RecalculateBounds();mesh=Save(mesh,Root+"/"+mesh.name+".asset");
                    var materials=new List<Material>();
                    for(int i=0;i<part.materials.Length;i++)
                    {
                        var m=part.materials[i];var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.name=key+"_"+part.name+"_"+i;
                        material.SetColor("_BaseColor",m.tint!=0||!string.IsNullOrEmpty(m.texture)?Color.white:new Color(m.color[0],m.color[1],m.color[2],m.color[3]).gamma);
                        material.SetFloat("_Smoothness",1-m.roughness);
                        if(key=="CompactShoes")SmoothBearKnitSetup.ApplyToMaterial(material,bone.localToWorldMatrix);
                        if(!string.IsNullOrEmpty(m.texture))
                        {
                            string tp=Root+"/"+m.texture;AssetDatabase.ImportAsset(tp,ImportAssetOptions.ForceSynchronousImport);
                            var ti=(TextureImporter)AssetImporter.GetAtPath(tp);ti.sRGBTexture=true;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
                            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
                        }
                        materials.Add(Save(material,Root+"/"+material.name+".mat"));
                    }
                    attachments.Add(new AvatarWearable.Attachment{name=part.name,bone=part.bone,mesh=mesh,materials=materials.ToArray(),tintModes=part.materials.Select(m=>m.tint).ToArray()});
                }
                result.attachments=attachments.ToArray();return Save(result,Root+"/"+key+".asset");
            }
            finally {UnityEngine.Object.DestroyImmediate(source);}
        }

        static void ValidateAndRender(AvatarPartCatalog catalog)
        {
            Directory.CreateDirectory(Root+"/Icons");
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
            var cameraObject=new GameObject("WearableCamera");var lightObject=new GameObject("WearableLight");
            var fillObject=new GameObject("WearableFillLight");
            var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            var existingLights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();
            int samples=0;
            try
            {
                instance.GetComponent<Animator>().enabled=false;
                foreach(var existingLight in existingLights)existingLight.enabled=false;
                foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                var applier=instance.GetComponent<AvatarAppearanceApplier>();
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;camera.allowHDR=false;
                var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.intensity=8;light.range=10;light.cullingMask=1<<30;
                fillObject.transform.SetParent(cameraObject.transform,false);fillObject.transform.localPosition=new Vector3(2,-.5f,1);
                var fill=fillObject.AddComponent<Light>();fill.type=LightType.Point;fill.intensity=4;fill.range=10;fill.cullingMask=1<<30;
                foreach(var face in catalog.Find(AvatarPartCategory.Face).Parts)
                {
                    SmoothBearAssets.LoadClip("Idle").SampleAnimation(instance,0);
                    applier.Apply(catalog.Default.With(AvatarPartCategory.Face,face.Id));
                    var originals=instance.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Eye_")).ToArray();
                    if(originals.Any(r=>r.enabled!=(face.Wearable==null)))throw new Exception("Default eye visibility: "+face.Id);
                    var eye=instance.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Eye_White_L");
                    var center=eye.bounds.center;center.x=0;
                    instance.transform.position=new Vector3(1000,0,0);center+=instance.transform.position;
                    camera.transform.position=center+new Vector3(0,0,4);camera.transform.LookAt(center);camera.orthographicSize=.3f;
                    light.transform.position=center+new Vector3(-1,2,3);
                    Render(camera,rt,Root+"/Icons/"+face.Id+".png");
                    camera.transform.position=instance.transform.position+new Vector3(2,1.2f,5);camera.transform.LookAt(instance.transform.position+Vector3.up*.95f);camera.orthographicSize=1.15f;
                    Render(camera,rt,Output+"/"+face.Id+"-full.png");
                    instance.transform.position=Vector3.zero;
                    foreach(var clip in SmoothBearAssets.LoadClips())
                    {
                        clip.SampleAnimation(instance,clip.length*.5f);
                        foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Wearable_")&&r.gameObject.activeSelf))
                        {
                            if(renderer.transform.localPosition.sqrMagnitude>1e-10f||Quaternion.Angle(renderer.transform.localRotation,Quaternion.identity)>.01f)throw new Exception("Attachment moved: "+clip.name);
                            if(renderer.sharedMaterials.Any(m=>m==null))throw new Exception("Missing wearable material");
                        }
                        samples++;
                    }
                }
                SmoothBearAssets.LoadClip("Idle").SampleAnimation(instance,0);
                foreach(var shoe in catalog.Find(AvatarPartCategory.Shoes).Parts)
                {
                    applier.Apply(catalog.Default.With(AvatarPartCategory.Shoes,shoe.Id));
                    var visible=instance.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
                    foreach(var r in visible)r.enabled=r.name.StartsWith("Wearable_CompactShoes");
                    instance.transform.position=new Vector3(1000,0,0);
                    var shoes=visible.Where(r=>r.enabled).ToArray();var bounds=shoes[0].bounds;foreach(var r in shoes)bounds.Encapsulate(r.bounds);
                    camera.transform.position=bounds.center+new Vector3(2,1.5f,4);camera.transform.LookAt(bounds.center);camera.orthographicSize=bounds.extents.magnitude*1.15f;
                    light.transform.position=bounds.center+new Vector3(-1,2,3);
                    Render(camera,rt,Root+"/Icons/"+shoe.Id+".png");
                    foreach(var r in visible)r.enabled=true;
                    instance.transform.position=Vector3.zero;
                }
                foreach(var shoe in catalog.Find(AvatarPartCategory.Shoes).Parts)
                foreach(var body in catalog.Find(AvatarPartCategory.BodyColor).Parts)
                {
                    var value=catalog.Default.With(AvatarPartCategory.Shoes,shoe.Id).With(AvatarPartCategory.BodyColor,body.Id).With(AvatarPartCategory.Face,"face_sleepy");applier.Apply(value);
                    foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Wearable_")&&r.gameObject.activeSelf))
                    {
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,0);
                        if(renderer.name.Contains("Lid")&&block.GetColor("_BaseColor")!=body.Swatch)throw new Exception("Lid tint failed");
                        if(renderer.name.Contains("Shoes")&&block.GetColor("_BaseColor")!=shoe.Swatch)throw new Exception("Shoe tint failed");
                    }
                    if(catalog.Normalise(new AvatarAppearance(value.BodyColorId,value.HoodId,value.ShoesId,value.FaceId))!=value)throw new Exception("Selection roundtrip failed");
                }
                var count=instance.GetComponentsInChildren<MeshRenderer>(true).Length;
                for(int i=0;i<10;i++)foreach(var face in catalog.Find(AvatarPartCategory.Face).Parts)applier.Apply(catalog.Default.With(AvatarPartCategory.Face,face.Id));
                if(instance.GetComponentsInChildren<MeshRenderer>(true).Length!=count)throw new Exception("Wearables duplicated on switch");
                File.WriteAllText(Output+"/validation.txt","PASS: "+samples+" animation samples; all shoe/body colours; default eye restoration; selection roundtrip; repeat switching.");
            }
            finally {foreach(var light in existingLights)if(light!=null)light.enabled=true;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lightObject);UnityEngine.Object.DestroyImmediate(instance);}
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var so=new SerializedObject(catalog);var groups=so.FindProperty("groups");
            for(int g=0;g<groups.arraySize;g++)
            {
                var group=groups.GetArrayElementAtIndex(g);var category=(AvatarPartCategory)group.FindPropertyRelative("category").intValue;if(category!=AvatarPartCategory.Face&&category!=AvatarPartCategory.Shoes)continue;
                var parts=group.FindPropertyRelative("parts");
                for(int i=0;i<parts.arraySize;i++)
                {
                    var part=parts.GetArrayElementAtIndex(i);var path=Root+"/Icons/"+part.FindPropertyRelative("id").stringValue+".png";
                    var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;ti.mipmapEnabled=false;ti.alphaIsTransparency=true;ti.SaveAndReimport();
                    part.FindPropertyRelative("thumbnail").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Render(Camera camera,RenderTexture rt,string path)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
