Shader "OpenSaber/DemonFaceComposite"
{
    Properties
    {
        [MainTexture] _BaseMap("Portrait", 2D) = "white" {}
        _BackingColor("Team backing", Color) = (0.192, 0.275, 0.62, 1)
        _RemoveGreenScreen("Remove green-screen pixels", Float) = 1
        _ChromaRemoval("Green removal strength", Range(0, 1)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BackingColor;
                float _RemoveGreenScreen;
                float _ChromaRemoval;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 rgb = t.rgb;
                half a = t.a;

                // Typical green-screen / lime: green dominates over R and B
                half gDom = t.g - max(t.r, t.b);
                half greenish = saturate(gDom * 4.0h);

                // Near pure green (0,1,0) — common export artifact
                half distToPureGreen = distance(t.rgb, half3(0, 1, 0));
                half nearPureGreen = 1.0h - saturate(distToPureGreen * 2.2h);

                half kill = saturate(max(greenish, nearPureGreen)) * _ChromaRemoval * _RemoveGreenScreen;
                a *= (1.0h - kill);

                // Straight-alpha composite onto team color; output is fully opaque (no sorting issues)
                half3 outRgb = lerp(_BackingColor.rgb, rgb, a);
                return half4(outRgb, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
