using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Tools.Editor
{
    /// <summary>
    /// 한글 폰트를 Dynamic 아틀라스 모드로 생성한다.
    /// 한글 완성형은 11,172자라 Static 모드로 한 장에 구우면 글자당 픽셀이 뭉개진다.
    /// Dynamic은 실제로 쓰인 글자만 런타임에 아틀라스로 채우므로 크기 손실이 없다.
    /// </summary>
    public static class KoreanFontAssetBuilder
    {
        private const string SourceFontPath =
            "Assets/_Game/Content/Fonts/Cafe24Ssurround-v2.0.ttf";

        private const string OutputPath =
            "Assets/_Game/Content/Fonts/Cafe24Ssurround SDF.asset";

        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 1024;

        [MenuItem("Tools/Fonts/Build Korean Font Asset (Dynamic)")]
        public static void Build()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"원본 폰트를 찾을 수 없습니다: {SourceFontPath}");
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError(
                    "폰트 애셋 생성에 실패했습니다. " +
                    "TTF Import Settings에서 Include Font Data가 켜져 있는지 확인하세요.");
                return;
            }

            if (File.Exists(OutputPath))
            {
                AssetDatabase.DeleteAsset(OutputPath);
            }

            AssetDatabase.CreateAsset(fontAsset, OutputPath);

            // 아틀라스 텍스처와 머티리얼을 서브 애셋으로 함께 저장한다.
            var atlasTexture = fontAsset.atlasTextures[0];
            atlasTexture.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);

            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignAsDefaultFontAsset(fontAsset);

            Debug.Log($"폰트 애셋을 생성했습니다: {OutputPath}", fontAsset);
            Selection.activeObject = fontAsset;
        }

        /// <summary>
        /// 씬과 프리팹의 TMP 텍스트는 fontAsset을 비워 두었으므로
        /// TMP Settings의 기본 폰트만 바꾸면 전부 이 폰트를 따라간다.
        /// </summary>
        private static void AssignAsDefaultFontAsset(TMP_FontAsset fontAsset)
        {
            const string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
            if (settings == null)
            {
                Debug.LogWarning(
                    $"TMP Settings를 찾을 수 없어 기본 폰트를 지정하지 못했습니다: {settingsPath}");
                return;
            }

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
            serializedSettings.ApplyModifiedProperties();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log("TMP Settings의 Default Font Asset을 새 폰트로 지정했습니다.", settings);
        }
    }
}
