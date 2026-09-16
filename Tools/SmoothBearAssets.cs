using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
                PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
