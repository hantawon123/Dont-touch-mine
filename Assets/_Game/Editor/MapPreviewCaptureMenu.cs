using System;
using System.IO;
using System.Linq;
using Game.Client.Lobby;
using Game.Core.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 열려 있는 매치 맵 씬의 Main Camera 시점을 4:3 사진으로 찍어 맵 선택 UI의 미리보기
    /// (<c>Resources/UI/Maps/MapPreview_{mapId}.png</c>)로 저장한다.
    /// </summary>
    /// <remarks>
    /// 맵 id는 씬 이름을 <see cref="MapCatalog.MapIds"/>와 대소문자 무시로 맞춰 정한다
    /// (Supermarket.unity → supermarket). 카메라 배치·조명을 바꾼 뒤에는 다시 실행해 사진을 갱신한다.
    /// 씬은 수정하지 않는다. 카메라를 렌더 텍스처에 한 번만 그리므로 Forward+에서도 안전하다.
    /// </remarks>
    public static class MapPreviewCaptureMenu
    {
        private const string MenuPath = "Game/Match Map/Preview/Capture Map Preview From Main Camera";
        private const string OutputFolder = "Assets/_Game/Content/Resources/" + MapPreviewSprites.ResourceFolder;

        /// <summary>저장 해상도. UI는 200×150까지만 쓰지만 여유를 두고 4:3으로 굽는다.</summary>
        private const int CaptureWidth = 1600;
        private const int CaptureHeight = 1200;
        private const int ImportMaxSize = 1024;

        [MenuItem(MenuPath)]
        public static void CaptureFromMainCamera()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                Debug.LogError("[Map Preview] 저장된 씬을 먼저 열어 주세요.");
                return;
            }

            var mapId = ResolveMapId(scene.name);
            var camera = FindMainCamera();
            if (camera == null)
            {
                Debug.LogError("[Map Preview] 씬에 MainCamera 태그가 붙은 카메라가 없습니다.");
                return;
            }

            var path = Capture(camera, mapId);
            Debug.Log($"[Map Preview] '{mapId}' 미리보기 저장: {path} ({CaptureWidth}×{CaptureHeight}, 카메라 '{camera.name}' " +
                      $"위치 {camera.transform.position}, 회전 {camera.transform.eulerAngles})");
        }

        /// <summary>씬 이름과 같은 맵 id가 카탈로그에 있으면 그것을, 없으면 소문자 씬 이름을 쓴다.</summary>
        public static string ResolveMapId(string sceneName)
        {
            var match = MapCatalog.MapIds.FirstOrDefault(
                id => string.Equals(id, sceneName, StringComparison.OrdinalIgnoreCase));
            return match ?? sceneName.ToLowerInvariant();
        }

        private static Camera FindMainCamera()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return cameras.FirstOrDefault(c => c.CompareTag("MainCamera") && c.gameObject.activeInHierarchy)
                   ?? cameras.FirstOrDefault(c => c.CompareTag("MainCamera"));
        }

        private static string Capture(Camera camera, string mapId)
        {
            var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousAspect = camera.aspect;
            var wasActive = camera.gameObject.activeSelf;
            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            try
            {
                if (!wasActive)
                {
                    camera.gameObject.SetActive(true);
                }

                camera.targetTexture = renderTexture;
                camera.aspect = MapPreviewSprites.Aspect;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                texture.Apply(false, false);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                camera.ResetAspect();
                RenderTexture.active = previousActive;
                if (!wasActive)
                {
                    camera.gameObject.SetActive(false);
                }

                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            Directory.CreateDirectory(OutputFolder);
            var assetPath = OutputFolder + MapPreviewSprites.ResourcePrefix + mapId + ".png";
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(assetPath);
            return assetPath;
        }

        /// <summary>UI 스프라이트로 읽히게 하고, 밉맵 없이 1024까지만 유지해 용량을 줄인다.</summary>
        private static void ConfigureImporter(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = ImportMaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
