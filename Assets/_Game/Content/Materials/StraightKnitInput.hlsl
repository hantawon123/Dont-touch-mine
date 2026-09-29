#ifndef GAME_STRAIGHT_KNIT_INPUT
#define GAME_STRAIGHT_KNIT_INPUT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Smoothness;
half _Metallic;
half _Cutoff;
half _Surface;
float4 _KnitU;
float4 _KnitV;
float _KnitSpacing;
float _KnitRelief;
CBUFFER_END

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    surface = (SurfaceData)0;
    half4 base = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    surface.albedo = base.rgb * _BaseColor.rgb;
    surface.alpha = Alpha(base.a, _BaseColor, _Cutoff);
    surface.metallic = _Metallic;
    surface.specular = _SpecColor.rgb;
    surface.smoothness = _Smoothness;
    surface.normalTS = half3(0, 0, 1);
    surface.occlusion = 1;
    surface.emission = _EmissionColor.rgb;
}

// Object-anchored straight ribs reproduce the Blender review's two projections.
// No polar UVs: ears and shell share physical spacing, including during animation.
float KnitSlope(float coordinate, float frequency)
{
    float phase = coordinate * frequency;
    // Fade subpixel ribs instead of allowing moire when the avatar is far away.
    float footprint = fwidth(phase);
    return cos(phase) * frequency * saturate(1 - footprint / 6.2831853);
}

half3 KnitNormalWS(float3 positionWS, half3 normalWS)
{
    float3 p = TransformWorldToObject(positionWS);
    float3 n = normalize(mul(normalWS, (float3x3)GetObjectToWorldMatrix()));
    float3 u = _KnitU.xyz;
    float3 v = _KnitV.xyz;
    float side = pow(abs(dot(n, normalize(u))), 8);
    float front = pow(abs(dot(n, normalize(v))), 8);
    float weight = side / max(side + front, 0.000001);
    float frequency = 6.2831853 / max(_KnitSpacing, 0.001);
    float3 gradientOS = lerp(u * KnitSlope(dot(p,u), frequency),
                            v * KnitSlope(dot(p,v), frequency), weight) * _KnitRelief;
    float3 gradientWS = mul(gradientOS, (float3x3)GetWorldToObjectMatrix());
    // The tangential gradient changes shading only; the silhouette stays intact.
    return normalize(normalWS - gradientWS + normalWS * dot(normalWS, gradientWS));
}
#endif
