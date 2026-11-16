// WallDecalMultiply.shader
//
// Shader for the Wall Grime decals (blood, scratches, dirt). It multiplies (darkens and tints) the
// wall behind it by the picture: where the picture is see-through, the wall is left exactly as it is.
// So the decal always matches the wall's lighting, including the baked lightmaps.
//
// Why not URP Unlit with Multiply blending: Unlit turns the see-through parts white (no change) and
// THEN mixes in the fog colour, so in the distance the whole square darkens the wall and shows as a
// box. Here the decal instead fades out with the fog, and the wall behind is fogged as usual.
//
// Setup: nothing. WallGrimeTextures puts this shader on the Wall Grime materials.

Shader "Escape Granny/Wall Decal (Multiply)"
{
    Properties
    {
        _BaseMap ("Decal (RGB = colour, A = how much)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WallDecal"
            Tags { "LightMode" = "UniversalForward" }

            Blend DstColor Zero   // result = wall colour x decal colour
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output); // both eyes on Quest
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 decal = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // How visible through the fog: 1 up close, 0 deep in the fog. Same check as URP's
                // MixFogColor: ComputeFogIntensity returns 0 when fog is off, which would hide the decal.
                bool anyFogEnabled = false;
                #if defined(FOG_LINEAR_KEYWORD_DECLARED)
                if (FOG_LINEAR) anyFogEnabled = true;
                #endif
                #if defined(FOG_EXP_KEYWORD_DECLARED)
                if (FOG_EXP) anyFogEnabled = true;
                #endif
                #if defined(FOG_EXP2_KEYWORD_DECLARED)
                if (FOG_EXP2) anyFogEnabled = true;
                #endif
                half visibility = anyFogEnabled ? ComputeFogIntensity(input.fogFactor) : 1.0h;

                // White = no change to the wall. Blend toward the decal colour by its opacity, less in fog.
                half amount = decal.a * visibility;
                return half4(lerp(half3(1, 1, 1), decal.rgb, amount), 1);
            }
            ENDHLSL
        }
    }
}
