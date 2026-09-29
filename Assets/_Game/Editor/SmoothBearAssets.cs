using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Client.Character;
using Game.Core.Players;

namespace Game.Editor
{
    /// <summary>One approved model and native animation set shared by preview and gameplay.</summary>
    public static class SmoothBearAssets
    {
        public const string Root = "Assets/_Game/Content/Characters/SmoothBear";
        public const string ModelPath = Root + "/SmoothBear.fbx";
        public const string PreviewControllerPath = Root + "/SmoothBearPreview.controller";
        public const string PrefabPath = Root + "/SmoothBear.prefab";
        public const string PlayerControllerPath = "Assets/_Game/Content/Animations/PlayerAnimator.controller";

        public static AnimationClip LoadClip(string name) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animations/" + name + ".anim");

        public static AnimationClip[] LoadClips() => AssetDatabase.FindAssets("t:AnimationClip",new[] { Root + "/Animations" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .Where(clip => clip != null).OrderBy(clip => clip.name,StringComparer.Ordinal).ToArray();

        public static void ApplyMaterials(GameObject visual)
        {
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var name = renderer.name switch
                {
                    "Body" => "MAT_Capsule_Character",
                    "Hood" => "MAT_Hood",
                    "Eye_White_L" or "Eye_White_R" => "MAT_EyeWhite",
                    "Eye_Pupil_L" or "Eye_Pupil_R" => "MAT_EyePupil",
                    "Mouth_Smile" => "MAT_Mouth",
                    _ => null
                };
                if(name == null) continue;
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Materials/"+name+".mat");
                if(material==null) throw new InvalidOperationException("Missing character material "+name);
                // The smooth body has one submesh. Extra material slots would
                // draw that surface again, hiding its yellow material.
                renderer.sharedMaterials=new[] { material };
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        public static void SaveCharacterPrefab()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if(source==null)return;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name="SmoothBear";
                var animator=instance.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);
                animator.applyRootMotion=false;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                ApplyMaterials(instance);
                WireAppearance(instance, instance, AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset"));
                PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        public static void WireAppearance(GameObject root, GameObject visual, AvatarPartCatalog catalog)
        {
            var applier=root.GetComponent<AvatarAppearanceApplier>()??root.AddComponent<AvatarAppearanceApplier>();
            var body=visual.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Body");
            var hood=visual.GetComponentsInChildren<MeshFilter>(true).Single(r=>r.name=="Hood");
            var so=new SerializedObject(applier);
            so.FindProperty("catalog").objectReferenceValue=catalog;
            so.FindProperty("hoodMesh").objectReferenceValue=hood;
            so.FindProperty("wearableRig").objectReferenceValue=visual.transform;
            var eyes=visual.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Eye_",StringComparison.Ordinal)).ToArray();
            var defaults=so.FindProperty("defaultEyes");defaults.arraySize=eyes.Length;
            for(int i=0;i<eyes.Length;i++)defaults.GetArrayElementAtIndex(i).objectReferenceValue=eyes[i];
            var targets=so.FindProperty("targets");targets.arraySize=2;
            for(int i=0;i<2;i++)
            {
                var target=targets.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("category").intValue=(int)(i==0?AvatarPartCategory.BodyColor:AvatarPartCategory.HoodColor);
                target.FindPropertyRelative("targetRenderer").objectReferenceValue=i==0?body:hood.GetComponent<Renderer>();
                target.FindPropertyRelative("materialIndex").intValue=0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
