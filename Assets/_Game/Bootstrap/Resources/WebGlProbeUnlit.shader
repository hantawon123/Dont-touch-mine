Shader "Game/Diagnostics/WebGlProbeUnlit"
{
    Properties { _ZTest ("Depth test", Float) = 4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 positionOS : POSITION; };
            struct Output { float4 positionCS : SV_POSITION; };
            Output Vert(Input input) { Output o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half4 Frag(Output input) : SV_Target { return half4(0,1,0,1); }
            ENDHLSL
        }
    }
}
