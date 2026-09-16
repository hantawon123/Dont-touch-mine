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
    /// <summary>Transfers the approved straight-knit review to shared Unity materials.</summary>
    [InitializeOnLoad]
    public static class SmoothBearKnitSetup
    {
        const string Output = "artifacts/smooth-bear-unity/knit-unity";
        const string Request = Output + "/apply.request";
        public const string ShaderName = "Game/Character/Straight Knit";
        const string Wearables = SmoothBearAssets.Root + "/Wearables";
        static SmoothBearKnitSetup() { EditorApplication.update += Check; }
        static void Check()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
            File.Delete(Request);
            Apply();
        }

        // The hood has an imported FBX basis; shoes are already in foot-bone space.
        // Capture their rest-space horizontal axes once, so ribs follow the part.
        public static void ApplyToMaterial(Material material, Matrix4x4 restToWorld)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Straight Knit shader is unavailable or has compile errors.");
            var color = material.GetColor("_BaseColor");
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", null);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_KnitSpacing", .022f * .978493f);
            material.SetFloat("_KnitRelief", .00022f);
            material.SetVector("_KnitU", restToWorld.transpose.MultiplyVector(Vector3.right));
            material.SetVector("_KnitV", restToWorld.transpose.MultiplyVector(Vector3.forward));
            EditorUtility.SetDirty(material);
        }

        [Serializable] class Report
        {
            public bool success;
            public int materials, hoodColorChecks, shoeColorChecks, animationSamples;
            public List<string> errors = new();
            public List<string> frames = new();
        }

        [MenuItem("Game/Setup/Approved straight hood and shoe knit")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            var report = new Report();
            GameObject model = null;
            try
            {
                model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.ModelPath));
                model.GetComponent<Animator>().enabled = false;
                var hood = model.GetComponentsInChildren<MeshFilter>().Single(x => x.name == "Hood");
                var hoodMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Materials/MAT_Hood.mat");
                Backup(hoodMaterial);
                ApplyToMaterial(hoodMaterial, hood.transform.localToWorldMatrix);report.materials++;
                var shoes = AssetDatabase.LoadAssetAtPath<AvatarWearable>(Wearables + "/CompactShoes.asset");
                foreach (var part in shoes.attachments)
                {
                    var bone = model.GetComponentsInChildren<Transform>().Single(x => x.name == part.bone);
                    foreach (var material in part.materials)
                    {
                        Backup(material);ApplyToMaterial(material, bone.localToWorldMatrix);report.materials++;
                    }
                }
                AssetDatabase.SaveAssets();
                ValidateAndRender(report);
                var errors = ShaderUtil.GetShaderMessages(Shader.Find(ShaderName)).Where(x => x.severity.ToString() == "Error").ToArray();
                if (errors.Length != 0) throw new Exception(string.Join("\n", errors.Select(x => x.message)));
                report.success = true;
            }
            catch (Exception e) { report.errors.Add(e.ToString());Debug.LogException(e); }
            finally { if (model != null) UnityEngine.Object.DestroyImmediate(model); }
            File.WriteAllText(Output + "/result.json", JsonUtility.ToJson(report, true));
            Debug.Log("STRAIGHT_KNIT_DONE success=" + report.success);
        }

        static void Backup(Material material)
        {
            var path = AssetDatabase.GetAssetPath(material);
            var target = Output + "/before-" + Path.GetFileName(path);
            if (!File.Exists(target)) File.Copy(path, target);
        }

        static void ValidateAndRender(Report report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmoothBearAssets.PrefabPath));
            var cameraObject = new GameObject("KnitValidationCamera");
            var keyObject = new GameObject("KnitValidationKey");var fillObject = new GameObject("KnitValidationFill");
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x => x.enabled).ToArray();
            var rt = new RenderTexture(900, 1000, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                foreach (var light in lights) light.enabled = false;
                instance.GetComponent<Animator>().enabled = false;
                instance.transform.position = new Vector3(1000, 0, 0);
                var applier = instance.GetComponent<AvatarAppearanceApplier>();
                var camera = cameraObject.AddComponent<Camera>();camera.enabled = false;
                camera.cullingMask = 1 << 30;camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.83f,.85f,.87f,1);camera.orthographic = true;camera.allowHDR = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var key = keyObject.AddComponent<Light>();key.type = LightType.Directional;key.intensity = 1.1f;key.shadows = LightShadows.Soft;key.cullingMask = 1 << 30;
                key.transform.rotation = Quaternion.Euler(35,-35,0);
                var fill = fillObject.AddComponent<Light>();fill.type = LightType.Directional;fill.intensity = .45f;fill.cullingMask = 1 << 30;fill.transform.rotation = Quaternion.Euler(25,130,0);
                var block = new MaterialPropertyBlock();
                var hoodRenderer = instance.GetComponentsInChildren<MeshRenderer>().Single(x => x.name == "Hood");
                var hoodMesh = hoodRenderer.GetComponent<MeshFilter>();
                var value = catalog.Default.With(AvatarPartCategory.Face,"face_brown");
                foreach (var shape in catalog.Find(AvatarPartCategory.Hood).Parts)
                {
                    foreach (var swatch in catalog.Find(AvatarPartCategory.HoodColor).Parts)
                    {
                        applier.Apply(value.With(AvatarPartCategory.Hood,shape.Id).With(AvatarPartCategory.HoodColor,swatch.Id));
                        hoodRenderer.GetPropertyBlock(block,0);
                        if (hoodMesh.sharedMesh != shape.Mesh || hoodRenderer.sharedMaterial.shader.name != ShaderName || block.GetColor("_BaseColor") != swatch.Swatch)
                            throw new Exception("Hood shape/color/material mismatch: " + shape.Id + "/" + swatch.Id);
                        report.hoodColorChecks++;
                    }
                    foreach (var clipName in new[]{"Idle","Walk_Forward","Crawl_Forward"})
                    {
                        SmoothBearAssets.LoadClip(clipName).SampleAnimation(instance,.3f);
                        applier.Apply(value.With(AvatarPartCategory.Hood,shape.Id));
                        if (hoodMesh.sharedMesh != shape.Mesh || hoodRenderer.sharedMaterial.shader.name != ShaderName) throw new Exception("Animated hood lost knit");
                        report.animationSamples++;
                    }
                    SmoothBearAssets.LoadClip("Idle").SampleAnimation(instance,0);
                    applier.Apply(value.With(AvatarPartCategory.Hood,shape.Id));
                    foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                    var bounds = hoodRenderer.bounds;
                    camera.orthographicSize = Mathf.Max(bounds.extents.y,bounds.extents.x)*1.18f;
                    camera.transform.position = bounds.center + new Vector3(0,.04f,4);camera.transform.LookAt(bounds.center);
                    Render(camera,rt,Output + "/" + shape.Id + "-front.png",report);
                    var icon = new RenderTexture(384,384,24,RenderTextureFormat.ARGB32);
                    try
                    {
                        camera.backgroundColor = Color.clear;
                        var animal = shape.Id.Substring("hood_".Length);
                        var iconPath = SmoothBearAssets.Root + "/Hoods/Icons/" + char.ToUpperInvariant(animal[0]) + animal.Substring(1) + ".png";
                        Render(camera,icon,iconPath,report);
                        AssetDatabase.ImportAsset(iconPath,ImportAssetOptions.ForceSynchronousImport);
                    }
                    finally { camera.backgroundColor = new Color(.83f,.85f,.87f,1);icon.Release();UnityEngine.Object.DestroyImmediate(icon); }
                    camera.transform.position = bounds.center + new Vector3(2,.3f,4);camera.transform.LookAt(bounds.center);
                    Render(camera,rt,Output + "/" + shape.Id + "-angle.png",report);
                }
                foreach (var swatch in catalog.Find(AvatarPartCategory.Shoes).Parts)
                {
                    applier.Apply(value.With(AvatarPartCategory.Shoes,swatch.Id));
                    foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>().Where(x=>x.name.StartsWith("Wearable_CompactShoes")))
                    {
                        renderer.GetPropertyBlock(block,0);
                        if (block.GetColor("_BaseColor") != swatch.Swatch || renderer.sharedMaterials.Any(x=>x.shader.name != ShaderName)) throw new Exception("Shoe color/material mismatch");
                        report.shoeColorChecks++;
                    }
                }
                applier.Apply(value);
                var shoeRenderers = instance.GetComponentsInChildren<MeshRenderer>().Where(x=>x.name.StartsWith("Wearable_CompactShoes")).ToArray();
                if (shoeRenderers.Length != 2) throw new Exception("Expected both shoes");
                var shoeBounds = shoeRenderers[0].bounds;shoeBounds.Encapsulate(shoeRenderers[1].bounds);
                camera.orthographicSize = shoeBounds.extents.magnitude * 1.35f;camera.transform.position = shoeBounds.center + new Vector3(1.4f,.9f,3);camera.transform.LookAt(shoeBounds.center);
                Render(camera,rt,Output + "/shoes.png",report);
                camera.orthographicSize = 1.16f;camera.transform.position = instance.transform.position + new Vector3(0,1.08f,5);camera.transform.LookAt(instance.transform.position+Vector3.up*1.08f);
                Render(camera,rt,Output + "/character.png",report);
            }
            finally
            {
                foreach (var light in lights) if (light != null) light.enabled = true;
                RenderTexture.active = previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(instance);UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(keyObject);UnityEngine.Object.DestroyImmediate(fillObject);
            }
        }

        static void Render(Camera camera, RenderTexture target, string path, Report report)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous;UnityEngine.Object.DestroyImmediate(texture); }
            report.frames.Add(path);
        }
    }
}
