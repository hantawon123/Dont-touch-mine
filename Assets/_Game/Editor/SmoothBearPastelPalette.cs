using System;
using System.IO;
using System.Linq;
using Game.Client.Character;
using Game.Core.Players;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class SmoothBearPastelPalette
    {
        [Serializable] class Palette { public SmoothBearCustomizationSetup.Swatch[] body,hood,shoes; }
        public static void Apply()
        {
            var palette=JsonUtility.FromJson<Palette>(File.ReadAllText("Assets/_Game/Content/Config/SmoothBearPalette.json"));
            var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
            var so=new SerializedObject(catalog);var groups=so.FindProperty("groups");
            for(int g=0;g<groups.arraySize;g++)
            {
                var group=groups.GetArrayElementAtIndex(g);var category=(AvatarPartCategory)group.FindPropertyRelative("category").intValue;
                var colors=category==AvatarPartCategory.BodyColor?palette.body:category==AvatarPartCategory.HoodColor?palette.hood:category==AvatarPartCategory.Shoes?palette.shoes:null;
                if(colors==null)continue;
                group.FindPropertyRelative("defaultPartId").stringValue=category==AvatarPartCategory.BodyColor?"body_black":category==AvatarPartCategory.HoodColor?"hood_purple":"shoes_pink_vivid";
                if(colors.Select(c=>c.id).Distinct().Count()!=colors.Length)throw new Exception("Duplicate palette IDs");
                var parts=group.FindPropertyRelative("parts");parts.arraySize=colors.Length;
                for(int i=0;i<colors.Length;i++)
                {
                    var color=colors[i];if(!ColorUtility.TryParseHtmlString(color.hex,out var swatch))throw new Exception("Invalid palette colour "+color.id);
                    if((category==AvatarPartCategory.HoodColor?"hood_rabbit|"+color.id:color.id).Length>32)throw new Exception("Colour ID exceeds storage limit");
                    var part=parts.GetArrayElementAtIndex(i);part.FindPropertyRelative("id").stringValue=color.id;part.FindPropertyRelative("label").stringValue=color.label;
                    part.FindPropertyRelative("swatch").colorValue=swatch;
                    foreach(var field in new[]{"material","texture","mesh","thumbnail"})part.FindPropertyRelative(field).objectReferenceValue=null;
                    part.FindPropertyRelative("wearable").objectReferenceValue=category==AvatarPartCategory.Shoes?AssetDatabase.LoadAssetAtPath<AvatarWearable>(SmoothBearAssets.Root+"/Wearables/CompactShoes.asset"):null;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
    }
}
