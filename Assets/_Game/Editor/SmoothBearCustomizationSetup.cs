using System;
using System.IO;
using System.Linq;
using Game.Client.Character;
using Game.Core.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    public static class SmoothBearCustomizationSetup
    {
        const string Hoods = SmoothBearAssets.Root + "/Hoods";
        const string CatalogPath = "Assets/_Game/Content/Config/AvatarPartCatalog.asset";
        const string Output = "artifacts/smooth-bear-unity/customization";
        [Serializable] public class Swatch { public string id,label,hex; }
        [Serializable] public class Palette { public Swatch[] body,hood; }
        [Serializable] class Report { public int shapes,bodyColors,hoodColors,combinations,animationSamples; public string[] errors; }

        [MenuItem("Game/Setup/Animal hood customization")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Application.isPlaying)
            {
                File.WriteAllText("artifacts/smooth-bear-unity/run-project-validation.request","customize");
                return;
            }
            Directory.CreateDirectory(Output);
            var report=new Report();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(Hoods+"/AnimalHoods.fbx");
                importer.importAnimation=false;importer.isReadable=true;
                importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                var variants=AssetDatabase.LoadAssetAtPath<GameObject>(Hoods+"/AnimalHoods.fbx");
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.ModelPath);
                var original=model.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Hood");
                var meshes=new Mesh[4];meshes[0]=original.sharedMesh;
                var names=new[]{"Bear","Cat","Dog","Rabbit"};
                for(int i=1;i<4;i++)
                {
                    var part=variants.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Hood_"+names[i]);
                    if(Vector3.Distance(part.transform.localPosition,original.transform.localPosition)>.0001f ||
                        Quaternion.Angle(part.transform.localRotation,original.transform.localRotation)>.05f ||
                        Vector3.Distance(part.transform.localScale,original.transform.localScale)>.0001f)
                        throw new Exception(names[i]+": hood transform differs from the character");
                    meshes[i]=part.sharedMesh;
                }
                var palette=JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/_Game/Content/Config/SmoothBearPalette.json"));
                var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>(CatalogPath);
                Directory.CreateDirectory(Output);
                if(!File.Exists(Output+"/before-catalog.asset"))File.Copy(CatalogPath,Output+"/before-catalog.asset");
                var so=new SerializedObject(catalog);var groups=so.FindProperty("groups");
                // Keep the existing shoes and face rows, preserving their ids.
                // Restore category order first so rerunning setup preserves face/shoes.
                for(int category=0;category<4;category++)
                    for(int index=category;index<groups.arraySize;index++)
                        if(groups.GetArrayElementAtIndex(index).FindPropertyRelative("category").intValue==category)
                        {groups.MoveArrayElement(index,category);break;}
                groups.arraySize=5;
                var bodyIcon=groups.GetArrayElementAtIndex(0).FindPropertyRelative("icon").objectReferenceValue;
                var hoodIcon=groups.GetArrayElementAtIndex(1).FindPropertyRelative("icon").objectReferenceValue;
                SetColors(groups.GetArrayElementAtIndex(0),AvatarPartCategory.BodyColor,"몸 색상",bodyIcon,palette.body);
                var hood=groups.GetArrayElementAtIndex(1);hood.FindPropertyRelative("category").intValue=(int)AvatarPartCategory.Hood;
                hood.FindPropertyRelative("label").stringValue="후드 모양";
                var parts=hood.FindPropertyRelative("parts");parts.arraySize=4;
                var labels=new[]{"곰","고양이","강아지","토끼"};
                for(int i=0;i<4;i++)SetPart(parts.GetArrayElementAtIndex(i),"hood_"+names[i].ToLowerInvariant(),labels[i],"#D4ECFF",meshes[i]);
                SetColors(groups.GetArrayElementAtIndex(4),AvatarPartCategory.HoodColor,"후드 색상",hoodIcon,palette.hood);
                groups.MoveArrayElement(4,2);
                so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);
                SmoothBearAssets.SaveCharacterPrefab();FirstInGameSetup.ApplyFromBatch();
                var preview=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
                try {Directory.CreateDirectory("Assets/_Game/Content/Resources");preview.name="AvatarPreview";PrefabUtility.SaveAsPrefabAsset(preview,"Assets/_Game/Content/Resources/AvatarPreview.prefab");}
                finally {UnityEngine.Object.DestroyImmediate(preview);}
                AssetDatabase.SaveAssets();
                Validate(catalog,palette,meshes,report);
                RenderHoods(catalog,names);
                // Assign actual hood thumbnails after importing the renders.
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                for(int i=0;i<4;i++)
                {
                    string path=Hoods+"/Icons/"+names[i]+".png";
                    var texture=(TextureImporter)AssetImporter.GetAtPath(path);
                    texture.textureType=TextureImporterType.Sprite;texture.spriteImportMode=SpriteImportMode.Single;texture.alphaIsTransparency=true;
                    texture.mipmapEnabled=false;texture.SaveAndReimport();
                }
                so.Update();parts=so.FindProperty("groups").GetArrayElementAtIndex(1).FindPropertyRelative("parts");
                for(int i=0;i<4;i++)parts.GetArrayElementAtIndex(i).FindPropertyRelative("thumbnail").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(Hoods+"/Icons/"+names[i]+".png");
                so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
                report.errors=Array.Empty<string>();
            }
            catch(Exception e){report.errors=new[]{e.ToString()};Debug.LogException(e);}
            File.WriteAllText(Output+"/customization-report.json",JsonUtility.ToJson(report,true));
            Debug.Log("CUSTOMIZATION_DONE combinations="+report.combinations+" errors="+report.errors.Length);
        }

        static void SetColors(SerializedProperty group,AvatarPartCategory category,string label,UnityEngine.Object icon,Swatch[] colors)
        {
            group.FindPropertyRelative("category").intValue=(int)category;
            group.FindPropertyRelative("label").stringValue=label;group.FindPropertyRelative("icon").objectReferenceValue=icon;
            var parts=group.FindPropertyRelative("parts");parts.arraySize=colors.Length;
            for(int i=0;i<colors.Length;i++)SetPart(parts.GetArrayElementAtIndex(i),colors[i].id,colors[i].label,colors[i].hex,null);
        }
        static void SetPart(SerializedProperty part,string id,string label,string hex,Mesh mesh)
        {
            part.FindPropertyRelative("id").stringValue=id;part.FindPropertyRelative("label").stringValue=label;
            ColorUtility.TryParseHtmlString(hex,out var color);part.FindPropertyRelative("swatch").colorValue=color;
            foreach(var field in new[]{"material","texture","thumbnail"})part.FindPropertyRelative(field).objectReferenceValue=null;
            part.FindPropertyRelative("mesh").objectReferenceValue=mesh;
        }
        static void Validate(AvatarPartCatalog catalog,Palette palette,Mesh[] meshes,Report report)
        {
            var a=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
            var b=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
            try
            {
                var applier=a.GetComponent<AvatarAppearanceApplier>();
                var body=a.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Body");
                var hood=a.GetComponentsInChildren<MeshFilter>().Single(r=>r.name=="Hood");
                var material=body.sharedMaterial;var block=new MaterialPropertyBlock();
                var shapes=catalog.Find(AvatarPartCategory.Hood).Parts;
                var hoodRenderer=hood.GetComponent<Renderer>();
                var originalBounds=hoodRenderer.bounds;
                foreach(var mesh in meshes)
                {
                    hood.sharedMesh=mesh;
                    if(Mathf.Abs(hoodRenderer.bounds.min.y-originalBounds.min.y)>.04f||
                       Vector3.Distance(hoodRenderer.bounds.center,originalBounds.center)>.5f)
                        throw new Exception("Hood mesh is not aligned with the authored head: "+mesh.name);
                }
                hood.sharedMesh=meshes[0];
                foreach(var shape in shapes)foreach(var bodyColor in palette.body)foreach(var hoodColor in palette.hood)
                {
                    var value=catalog.Default.With(AvatarPartCategory.Hood,shape.Id)
                        .With(AvatarPartCategory.BodyColor,bodyColor.id).With(AvatarPartCategory.HoodColor,hoodColor.id);
                    applier.Apply(value);
                    if(hood.sharedMesh!=shape.Mesh||body.sharedMaterial!=material)throw new Exception("Wrong mesh or material allocation");
                    CheckColor(body,bodyColor.hex,block);CheckColor(hood.GetComponent<Renderer>(),hoodColor.hex,block);
                    var roundTrip=new AvatarAppearance(value.BodyColorId,value.HoodId,value.ShoesId,value.FaceId);
                    if(roundTrip!=value||roundTrip.HoodShapeId!=shape.Id||roundTrip.HoodColorId!=hoodColor.id||value.HoodId.Length>32)
                        throw new Exception("Appearance storage round-trip failed");
                    report.combinations++;
                }
                if(b.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Hood").sharedMesh!=meshes[0])throw new Exception("Another avatar was changed");
                applier.Apply(AvatarAppearance.Default);
                if(hood.sharedMesh!=meshes[0])throw new Exception("Default hood restore failed");
                var head=a.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");
                var attachment=head.worldToLocalMatrix*hood.transform.localToWorldMatrix;
                foreach(var clip in SmoothBearAssets.LoadClips())foreach(var shape in shapes)
                {
                    clip.SampleAnimation(a,clip.length*.5f);
                    applier.Apply(catalog.Default.With(AvatarPartCategory.Hood,shape.Id));
                    var posed=head.worldToLocalMatrix*hood.transform.localToWorldMatrix;
                    for(int k=0;k<16;k++)if(Mathf.Abs(posed[k]-attachment[k])>.001f)
                        throw new Exception("Hood attachment changed in "+clip.name);
                    if(hood.sharedMesh!=shape.Mesh)throw new Exception("Animation replaced hood mesh");
                    report.animationSamples++;
                }
                report.shapes=4;report.bodyColors=palette.body.Length;report.hoodColors=palette.hood.Length;
            }
            finally {UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        }
        static void CheckColor(Renderer renderer,string hex,MaterialPropertyBlock block)
        {
            renderer.GetPropertyBlock(block,0);ColorUtility.TryParseHtmlString(hex,out var expected);
            var color=block.GetColor("_BaseColor");
            if(Mathf.Abs(color.r-expected.r)+Mathf.Abs(color.g-expected.g)+Mathf.Abs(color.b-expected.b)>.001f)throw new Exception("Wrong independent color "+hex);
        }
        static void RenderHoods(AvatarPartCatalog catalog,string[] names)
        {
            Directory.CreateDirectory(Hoods+"/Icons");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var originals=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Renderer>()).ToArray();
            var enabled=originals.Select(r=>r.enabled).ToArray();
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
            var cameraObject=new GameObject("HoodThumbnailCamera");var camera=cameraObject.AddComponent<Camera>();
            var rt=new RenderTexture(384,384,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try
            {
                foreach(var renderer in originals)renderer.enabled=false;
                instance.GetComponent<Animator>().enabled=false;SmoothBearAssets.LoadClip("Idle").SampleAnimation(instance,0);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;camera.enabled=false;camera.allowHDR=false;
                var applier=instance.GetComponent<AvatarAppearanceApplier>();
                for(int i=0;i<4;i++)
                {
                    applier.Apply(catalog.Default.With(AvatarPartCategory.Hood,"hood_"+names[i].ToLowerInvariant()));
                    var hood=instance.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Hood").GetComponent<Renderer>();
                    var center=hood.bounds.center;
                    camera.transform.position=center+new Vector3(0,0,5);camera.transform.LookAt(center);
                    camera.orthographicSize=Mathf.Max(hood.bounds.extents.y,hood.bounds.extents.x)*1.15f;
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                    RenderTexture.active=rt;var texture=new Texture2D(384,384,TextureFormat.RGBA32,false);
                    texture.ReadPixels(new Rect(0,0,384,384),0,0);texture.Apply();
                    File.WriteAllBytes(Hoods+"/Icons/"+names[i]+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(instance);
                for(int i=0;i<originals.Length;i++)originals[i].enabled=enabled[i];
                if(scene.isDirty)EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
