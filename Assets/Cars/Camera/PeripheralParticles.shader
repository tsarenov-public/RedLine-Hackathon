Shader "Arcade/Peripheral Particles"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _SideStart ("Side threshold", Range(0,1)) = .69
        _TopStart ("Top threshold", Range(0,1)) = .76
        _TopWeight ("Top visibility", Range(0,1)) = 1
        _Feather ("Edge fade", Range(.01,.25)) = .10
        _Brightness ("Brightness", Range(0,2)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _SideStart, _TopStart, _TopWeight, _Feather, _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 fromCenter = 2 * (input.screenPos.xy / input.screenPos.w) - 1;
                half sides = smoothstep(_SideStart, _SideStart + _Feather, abs(fromCenter.x));
                half top = smoothstep(_TopStart, _TopStart + _Feather, fromCenter.y) * _TopWeight;
                half coverage = max(sides, top);
                clip(coverage - .002h);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
                color.rgb = saturate(color.rgb * sqrt(_Brightness));
                color.a = saturate(color.a * _Brightness) * coverage;
                return color;
            }
            ENDHLSL
        }
    }
}
