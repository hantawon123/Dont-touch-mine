using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Client.Interactions;
using Game.Client.Items;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class ItemCollectionBuilder
    {
        public const string Folder = "Assets/_Game/Content/ItemCollection";
        public const string ScenePath = "Assets/_Game/Content/Scenes/ItemGallery.unity";
        private const string Request = "Temp/BuildItemCollection.request";
        private const string Report = "docs/items";
        private static readonly string[] Categories = { "food", "household", "tools", "modern", "fantasy", "reserve" };
        private static readonly string[] Names = { "음식·음료", "생활용품", "공구·작업용품", "총기·폭발물", "판타지 소품", "보류·부품·모형" };
        private static readonly Color[] Colors = { new(.96f,.53f,.19f),new(.2f,.69f,.7f),new(.93f,.71f,.2f),new(.37f,.53f,.83f),new(.66f,.4f,.82f),new(.5f,.55f,.6f) };

        [Serializable] public class Entry
        {
            public string id, source, originalSource, package, category, categoryName, displayName, note, family;
            public string prefab, thumbnail;
            public Vector3 originalSize, finalSize;
            public float scale;
        }
        [Serializable] public class Manifest { public Entry[] items; }
        [Serializable] public class Result
        {
            public string characterSource = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
            public string scaleRule = "min(1, character.x/item.x, character.y/item.y, character.z/item.z); uniform scale; never enlarge";
            public Vector3 characterSize, maximumItemSize;
            public Entry[] items;
        }

        private static Scene scene, previous;
        private static Result result;
        private static int cursor;
        private static Camera camera;
        private static GameObject character;
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly Dictionary<string, Transform> Groups = new();
        private static readonly Dictionary<string, int> Counts = new();
        private static TMP_FontAsset font;

        [InitializeOnLoadMethod]
        private static void OnReload()
        {
            EditorApplication.update -= CheckRequest;
            EditorApplication.update += CheckRequest;
        }

        private static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            try { File.Delete(Request); } catch (IOException) { return; }
            Build();
        }

        [MenuItem("Tools/Game/Items/Build Categorized Item Gallery")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding the gallery.");
            if (result != null) return;
            // Recover only this builder's unfinished, unsaved scene after a domain reload.
            for (var i=SceneManager.sceneCount-1;i>=0;i--)
            {
                var pending=SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(pending.path) && pending.GetRootGameObjects().Any(g=>g.name=="Gallery Camera") &&
                    pending.GetRootGameObjects().Any(g=>g.name.StartsWith("01_음식·음료")))
                    EditorSceneManager.CloseScene(pending,true);
            }
            Directory.CreateDirectory(Folder + "/Materials");
            Directory.CreateDirectory(Report + "/images");
            AssetDatabase.Refresh();
            previous = SceneManager.GetActiveScene();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/ItemCollection.json"));
                result = new Result { items = manifest.items.OrderBy(e => Array.IndexOf(Categories,e.category)).ThenBy(e => e.source).ToArray() };
                Materials.Clear(); Groups.Clear(); Counts.Clear(); cursor = 0;
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Content/Fonts/Paperlogy-5Medium SDF.asset");
                var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(result.characterSource);
                var visual = playerAsset.transform.Find("Visual");
                if (visual == null) throw new InvalidOperationException("Player Visual is missing");
                character = Object.Instantiate(visual.gameObject);
                SceneManager.MoveGameObjectToScene(character,scene);
                character.name = "Current Character Visual";
                foreach (var script in character.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) Object.DestroyImmediate(script);
                foreach (var animator in character.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                result.characterSize = BoundsOf(character).size;
                if (Mathf.Min(result.characterSize.x,result.characterSize.y,result.characterSize.z) < .01f) throw new InvalidOperationException("Invalid character bounds");
                result.maximumItemSize = result.characterSize;
                character.SetActive(false);
                camera = NewObject("Gallery Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f,.07f,.095f);
                camera.nearClipPlane = .01f;
                camera.useOcclusionCulling = false;
                camera.farClipPlane = 700;
                camera.fieldOfView = 48;
                var sun = NewObject("Gallery Key Light").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.3f; sun.cullingMask = 1 << 30;
                sun.transform.rotation = Quaternion.Euler(45,-35,0);
                var fill = NewObject("Gallery Fill Light").AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = .55f; fill.cullingMask = 1 << 30;
                fill.transform.rotation = Quaternion.Euler(30,150,0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.55f,.6f,.7f);
                RenderSettings.fog = false;
                var navigator = camera.gameObject.AddComponent<ItemGalleryCamera>();
                navigator.viewpoints = new Transform[Categories.Length]; navigator.labels = Names;
                for (var i = 0; i < Categories.Length; i++)
                {
                    var cat = Categories[i]; var count = result.items.Count(e => e.category == cat);
                    var group = NewObject($"{i+1:00}_{Names[i]} ({count})").transform;
                    group.position = new Vector3(i * 19,0,0); Groups[cat] = group; Counts[cat] = 0;
                    var depth = Mathf.Ceil(count / 6f) * 2.4f + 6;
                    Box("Floor",new Vector3(0,-.15f,depth/2-3),new Vector3(16,.2f,depth),ColorMaterial("Floor",new Color(.065f,.085f,.12f)),group);
                    Label($"{i+1:00}  {Names[i]}  /  {count}" + (cat=="modern" ? "  (4개 부족)" : ""), new Vector3(0,1.8f,-2.6f), group, 3.5f, 15);
                    var reference = Object.Instantiate(character,group);
                    reference.name = "REFERENCE - Current Player 1x"; reference.SetActive(true);
                    SetLayer(reference,30);
                    RestOn(reference,new Vector3(-6.5f,0,0));
                    Label("현재 캐릭터 1×",new Vector3(-6.5f,.2f,-1),group,1.7f,2.4f);
                    var view = NewObject("Viewpoint",group).transform;
                    view.localPosition = new Vector3(0,5,-7);
                    view.LookAt(group.position + new Vector3(0,.3f,3.5f));
                    navigator.viewpoints[i] = view;
                }
                EditorApplication.update += BuildNext;
                EditorApplication.LockReloadAssemblies();
                File.WriteAllText("Temp/ItemCollection.progress","Started: " + result.items.Length);
            }
            catch (Exception e) { Fail(e); }
        }

        private static void BuildNext()
        {
            try
            {
                if (cursor == result.items.Length) { Finish(); return; }
                var entry = result.items[cursor];
                var source = InstantiateSource(entry.source);
                source.transform.position = Vector3.zero;
                entry.originalSize = BoundsOf(source).size;
                var limit = result.maximumItemSize;
                entry.scale = Mathf.Min(1,limit.x/Mathf.Max(.00001f,entry.originalSize.x),limit.y/Mathf.Max(.00001f,entry.originalSize.y),limit.z/Mathf.Max(.00001f,entry.originalSize.z));
                var wrapper = NewObject(entry.id);
                source.transform.SetParent(wrapper.transform,false);
                source.transform.localScale *= entry.scale;
                foreach (var script in source.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                foreach (var body in source.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                foreach (var collider in source.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(CompatibleMaterial).ToArray();
                var bounds = BoundsOf(wrapper);
                source.transform.position -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                bounds = BoundsOf(wrapper);
                entry.finalSize = bounds.size;
                Validate(entry,limit);
                var box = wrapper.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = Vector3.Max(bounds.size,Vector3.one*.005f);
                wrapper.AddComponent<Rigidbody>().isKinematic = true;
                var carry = wrapper.AddComponent<CarryableItem>();
                var so = new SerializedObject(carry); so.FindProperty("displayName").stringValue = entry.displayName; so.ApplyModifiedPropertiesWithoutUndo();
                SetLayer(wrapper,0);
                Directory.CreateDirectory(Folder + "/Prefabs/" + entry.category);
                entry.prefab = Folder + "/Prefabs/" + entry.category + "/" + entry.id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(wrapper,entry.prefab);
                SetLayer(wrapper,30);
                // Render the exact saved geometry and URP material, isolated from other exhibits.
                wrapper.transform.position = new Vector3(0,0,-300);
                var shot = BoundsOf(wrapper);
                camera.transform.position = shot.center + new Vector3(1.4f,1,-1.8f).normalized * Mathf.Max(.08f,shot.size.magnitude*1.55f);
                camera.transform.LookAt(shot.center);
                camera.farClipPlane = Mathf.Max(1,shot.size.magnitude*5);
                entry.thumbnail = "images/" + entry.id + ".png";
                Screenshot(Report + "/" + entry.thumbnail,384,320);
                camera.farClipPlane = 700;
                Object.DestroyImmediate(wrapper);
                var instance = InstantiateSource(entry.prefab);
                var catIndex = Array.IndexOf(Categories,entry.category);
                var index = Counts[entry.category]++;
                instance.transform.SetParent(Groups[entry.category],false);
                instance.transform.localPosition = new Vector3((index%6-2.5f)*1.9f,.1f,(index/6)*2.4f);
                instance.name = $"{index+1:000} {entry.displayName} [{entry.id}]";
                SetLayer(instance,30);
                Box("Display base",new Vector3(instance.transform.localPosition.x,0,instance.transform.localPosition.z),new Vector3(1.6f,.12f,1.6f),ColorMaterial(entry.category,Colors[catIndex]*.3f),Groups[entry.category]);
                Label($"{index+1:000}  {entry.displayName}\n{entry.finalSize.x:F2} × {entry.finalSize.y:F2} × {entry.finalSize.z:F2} m",instance.transform.localPosition+new Vector3(0,.02f,-.95f),Groups[entry.category],1.5f,1.9f);
                cursor++;
                File.WriteAllText("Temp/ItemCollection.progress",$"{cursor}/{result.items.Length} {entry.displayName}");
            }
            catch (Exception e) { Fail(e); }
        }

        private static void Finish()
        {
            EditorApplication.update -= BuildNext;
            Object.DestroyImmediate(character);
            var navigator = camera.GetComponent<ItemGalleryCamera>();
            navigator.exhibits=Categories.SelectMany(c=>Groups[c].GetComponentsInChildren<CarryableItem>().Select(item=>item.transform)).ToArray();
            navigator.categoryStarts=Categories.Select((c,i)=>Categories.Take(i).Sum(cat=>Counts[cat])).ToArray();
            for (var i = 0; i < Categories.Length; i++)
            {
                camera.transform.SetPositionAndRotation(navigator.viewpoints[i].position,navigator.viewpoints[i].rotation);
                Screenshot(Report+"/images/category_"+Categories[i]+".png",1600,1000);
            }
            camera.transform.SetPositionAndRotation(navigator.viewpoints[0].position,navigator.viewpoints[0].rotation);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save gallery scene");
            File.WriteAllText(Report+"/item-results.json",JsonUtility.ToJson(result,true));
            File.WriteAllText("Temp/ItemCollection.done",$"PASS: {result.items.Length} items; character {result.characterSize}; maximum {result.maximumItemSize}");
            Debug.Log(File.ReadAllText("Temp/ItemCollection.done"));
            result = null;
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
            EditorApplication.UnlockReloadAssemblies();
        }

        private static void Fail(Exception e)
        {
            EditorApplication.update -= BuildNext;
            Directory.CreateDirectory(Report);
            File.WriteAllText("Temp/ItemCollection.error",e.ToString());
            Debug.LogException(e); result = null;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            EditorApplication.UnlockReloadAssemblies();
        }

        private static void Validate(Entry e,Vector3 limit)
        {
            if (e.scale<=0 || e.scale>1 || float.IsNaN(e.scale) || e.finalSize.sqrMagnitude<1e-10f ||
                e.finalSize.x>limit.x+.001f || e.finalSize.y>limit.y+.001f || e.finalSize.z>limit.z+.001f)
                throw new InvalidOperationException("Invalid size: " + e.source);
            if ((e.originalSize*e.scale-e.finalSize).magnitude>.005f)
                throw new InvalidOperationException("Aspect ratio changed: " + e.source);
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var bounds = new Bounds(); var found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                Bounds local; Mesh baked = null;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    baked = new Mesh(); skin.BakeMesh(baked); local = baked.bounds;
                }
                else
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null) continue;
                    local = mesh.sharedMesh.bounds;
                }
                for (var i=0;i<8;i++)
                {
                    var p=renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if (!found) { bounds=new Bounds(p,Vector3.zero); found=true; } else bounds.Encapsulate(p);
                }
                if (baked != null) Object.DestroyImmediate(baked);
            }
            if (!found) throw new InvalidOperationException("No visible mesh: " + root.name);
            return bounds;
        }

        private static Material CompatibleMaterial(Material original)
        {
            if (original == null) throw new InvalidOperationException("Missing source material");
            var path = AssetDatabase.GetAssetPath(original);
            var key = AssetDatabase.AssetPathToGUID(path)+"_"+original.name;
            if (Materials.TryGetValue(key,out var cached)) return cached;
            Material material;
            if (original.shader != null && original.shader.name.StartsWith("Shader Graphs/"))
                material = new Material(original);
            else
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var textureKey = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (original.HasProperty(textureKey))
                {
                    material.SetTexture("_BaseMap",original.GetTexture(textureKey));
                    material.SetTextureScale("_BaseMap",original.GetTextureScale(textureKey));
                    material.SetTextureOffset("_BaseMap",original.GetTextureOffset(textureKey));
                }
                if (path.Contains("/Low_Poly_Weapons_VOL1/") && material.GetTexture("_BaseMap")==null)
                    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ItemSources/Low_Poly_Weapons_VOL1/Low Poly Weapons VOL.1/Textures_Guns.png"));
                if (original.HasProperty("_BaseColor")) material.SetColor("_BaseColor",original.GetColor("_BaseColor"));
                else if (original.HasProperty("_Color")) material.SetColor("_BaseColor",original.GetColor("_Color"));
                if (original.HasProperty("_Metallic")) material.SetFloat("_Metallic",original.GetFloat("_Metallic"));
                material.SetFloat("_Smoothness",original.HasProperty("_Smoothness")?original.GetFloat("_Smoothness"):original.HasProperty("_Glossiness")?original.GetFloat("_Glossiness"):.25f);
                if (original.HasProperty("_Cull")) material.SetFloat("_Cull",original.GetFloat("_Cull"));
                foreach (var property in new[]{"_MetallicGlossMap","_OcclusionMap","_EmissionMap"})
                    if (original.HasProperty(property) && original.GetTexture(property)!=null) material.SetTexture(property,original.GetTexture(property));
                if (original.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor",original.GetColor("_EmissionColor"));
                if (original.HasProperty("_BumpMap") && original.GetTexture("_BumpMap") != null)
                { material.SetTexture("_BumpMap",original.GetTexture("_BumpMap")); material.EnableKeyword("_NORMALMAP"); }
            }
            var dest = Folder+"/Materials/"+key+".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(dest);
            if (existing != null) { EditorUtility.CopySerialized(material,existing); Object.DestroyImmediate(material); material=existing; }
            else AssetDatabase.CreateAsset(material,dest);
            Materials[key]=material; return material;
        }

        private static GameObject InstantiateSource(string path)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset==null) throw new FileNotFoundException("Missing prefab/model",path);
            if (path.Contains("/Low_Poly_Weapons_VOL1/") && path.EndsWith(".prefab") &&
                asset.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh==null))
            {
                var model = AssetDatabase.FindAssets("t:Model",new[]{"Assets/ItemSources/Low_Poly_Weapons_VOL1"})
                    .Select(AssetDatabase.GUIDToAssetPath).Single(p=>Path.GetFileNameWithoutExtension(p)==Path.GetFileNameWithoutExtension(path));
                asset=AssetDatabase.LoadAssetAtPath<GameObject>(model);
            }
            return (GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
        }
        private static GameObject NewObject(string name,Transform parent=null)
        {
            var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene); go.layer=30;
            if (parent!=null) go.transform.SetParent(parent,false);
            return go;
        }
        private static void SetLayer(GameObject root,int layer) { foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=layer; }
        private static void RestOn(GameObject go,Vector3 position) { var b=BoundsOf(go); go.transform.position+=go.transform.parent.position+position-new Vector3(b.center.x,b.min.y,b.center.z); }
        private static Material ColorMaterial(string id,Color color)
        {
            if(Materials.TryGetValue("gallery_"+id,out var mat)) return mat;
            var path=Folder+"/Materials/gallery_"+id+".mat";
            mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.color=color; Materials["gallery_"+id]=mat; return mat;
        }
        private static void Box(string name,Vector3 position,Vector3 size,Material material,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.layer=30;
            go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        private static void Label(string text,Vector3 position,Transform parent,float size,float width)
        {
            var label=NewObject(text.Split('\n')[0],parent).AddComponent<TextMeshPro>();
            label.font=font; label.text=text; label.fontSize=size; label.alignment=TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta=new Vector2(width,1.4f); label.transform.localPosition=position;
            label.color=new Color(.94f,.96f,1); label.textWrappingMode=TextWrappingModes.Normal;
        }
        private static void Screenshot(string path,int width,int height)
        {
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var previousTarget=RenderTexture.active;
            try
            {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
                var pixels=texture.GetPixels32(); var background=pixels[0]; var visible=0;
                for(var i=0;i<pixels.Length;i+=7)
                    if(Math.Abs(pixels[i].r-background.r)+Math.Abs(pixels[i].g-background.g)+Math.Abs(pixels[i].b-background.b)>24) visible++;
                if(visible<8) { Object.DestroyImmediate(texture); throw new InvalidOperationException("Empty render: "+path); }
                File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            }
            finally { camera.targetTexture=null; RenderTexture.active=previousTarget; RenderTexture.ReleaseTemporary(target); }
        }
    }
}
