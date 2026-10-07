Shader "OVARP/FloorFade"
{
    Properties
    {
        _BaseColor      ("Base Color", Color) = (0.13, 0.14, 0.17, 1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.55
        _FadeStart      ("Fade Start", Range(0,0.5)) = 0.12
        _FadeEnd        ("Fade End", Range(0,0.5)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _ShadowStrength;
                float  _FadeStart;
                float  _FadeEnd;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Radial falloff from the centre of the quad, so the floor has no edge
                // to see however the camera is angled.
                float dist = length(IN.uv - 0.5);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, dist);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                half   atten       = MainLightRealtimeShadow(shadowCoord);
                half   shade       = lerp(1.0h, atten, _ShadowStrength);

                half3 rgb = _BaseColor.rgb * shade;
                return half4(rgb, _BaseColor.a * fade);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
