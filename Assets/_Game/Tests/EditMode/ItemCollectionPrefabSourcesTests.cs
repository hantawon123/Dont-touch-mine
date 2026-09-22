using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// ItemCollection 래퍼 프리팹은 ItemSources의 팩 프리팹을 중첩 프리팹으로 참조한다.
    /// 장난감 카테고리는 처음 커밋에 소스 팩이 빠져, 다른 브랜치에서 콜라이더만 남고 메시가
    /// 보이지 않았다. 이 테스트는 래퍼가 참조하는 프리팹·메시·머티리얼·텍스처가 저장소에
    /// 있는지, 바이너리가 git-lfs 포인터로 남아 있지 않은지 커밋 전에 잡는다.
    /// </summary>
    public sealed class ItemCollectionPrefabSourcesTests
    {
        private const string PrefabsFolder = ItemCollectionBuilder.Folder + "/Prefabs";
        private const string ErrorShader = "Hidden/InternalErrorShader";
        private const string LfsPointerPrefix = "version https://git-lfs.github.com/spec";

        private static readonly Regex GuidPattern = new(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);
        private static readonly Regex ShaderGuidPattern = new(@"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", RegexOptions.Compiled);
        private static readonly Regex TextureGuidPattern = new(@"m_Texture: \{fileID: -?\d+, guid: ([0-9a-f]{32})", RegexOptions.Compiled);
        private static readonly Regex BuiltinGuidPattern = new(@"^0{16}[0-9a-f]0{15}$", RegexOptions.Compiled);

        private static readonly string[] BinaryExtensions =
        {
            ".fbx", ".obj", ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff",
        };

        private static readonly string[] TextAssetExtensions = { ".prefab", ".mat" };

        [Test]
        public void EveryItemPrefabResolvesItsSourceAssets()
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(prefabPaths, Is.Not.Empty, PrefabsFolder);

            var problems = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in prefabPaths)
            {
                CheckReferences(path, path, visited, problems);
            }

            Report(problems, "다음 참조가 저장소에 없거나 LFS 포인터입니다. 소스 팩을 같은 커밋에 포함하고 git lfs pull을 확인하세요.");
        }

        [Test]
        public void EveryItemPrefabHasVisibleMesh()
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(prefabPaths, Is.Not.Empty, PrefabsFolder);

            var problems = new List<string>();
            foreach (var path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    problems.Add($"{path}: 프리팹을 불러오지 못했습니다.");
                    continue;
                }

                var meshes = 0;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                    {
                        problems.Add($"{path}: '{filter.name}' MeshFilter의 메시가 없습니다.");
                        continue;
                    }

                    meshes++;
                }

                foreach (var skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skinned.sharedMesh == null)
                    {
                        problems.Add($"{path}: '{skinned.name}' SkinnedMeshRenderer의 메시가 없습니다.");
                        continue;
                    }

                    meshes++;
                }

                if (meshes == 0)
                {
                    problems.Add($"{path}: 보이는 메시가 없습니다. 중첩 소스 프리팹이 누락됐을 수 있습니다.");
                }

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    if (materials.Length == 0)
                    {
                        problems.Add($"{path}: '{renderer.name}' 렌더러에 머티리얼이 없습니다.");
                    }

                    for (var index = 0; index < materials.Length; index++)
                    {
                        var material = materials[index];
                        if (material == null)
                        {
                            problems.Add($"{path}: '{renderer.name}' 머티리얼 {index}가 비어 있습니다.");
                        }
                        else if (material.shader == null || material.shader.name == ErrorShader)
                        {
                            problems.Add($"{path}: '{renderer.name}' 머티리얼 '{material.name}'의 셰이더가 깨졌습니다.");
                        }
                    }
                }
            }

            Report(problems, "다음 프리팹은 인게임에서 보이지 않거나 깨져 보입니다.");
        }

        private static void CheckReferences(
            string root,
            string path,
            HashSet<string> visited,
            List<string> problems)
        {
            if (!visited.Add(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException exception)
            {
                problems.Add($"{root}: {path} 읽기 실패 ({exception.Message})");
                return;
            }

            if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                // 팩 원본 머티리얼에는 배포되지 않은 마스크 텍스처 같은 끊긴 참조가 이미 있다.
                // 보이지 않게 만드는 건 셰이더 누락이므로 셰이더만 필수로 보고, 텍스처는 있을 때만 검사한다.
                foreach (Match match in ShaderGuidPattern.Matches(text))
                {
                    Resolve(root, path, match.Groups[1].Value, required: true, visited, problems);
                }

                foreach (Match match in TextureGuidPattern.Matches(text))
                {
                    Resolve(root, path, match.Groups[1].Value, required: false, visited, problems);
                }

                return;
            }

            foreach (Match match in GuidPattern.Matches(text))
            {
                Resolve(root, path, match.Groups[1].Value, required: true, visited, problems);
            }
        }

        private static void Resolve(
            string root,
            string path,
            string guid,
            bool required,
            HashSet<string> visited,
            List<string> problems)
        {
            if (BuiltinGuidPattern.IsMatch(guid))
            {
                return;
            }

            var resolved = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(resolved))
            {
                if (required)
                {
                    problems.Add($"{root}: {path} 가 참조하는 guid {guid} 에 해당하는 에셋이 저장소에 없습니다.");
                }

                return;
            }

            var extension = Path.GetExtension(resolved).ToLowerInvariant();
            if (TextAssetExtensions.Contains(extension))
            {
                CheckReferences(root, resolved, visited, problems);
            }
            else if (BinaryExtensions.Contains(extension) && visited.Add(resolved))
            {
                CheckNotLfsPointer(root, resolved, problems);
            }
        }

        private static void CheckNotLfsPointer(string root, string path, List<string> problems)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path))
            {
                return;
            }

            var info = new FileInfo(path);
            if (info.Length > 512)
            {
                return;
            }

            var head = new byte[Math.Min(64, (int)info.Length)];
            using (var stream = File.OpenRead(path))
            {
                var read = stream.Read(head, 0, head.Length);
                Array.Resize(ref head, read);
            }

            if (Encoding.ASCII.GetString(head).StartsWith(LfsPointerPrefix, StringComparison.Ordinal))
            {
                problems.Add($"{root}: {path} 는 git-lfs 포인터입니다. git lfs pull 이 필요합니다.");
            }
        }

        private static void Report(List<string> problems, string headline)
        {
            if (problems.Count == 0)
            {
                return;
            }

            var shown = problems.Take(40).ToList();
            if (problems.Count > shown.Count)
            {
                shown.Add($"... 외 {problems.Count - shown.Count}건");
            }

            Assert.Fail(headline + "\n" + string.Join("\n", shown));
        }
    }
}
