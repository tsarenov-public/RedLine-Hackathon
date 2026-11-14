using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
#if TND_URP
using UnityEngine.Rendering.Universal;
#endif

namespace TND.Upscaling.Framework.IUFW
{
    public abstract partial class UpscalerAdapter: AbstractUpscaler
    {
        public static readonly string UpscalerId = "com.tnd.upscaling";
        public static readonly string UpscalerDisplayName = "TND Upscaling";
        
        private static readonly string TexArraysKeyword = "TND_USE_TEXARRAYS";
        private static readonly string SrgbKeyword = "TND_SRGB";
        
        [AutoStaticsCleanup]
        private static GlobalKeyword s_texArraysKeyword;
        [AutoStaticsCleanup]
        private static GlobalKeyword s_srgbKeyword;

        public override string upscalerId => UpscalerId;
        public override string name => UpscalerDisplayName;
        public override bool isTemporal => true;    // TODO: should be variable depending on which upscaler is selected
        public override bool supportsSharpening => true;    // All TND upscalers have built-in sharpening support
        public override bool hasQualityMode => true;        // All TND upscalers are controlled with a standardized set of quality modes
        public override bool isSupportedOnDevice => true;
        public override ReactiveMaskSource reactiveMaskSource => ReactiveMaskSource.Color;

        private Vector2Int _previousPostUpscaleResolution;

        protected UpscalerAdapter()
        {
            s_texArraysKeyword = GlobalKeyword.Create(TexArraysKeyword);
            s_srgbKeyword = GlobalKeyword.Create(SrgbKeyword);
        }

        public override IUpscalerContext CreateContext(UpscalerOptions options, Vector2Int displayResolution)
        {
            if (options is not UpscalerAdapterOptions upscalerOptions)
                return null;

            return new UpscalerAdapterContext(upscalerOptions, displayResolution);
        }

        class PassData
        {
            public UpscalerAdapterContext upscalerContext;
            
            public UpscalerInitParams initParams;
            public UpscalerDispatchParams dispatchParams;

            public TextureHandle inputColor;
            public TextureHandle inputDepth;
            public TextureHandle inputMotionVectors;
            public TextureHandle inputExposure;
            public TextureHandle inputReactiveMask;
            public TextureHandle inputOpaqueColor;
            public TextureHandle outputColor;

            public bool isSRGB;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UpscalingIO io = frameData.Get<UpscalingIO>();
            
            if (io.context is not UpscalerAdapterContext upscalerContext)
            {
                return;
            }
            
            var upscalerOptions = io.options as UpscalerAdapterOptions;
            var inputColorDesc = io.cameraColor.GetDescriptor(renderGraph);

            TextureHandle outputColor;
            {
                TextureDesc outputDesc = inputColorDesc;
                outputDesc.width = io.postUpscaleResolution.x;
                outputDesc.height = io.postUpscaleResolution.y;
                outputDesc.format = inputColorDesc.format;
                outputDesc.msaaSamples = MSAASamples.None;
                outputDesc.useMipMap = false;
                outputDesc.autoGenerateMips = false;
                outputDesc.useDynamicScale = false;
                outputDesc.anisoLevel = 0;
                outputDesc.discardBuffer = false;
                outputDesc.enableRandomWrite = true;
                outputDesc.name = "_UpscalerOutput";
                outputDesc.clearBuffer = false;
                outputDesc.filterMode = FilterMode.Point;
                outputColor = renderGraph.CreateTexture(outputDesc);
            }
            
            TextureHandle inputOpaqueOnly = io.cameraColor;     // Ensure we always provide a valid opaque-only input, for those upscalers that need it
#if TND_URP
            // This requires Opaque Texture to be enabled in the URP asset's settings
            var resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle cameraOpaqueTexture = resourceData.cameraOpaqueTexture;
            if (cameraOpaqueTexture.IsValid())
            {
                // Validate that URP's opaque camera color matches the upscaler's input color texture
                var opaqueDesc = cameraOpaqueTexture.GetDescriptor(renderGraph);
                if (opaqueDesc.width == inputColorDesc.width && opaqueDesc.height == inputColorDesc.height)
                {
                    inputOpaqueOnly = cameraOpaqueTexture;
                }
            }
#endif

