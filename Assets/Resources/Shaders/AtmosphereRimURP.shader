Shader "Custom/URP/AtmosphereRim"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.32, 0.62, 1.0, 1.0)
        _SunTint("Sun Tint", Color) = (0.95, 0.97, 1.0, 1.0)
        _Intensity("Intensity", Range(0.0, 10.0)) = 1.8
        _RimPower("Rim Power", Range(0.5, 10.0)) = 3.2
        _Alpha("Alpha", Range(0.0, 1.0)) = 0.5
        _DayStrength("Day Strength", Range(0.0, 3.0)) = 1.3
        _NightStrength("Night Strength", Range(0.0, 3.0)) = 0.25
        _TerminatorSharpness("Terminator Sharpness", Range(1.0, 8.0)) = 2.8
        _NightRimFloor("Night Rim Floor", Range(0.0, 0.5)) = 0.03
        _NightAlphaFloor("Night Alpha Floor", Range(0.0, 0.5)) = 0.02
        _SunScatterPower("Sun Scatter Power", Range(1.0, 20.0)) = 8.0
        _SunDirection("Sun Direction", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SunTint;
                half _Intensity;
                half _RimPower;
                half _Alpha;
                half _DayStrength;
                half _NightStrength;
                half _TerminatorSharpness;
                half _NightRimFloor;
                half _NightAlphaFloor;
                half _SunScatterPower;
                half4 _SunDirection;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = normalize(GetWorldSpaceViewDir(positionInputs.positionWS));

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half3 sunDirWS = normalize(_SunDirection.xyz);

                half ndv = saturate(dot(normalWS, viewDirWS));
                half rim = pow(saturate(1.0h - ndv), _RimPower);
                half softRim = pow(saturate(1.0h - ndv), max(1.0h, _RimPower * 0.5h));

                half day = saturate(dot(normalWS, sunDirWS));
                half sunLit = pow(day, max(1.0h, _TerminatorSharpness));
                half horizonFacingSun = saturate(dot(viewDirWS, sunDirWS));
                half sunScatter = pow(horizonFacingSun, _SunScatterPower) * day;

                half dayNightBlend = lerp(_NightStrength, _DayStrength, sunLit);
                half nightRimFactor = lerp(_NightRimFloor, 1.0h, sunLit);
                half nightAlphaFactor = lerp(_NightAlphaFloor, 1.0h, sunLit);
                half3 baseGlow = _BaseColor.rgb * rim * _Intensity * dayNightBlend;
                half3 horizonGlow = _BaseColor.rgb * softRim * _Intensity * 0.35h * nightRimFactor;
                half3 sunGlow = _SunTint.rgb * sunScatter * _Intensity;

                half alpha = saturate((_Alpha * rim * nightAlphaFactor) + (sunScatter * 0.15h));
                half3 color = baseGlow + horizonGlow + sunGlow;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
