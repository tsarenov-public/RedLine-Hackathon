Shader "Hidden/Arcade/Peripheral Motion Blur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // Per-frame inputs supplied by URP's MotionBlurPostProcessPass.
        #if defined(USING_STEREO_MATRICES)
            float4x4 _ViewProjMStereo[2], _PrevViewProjMStereo[2];
            #define CURRENT_VP _ViewProjMStereo[unity_StereoEyeIndex]
            #define PREVIOUS_VP _PrevViewProjMStereo[unity_StereoEyeIndex]
        #else
            float4x4 _ViewProjM, _PrevViewProjM;
            #define CURRENT_VP _ViewProjM
            #define PREVIOUS_VP _PrevViewProjM
        #endif

        TEXTURE2D_X(_MotionVectorTexture);
        float _Intensity, _Clamp;

        struct MotionVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 textureUV : TEXCOORD0;
            float2 screenUV : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        MotionVaryings MotionVertex(Attributes input)
        {
            MotionVaryings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.screenUV = GetFullScreenTriangleTexCoord(input.vertexID);
            output.textureUV = DYNAMIC_SCALING_APPLY_SCALEBIAS(output.screenUV);
            return output;
        }

        float2 CameraMotion(float2 screenUV, float2 textureUV)
        {
            float depth = SampleSceneDepth(textureUV);
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
            #endif
            float4 world = float4(ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP), 1.0);
            float4 previous = mul(PREVIOUS_VP, world);
            float4 current = mul(CURRENT_VP, world);
            if (previous.w <= 1e-4 || current.w <= 1e-4) return 0.0;
            float2 velocity = (previous.xy / previous.w - current.xy / current.w) * 0.5;
            #if UNITY_UV_STARTS_AT_TOP
                velocity.y = -velocity.y;
            #endif
            return velocity;
        }

        half4 Blur(MotionVaryings input, int sampleCount, bool useMotionVectors)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.textureUV;
            half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            // The middle 64% of the frame retains the exact source sample.
            float edge = smoothstep(0.64, 0.98, abs(input.screenUV.x * 2.0 - 1.0));
            UNITY_BRANCH
            if (edge <= 0.0) return source;

            float2 velocity = useMotionVectors
                ? SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_LinearClamp, uv).xy
                : CameraMotion(input.screenUV, uv);
            float distance = length(velocity);
            velocity *= min(1.0, _Clamp / max(distance, 1e-5)) * _Intensity * edge;
            UNITY_BRANCH
            if (dot(velocity, velocity) < 1e-10) return source;

            half4 sum = source * 2.0h;
            UNITY_UNROLL
            for (int index = 0; index < sampleCount; index++)
            {
                float offset = (index + 1.0) / sampleCount;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + velocity * offset);
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - velocity * offset);
            }
            return sum / (2.0 * (sampleCount + 1));
        }

        half4 CameraLow(MotionVaryings input) : SV_Target { return Blur(input, 2, false); }
        half4 CameraMedium(MotionVaryings input) : SV_Target { return Blur(input, 3, false); }
        half4 CameraHigh(MotionVaryings input) : SV_Target { return Blur(input, 5, false); }
        half4 ObjectsLow(MotionVaryings input) : SV_Target { return Blur(input, 2, true); }
        half4 ObjectsMedium(MotionVaryings input) : SV_Target { return Blur(input, 3, true); }
        half4 ObjectsHigh(MotionVaryings input) : SV_Target { return Blur(input, 5, true); }
        ENDHLSL

        Pass
        {
            Name "Camera Low"
            HLSLPROGRAM
            #pragma vertex MotionVertex
            #pragma fragment CameraLow
            ENDHLSL
        }
        Pass
        {
            Name "Camera Medium"
            HLSLPROGRAM
            #pragma vertex MotionVertex
            #pragma fragment CameraMedium
            ENDHLSL
        }
        Pass
        {
            Name "Camera High"
            HLSLPROGRAM
            #pragma vertex MotionVertex
            #pragma fragment CameraHigh
            ENDHLSL
        }
        Pass
        {
            Name "Objects Low"
            HLSLPROGRAM
            #pragma vertex MotionVertex
            #pragma fragment ObjectsLow
            ENDHLSL
        }
        Pass
        {
            Name "Objects Medium"
            HLSLPROGRAM
            #pragma vertex MotionVertex
            #pragma fragment ObjectsMedium
            ENDHLSL
        }
        Pass
        {
            Name "Objects High"
            HLSLPROGRAM
            #pragma vertex MotionVertex
            #pragma fragment ObjectsHigh
            ENDHLSL
        }
    }
}