            using (var builder = renderGraph.AddUnsafePass(UpscalerDisplayName, out PassData passData))
            {
                passData.upscalerContext = upscalerContext;
                
                ref var init = ref passData.initParams;
                init.camera = Resources.EntityIdToObject(EntityId.FromULong(io.cameraInstanceID)) as Camera;
                init.useTextureArrays = io.enableTexArray;
                init.numTextureSlices = io.numActiveViews;
                init.maxRenderSize = upscalerOptions != null ? GetRenderResolution(io.postUpscaleResolution, upscalerOptions.QualityMode) : io.postUpscaleResolution;
                init.upscaleSize = io.postUpscaleResolution;
                init.enableHDR = io.hdrInput;
                init.invertedDepth = io.invertedDepth;
                init.highResMotionVectors = io.motionVectorTextureSize == io.postUpscaleResolution;
                init.jitteredMotionVectors = io.jitteredMotionVectors;
                
                float motionVectorSign = io.motionVectorDirection == UpscalingIO.MotionVectorDirection.PreviousFrameToCurrentFrame ? -1.0f : 1.0f;
                Vector2 motionVectorScale = io.motionVectorDomain == UpscalingIO.MotionVectorDomain.NDC ? new Vector2(io.motionVectorTextureSize.x, io.motionVectorTextureSize.y) : Vector2.one;
                
                ref var exec = ref passData.dispatchParams;
                exec.nonJitteredProjectionMatrix = io.projectionMatrices[0];    // Both URP and HDRP set the non-jittered projection matrices here
                exec.viewIndex = 0;
                
                // NOTE: io.preUpscaleResolution is sometimes off in HDRP, due to rounding errors in the dynamic resolution system, e.g. CeilToInt((((float)720 / 1080) * 100.0f) / 100.0f * 1080) = 721
                // This workaround makes use of the fact that we return a fixed render resolution in GetResolutionInfo, which means that the pre-upscale resolution is always predictable.
                exec.renderSize = upscalerOptions != null ? GetRenderResolution(io.postUpscaleResolution, upscalerOptions.QualityMode) : io.preUpscaleResolution;
                exec.motionVectorScale = motionVectorSign * motionVectorScale;
                exec.jitterOffset = io.subpixelJitter;
                exec.preExposure = io.preExposureValue;
                exec.resetHistory = io.resetHistory;
                if (upscalerOptions != null)
                {
                    exec.enableSharpening = upscalerOptions.EnableSharpening;
                    exec.sharpness = upscalerOptions.Sharpness;
                }
                else
                {
                    exec.enableSharpening = false;
                    exec.sharpness = 0.0f;
                }

                passData.inputColor = io.cameraColor;
                passData.inputDepth = io.cameraDepth;
                passData.inputMotionVectors = io.motionVectorColor;
                passData.inputExposure = io.exposureTexture;
                passData.inputReactiveMask = io.reactiveMask;
                passData.inputOpaqueColor = inputOpaqueOnly;
                passData.outputColor = outputColor;
                
                builder.UseTexture(passData.inputColor);
                builder.UseTexture(passData.inputDepth);
                builder.UseTexture(passData.inputMotionVectors);
                if (passData.inputExposure.IsValid()) builder.UseTexture(passData.inputExposure);
                if (passData.inputReactiveMask.IsValid()) builder.UseTexture(passData.inputReactiveMask);
                if (passData.inputOpaqueColor.IsValid()) builder.UseTexture(passData.inputOpaqueColor);
                builder.UseTexture(passData.outputColor, AccessFlags.Write);

                passData.isSRGB = GraphicsFormatUtility.IsSRGBFormat(inputColorDesc.format);
                
                builder.SetRenderFunc<PassData>(ExecutePass);
            }

            io.cameraColor = outputColor;
        }

        private static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            IUpscaler upscaler = data.upscalerContext.GetOrCreateUpscaler(cmd, data.initParams);
            if (upscaler == null)
                return;
            
            if (data.initParams.useTextureArrays)
                cmd.EnableKeyword(s_texArraysKeyword);
            else
                cmd.DisableKeyword(s_texArraysKeyword);
            
            if (data.isSRGB)
                cmd.EnableKeyword(s_srgbKeyword);
            else
                cmd.DisableKeyword(s_srgbKeyword);
            
            // Get the actual Texture objects from the texture handles and convert them to TextureRefs for the TND Upscaler framework
            ref var exec = ref data.dispatchParams;
            exec.inputColor = new TextureRef(data.inputColor);
            exec.inputDepth = new TextureRef(data.inputDepth);
            exec.inputMotionVectors = new TextureRef(data.inputMotionVectors);
            exec.inputExposure = new TextureRef(data.inputExposure);
            exec.inputReactiveMask = new TextureRef(data.inputReactiveMask);
            exec.inputOpaqueOnly = new TextureRef(data.inputOpaqueColor);
            exec.outputColor = new TextureRef(data.outputColor);
            
            upscaler.Dispatch(cmd, data.dispatchParams);
        }

        public override void CalculateJitter(int frameIndex, float upscaleRatio, out Vector2 jitter, out bool allowScaling)
        {
            jitter = UpscalerAdapterContext.CalculateDefaultJitter(frameIndex, upscaleRatio);
            allowScaling = false;
        }

        public override UpscalerResolutionInfo GetResolutionInfo(Vector2Int displayResolution, UpscalerOptions options)
        {
            if (options is not UpscalerAdapterOptions upscalerOptions)
            {
                return base.GetResolutionInfo(displayResolution, options);
            }

            Vector2Int maxResolution = GetRenderResolution(displayResolution, upscalerOptions.QualityMode);
            return UpscalerResolutionInfo.Fixed(maxResolution);
        }

        protected static float GetScaleFactor(UpscalerQuality qualityMode)
        {
            return qualityMode switch
            {
                UpscalerQuality.NativeAA => 1.0f,
                UpscalerQuality.UltraQuality => 1.2f,
                UpscalerQuality.Quality => 1.5f,
                UpscalerQuality.Balanced => 1.7f,
                UpscalerQuality.Performance => 2.0f,
                UpscalerQuality.UltraPerformance => 3.0f,
                _ => 1.0f,
            };
        }

        private static Vector2Int GetRenderResolution(Vector2Int displayResolution, UpscalerQuality qualityMode)
        {
            float scale = GetScaleFactor(qualityMode);
            return new Vector2Int(Mathf.CeilToInt(displayResolution.x / scale), Mathf.CeilToInt(displayResolution.y / scale));
        }
    }
}
