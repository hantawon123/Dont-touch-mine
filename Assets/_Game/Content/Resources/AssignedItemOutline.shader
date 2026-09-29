Shader "Game/AssignedItemOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.9, 0.05, 0.05, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.05)) = 0.012
        _OutlinePixels ("Outline Pixels", Float) = 0

        // 아래는 렌더 상태만 바꾸는 값. 기본값은 기존 동작(앞면 컬링, 깊이 LEqual, 색 쓰기, 스텐실 없음)과 같다.
        // InteractableFocusOutline의 SeeThrough(가려져도 보이기)가 마스크·껍질 재질에 다른 값을 넣는다.
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Float) = 15
        _StencilRef ("Stencil Ref", Float) = 0
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 0
    }

    SubShader
    {
        Tags
        {
            // AlphaTest+49 = 2499. Synty(Polygon) 재질은 알파 클립 때문에 큐 2450에 그려지므로, 그보다 뒤에 그려야
            // 앞에 있는 소품·선반이 윤곽선을 덮어쓰지 않는다(Geometry+1=2001이면 2450 소품이 나중에 그려져 가림).
            "Queue" = "AlphaTest+49"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "AssignedItemOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite Off
            ZTest [_ZTest]
            ColorMask [_ColorMask]
            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _OutlinePixels;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                if (_OutlinePixels > 0.001)
                {
                    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                    float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                    float4 positionCS = TransformWorldToHClip(positionWS);
                    float worldPerPixel = (2.0 * positionCS.w) /
                                          (max(_ScreenParams.y, 1.0) * abs(UNITY_MATRIX_P._m11));
                    output.positionCS = TransformWorldToHClip(
                        positionWS + (normalWS * (_OutlinePixels * worldPerPixel)));
                    return output;
                }

                float3 expanded = input.positionOS.xyz +
                                  (normalize(input.normalOS) * _OutlineWidth);
                output.positionCS = TransformObjectToHClip(expanded);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
