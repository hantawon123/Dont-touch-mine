#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Summer Beach 팩이 URP 변환 중 _BaseMap을 잃은 경우, 이름 규칙으로 텍스처를 다시 연결한다.
    /// </summary>
    public static class SummerBeachMaterialFixer
    {
        private const string Root = "Assets/Summer Beach - Low Poly";
        private static readonly string[] TextureFolders =
        {
            Root + "/Textures",
            Root + "/Textures_2048",
        };

        [MenuItem("Tools/Game/Assets/Fix Summer Beach Materials")]
        public static void Fix()
        {
            var texturesByKey = BuildTextureIndex();
            var matGuids = AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" });
            var fixedCount = 0;
            var missing = new List<string>();

            foreach (var guid in matGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                    continue;

                var key = Path.GetFileNameWithoutExtension(path);
                if (!texturesByKey.TryGetValue(key, out var texPath))
                {
                    // fire_particule.mat <- fire_particule_D32.png
                    if (!texturesByKey.TryGetValue(key + "_D", out texPath))
                    {
                        missing.Add(path);
                        continue;
                    }
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    missing.Add(path + " (texture missing: " + texPath + ")");
                    continue;
                }

                var dirty = false;
                if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != tex)
                {
                    mat.SetTexture("_BaseMap", tex);
                    dirty = true;
                }

                if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != tex)
                {
                    mat.SetTexture("_MainTex", tex);
                    dirty = true;
                }

                // fence light emission map if present
                if (key == "light_fence_flagWarn_D")
                {
                    var ePath = FindExactTexture("light_fence_flagWarn_E");
                    if (!string.IsNullOrEmpty(ePath) && mat.HasProperty("_EmissionMap"))
                    {
                        var eTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ePath);
                        if (eTex != null && mat.GetTexture("_EmissionMap") != eTex)
                        {
                            mat.SetTexture("_EmissionMap", eTex);
                            mat.EnableKeyword("_EMISSION");
                            dirty = true;
                        }
                    }
                }

                if (!dirty)
                    continue;

                EditorUtility.SetDirty(mat);
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[SummerBeachMaterialFixer] fixed={fixedCount}, no-match={missing.Count}\n"
                    + string.Join("\n", missing)
            );
        }

        private static Dictionary<string, string> BuildTextureIndex()
        {
            // Prefer lower-res Textures/ over Textures_2048/
            var best = new Dictionary<string, (string path, int priority)>();
            foreach (var folder in TextureFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                var priority = folder.EndsWith("Textures_2048") ? 1 : 0;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var stem = Path.GetFileNameWithoutExtension(path);
                    var key = NormalizeTextureKey(stem);
                    if (!best.TryGetValue(key, out var cur) || priority < cur.priority)
                        best[key] = (path, priority);
                }
            }

            var result = new Dictionary<string, string>();
            foreach (var kv in best)
                result[kv.Key] = kv.Value.path;
            return result;
        }

        private static string FindExactTexture(string keyPrefix)
        {
            foreach (var folder in TextureFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;
                foreach (var guid in AssetDatabase.FindAssets(keyPrefix + " t:Texture2D", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var stem = Path.GetFileNameWithoutExtension(path);
                    if (NormalizeTextureKey(stem) == keyPrefix || stem.StartsWith(keyPrefix))
                        return path;
                }
            }

            return null;
        }

        private static string NormalizeTextureKey(string stem)
        {
            // beachball_D512 / cocktail_D256 / fire_particule_D32 / beachball_D2048
            return Regex.Replace(stem, @"(512|1024|2048|256|32|64|128)$", string.Empty);
        }
    }
}
#endif
