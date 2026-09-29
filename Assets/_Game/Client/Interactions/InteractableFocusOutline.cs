using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Client.Interactions
{
    /// <summary>조준 중인 집을 수 있는 물건에 주황 2px 실루엣을 그린다.</summary>
    /// <remarks>
    /// 기본으로는 자기 자식 렌더러에 실루엣을 씌운다. 씬에 이미 놓인 환경 소품
    /// (여러 개를 하나의 상호작용 대상으로 묶는 경우)에도 같은 실루엣을 쓰려면
    /// <see cref="sourceRenderers"/>에 그 렌더러들을 직접 지정한다. 지정된 것이
    /// 하나라도 있으면 자식 검색은 하지 않는다.
    /// <para>
    /// <see cref="SeeThrough"/>를 켜면 실루엣을 스텐실 2단계로 그린다. 먼저 물건이 차지하는 화면 영역을
    /// 깊이 무시·색 없이 스텐실에 표시하고, 그 바깥에만 뒤집힌 껍질을 깊이 무시로 그린다. 진열대처럼
    /// 옆 물건·선반 판·뒷줄 상품이 물건에 딱 붙어 있으면 보통의 껍질(깊이 검사 LEqual)은 전부 그 안에
    /// 파묻혀 보이지 않기 때문이다. 집는 물건은 이 방식을 쓰고, 상시 표시 설치물(로비 계획판)은 기본 방식을 쓴다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class InteractableFocusOutline : MonoBehaviour
    {
        public static readonly Color FocusColor = new(1f, 154f / 255f, 106f / 255f, 1f);
        public const float FocusPixels = 2f;

        /// <summary>실루엣 마스크에 쓰는 스텐실 비트. URP가 쓰는 상위 비트를 피해 사용자 영역(0~3)의 첫 비트를 쓴다.</summary>
        public const int StencilBit = 1;

        public const string OutlineChildName = "[Interactable Focus Outline]";
        public const string MaskChildName = "[Interactable Focus Outline Mask]";

        private const string ShaderResourceName = "AssignedItemOutline";
        // 셰이더 기본 큐(AlphaTest+49 = 2499)와 같은 구간. 알파 클립을 쓰는 Synty 재질(큐 2450)보다 뒤에 그려야
        // 앞의 소품·선반이 실루엣을 덮어쓰지 않는다. 마스크가 껍질보다 먼저여야 스텐실이 준비된다.
        private const int MaskRenderQueue = (int)RenderQueue.AlphaTest + 48;
        private const int SeeThroughOutlineRenderQueue = (int)RenderQueue.AlphaTest + 49;

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlinePixelsId = Shader.PropertyToID("_OutlinePixels");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private static readonly int ColorMaskId = Shader.PropertyToID("_ColorMask");
        private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");
        private static readonly int StencilReadMaskId = Shader.PropertyToID("_StencilReadMask");
        private static readonly int StencilWriteMaskId = Shader.PropertyToID("_StencilWriteMask");
        private static readonly int StencilCompId = Shader.PropertyToID("_StencilComp");
        private static readonly int StencilPassId = Shader.PropertyToID("_StencilPass");

        [Tooltip("실루엣을 씌울 렌더러. 비어 있으면 이 오브젝트의 자식 렌더러를 쓴다.")]
        [SerializeField]
        private Renderer[] sourceRenderers;

        [Tooltip("sourceRenderers와 같은 순서의 원본 메시. 정적 배칭된 소품은 런타임에 " +
                 "MeshFilter가 결합 메시를 가리키므로, 에디터에서 저장한 원본을 대신 쓴다.")]
        [SerializeField]
        private Mesh[] sourceMeshes;

        [Tooltip("실루엣 색. 집는 소품은 기본 주황, 상시 표시하는 설치물은 다른 색으로 구분한다.")]
        [SerializeField]
        private Color outlineColor = FocusColor;

        [Tooltip("실루엣 두께(화면 픽셀).")]
        [SerializeField]
        [Min(0.5f)]
        private float pixelWidth = FocusPixels;

        [Tooltip("켜면 옆 물건·선반에 가려지는 부분도 실루엣이 보인다(스텐실 2단계, 깊이 무시). " +
                 "진열 상품처럼 붙어 있는 물건용. 상시 표시 설치물은 끈다.")]
        [SerializeField]
        private bool seeThrough;

        private readonly List<Renderer> outlineRenderers = new();
        private Material outlineMaterial;
        private Material maskMaterial;
        private bool built;

        public Color Color => outlineColor;

        public float PixelWidth => pixelWidth;

        public bool IsVisible { get; private set; }

        /// <summary>실루엣 대상으로 지정된 렌더러 수. 0이면 자식 렌더러를 쓴다.</summary>
        public int SourceCount => sourceRenderers?.Length ?? 0;

        /// <summary>가려진 부분도 실루엣을 그릴지. 이미 만들어진 뒤 바꾸면 실루엣을 다시 만든다.</summary>
        public bool SeeThrough
        {
            get => seeThrough;
            set
            {
                if (seeThrough == value)
                {
                    return;
                }

                seeThrough = value;
                if (built)
                {
                    Rebuild();
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                EnsureBuilt();
            }

            IsVisible = visible;
            for (var index = 0; index < outlineRenderers.Count; index++)
            {
                // 외부 소품에 붙인 복사본은 씬 언로드 때 이 컴포넌트보다 먼저 사라질 수 있다.
                var renderer = outlineRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;
            var shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader == null)
            {
                Debug.LogError(
                    $"Interactable focus outline shader resource '{ShaderResourceName}' is missing.",
                    this);
                return;
            }

            outlineMaterial = CreateMaterial(shader, "Focus Outline");
            outlineMaterial.SetColor(OutlineColorId, outlineColor);
            outlineMaterial.SetFloat(OutlineWidthId, 0.001f);
            outlineMaterial.SetFloat(OutlinePixelsId, pixelWidth);

            if (seeThrough)
            {
                // 2단계: 물건 실루엣(스텐실 비트가 켜진 곳) 바깥에만, 깊이를 무시하고 껍질을 그린다.
                outlineMaterial.SetFloat(ZTestId, (float)CompareFunction.Always);
                outlineMaterial.SetFloat(StencilRefId, StencilBit);
                outlineMaterial.SetFloat(StencilReadMaskId, StencilBit);
                outlineMaterial.SetFloat(StencilWriteMaskId, StencilBit);
                outlineMaterial.SetFloat(StencilCompId, (float)CompareFunction.NotEqual);
                outlineMaterial.renderQueue = SeeThroughOutlineRenderQueue;

                // 1단계: 물건이 차지하는 화면 영역을 색은 쓰지 않고 스텐실에만 표시한다.
                // 깊이를 무시하므로 다른 것에 가려진 부분도 포함되고, 양면을 그려 한 면짜리 메시도 빠지지 않는다.
                maskMaterial = CreateMaterial(shader, "Focus Outline Mask");
                maskMaterial.SetFloat(OutlineWidthId, 0f);
                maskMaterial.SetFloat(OutlinePixelsId, 0f);
                maskMaterial.SetFloat(CullId, (float)CullMode.Off);
                maskMaterial.SetFloat(ZTestId, (float)CompareFunction.Always);
                maskMaterial.SetFloat(ColorMaskId, 0f);
                maskMaterial.SetFloat(StencilRefId, StencilBit);
                maskMaterial.SetFloat(StencilReadMaskId, StencilBit);
                maskMaterial.SetFloat(StencilWriteMaskId, StencilBit);
                maskMaterial.SetFloat(StencilCompId, (float)CompareFunction.Always);
                maskMaterial.SetFloat(StencilPassId, (float)StencilOp.Replace);
                maskMaterial.renderQueue = MaskRenderQueue;
            }

            var sources = SourceCount > 0
                ? sourceRenderers
                : GetComponentsInChildren<Renderer>(includeInactive: true);
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index];
                if (source == null || ItemOutlineRenderers.IsGenerated(source))
                {
                    continue;
                }

                switch (source)
                {
                    case MeshRenderer meshRenderer:
                        CreateMeshOutline(meshRenderer, StoredMesh(index));
                        break;
                    case SkinnedMeshRenderer skinnedRenderer:
                        CreateSkinnedOutline(skinnedRenderer);
                        break;
                }
            }
        }

        private Material CreateMaterial(Shader shader, string label)
        {
            return new Material(shader)
            {
                name = $"{name} {label} (Runtime)",
                hideFlags = HideFlags.DontSave,
            };
        }

        /// <summary>지정 소품용으로 저장된 원본 메시. 자식 렌더러 모드나 미저장이면 null.</summary>
        private Mesh StoredMesh(int index)
        {
            return SourceCount > 0 && sourceMeshes != null && index < sourceMeshes.Length
                ? sourceMeshes[index]
                : null;
        }

        /// <remarks>
        /// 정적 배칭이 켜진 소품은 플레이 중 <c>MeshFilter.sharedMesh</c>가 씬 전체를 합친
        /// "Combined Mesh"가 되어, 그대로 복사하면 엉뚱한 곳에 다른 물건의 실루엣이 그려진다.
        /// 저장된 원본 메시가 있으면 그것을 우선한다.
        /// </remarks>
        private void CreateMeshOutline(MeshRenderer source, Mesh storedMesh)
        {
            var sourceFilter = source.GetComponent<MeshFilter>();
            var mesh = storedMesh != null ? storedMesh : sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (mesh == null)
            {
                return;
            }

            if (maskMaterial != null)
            {
                CreateMeshCopy(source.transform, mesh, maskMaterial, MaskChildName);
            }

            CreateMeshCopy(source.transform, mesh, outlineMaterial, OutlineChildName);
        }

        private void CreateMeshCopy(Transform parent, Mesh mesh, Material material, string childName)
        {
            var child = CreateOutlineChild(parent, childName);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            Configure(renderer, mesh.subMeshCount, material);
        }

        private void CreateSkinnedOutline(SkinnedMeshRenderer source)
        {
            if (source.sharedMesh == null)
            {
                return;
            }

            if (maskMaterial != null)
            {
                CreateSkinnedCopy(source, maskMaterial, MaskChildName);
            }

            CreateSkinnedCopy(source, outlineMaterial, OutlineChildName);
        }

        private void CreateSkinnedCopy(SkinnedMeshRenderer source, Material material, string childName)
        {
            var child = CreateOutlineChild(source.transform, childName);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = source.sharedMesh;
            renderer.rootBone = source.rootBone;
            renderer.bones = source.bones;
            renderer.localBounds = source.localBounds;
            renderer.updateWhenOffscreen = source.updateWhenOffscreen;
            Configure(renderer, source.sharedMaterials.Length, material);
        }

        /// <remarks>
        /// 원본 렌더러의 자식으로 붙인다. 원본이 정적(static)이어도 새 자식은
        /// 정적 플래그를 받지 않으므로 라이트맵·배칭에 섞이지 않는다.
        /// </remarks>
        private GameObject CreateOutlineChild(Transform parent, string childName)
        {
            var child = new GameObject(childName)
            {
                layer = parent.gameObject.layer,
                hideFlags = HideFlags.DontSave,
            };
            child.transform.SetParent(parent, worldPositionStays: false);
            return child;
        }

        private void Configure(Renderer renderer, int materialCount, Material material)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var materials = new Material[Mathf.Max(1, materialCount)];
            for (var index = 0; index < materials.Length; index++)
            {
                materials[index] = material;
            }

            renderer.sharedMaterials = materials;
            renderer.enabled = false;
            outlineRenderers.Add(renderer);
        }

        /// <summary>만들어 둔 실루엣 복사본과 재질을 모두 치우고, 보이던 중이면 새 설정으로 다시 만든다.</summary>
        private void Rebuild()
        {
            var visible = IsVisible;
            DestroyGenerated(includeOwnChildren: true);
            built = false;
            if (visible)
            {
                SetVisible(true);
            }
        }

        private void OnDestroy()
        {
            // 외부 렌더러에 붙인 실루엣은 이 컴포넌트가 사라져도 남으므로 함께 치운다.
            // 자기 자식에 붙인 것은 오브젝트와 함께 사라진다.
            DestroyGenerated(includeOwnChildren: false);
        }

        private void DestroyGenerated(bool includeOwnChildren)
        {
            for (var index = 0; index < outlineRenderers.Count; index++)
            {
                var renderer = outlineRenderers[index];
                if (renderer == null || (!includeOwnChildren && renderer.transform.IsChildOf(transform)))
                {
                    continue;
                }

                DestroyObject(renderer.gameObject);
            }

            outlineRenderers.Clear();

            if (outlineMaterial != null)
            {
                DestroyObject(outlineMaterial);
                outlineMaterial = null;
            }

            if (maskMaterial != null)
            {
                DestroyObject(maskMaterial);
                maskMaterial = null;
            }
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
