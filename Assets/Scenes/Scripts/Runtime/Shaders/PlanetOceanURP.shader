Shader "SolarSystemExplorer/PlanetOceanURP"
{
    Properties
    {
        _DeepColor("Deep Color", Color) = (0.06, 0.16, 0.36, 1)
        _ShallowColor("Shallow Color", Color) = (0.23, 0.74, 0.78, 1)
        _Alpha("Alpha", Range(0, 1)) = 0.75
        _Smoothness("Smoothness", Range(0, 1)) = 0.9
        _FresnelPower("Fresnel Power", Range(1, 8)) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float _Alpha;
                float _Smoothness;
                float _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight();

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);
                float horizon = 1.0 - saturate(abs(normalWS.y));
                float3 albedo = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(horizon + fresnel * 0.5));

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = albedo * (0.2 + NdotL * mainLight.shadowAttenuation);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float specular = pow(saturate(dot(normalWS, halfDir)), lerp(16.0, 96.0, _Smoothness));
                float3 color = diffuse + mainLight.color * specular * _Smoothness;

                color = MixFog(color, input.fogFactor);
                float alpha = saturate(_Alpha + fresnel * 0.15);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
