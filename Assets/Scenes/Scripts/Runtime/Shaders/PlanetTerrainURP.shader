// URP terrain shader for generated planets.
// Reads per-vertex color (written by CelestialBodyPlanetBuilder from height data)
// and applies standard URP Lambert + specular lighting using the scene sun.
Shader "SolarSystemExplorer/PlanetTerrainURP"
{
    Properties
    {
        _Tint("Global Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        _Metallic("Metallic", Range(0,1)) = 0.0
        _AmbientStrength("Ambient Strength", Range(0,1)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Smoothness;
                float _Metallic;
                float _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 color       : COLOR;
                float fogFactor    : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posIn = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmIn = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posIn.positionCS;
                OUT.positionWS  = posIn.positionWS;
                OUT.normalWS    = nrmIn.normalWS;
                OUT.color       = IN.color;
                OUT.fogFactor   = ComputeFogFactor(posIn.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 baseColor = IN.color.rgb * _Tint.rgb;
                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                Light mainLight = GetMainLight();

                float NdotL = saturate(dot(N, mainLight.direction));
                float3 ambient = baseColor * _AmbientStrength;
                float3 diffuse = baseColor * mainLight.color * NdotL;

                float3 halfDir = normalize(mainLight.direction + viewDir);
                float specularPower = lerp(16.0, 96.0, _Smoothness);
                float specularStrength = pow(saturate(dot(N, halfDir)), specularPower) * _Smoothness;
                float3 specular = mainLight.color * specularStrength;

                half4 color = half4(ambient + diffuse + specular, 1);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
