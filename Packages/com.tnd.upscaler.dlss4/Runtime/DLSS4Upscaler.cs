using UnityEngine;
using UnityEngine.Rendering;
using TND.Upscaling.Framework;
using DLSSUP = TND.Upscaling.DLSS4.DLSS4UnityPlugin;

namespace TND.Upscaling.DLSS4
{
    public class DLSS4Upscaler : UpscalerBase<DLSS4UpscalerSettings>
    {
        public static readonly string DisplayName = "DLSS 4.5";

        public DLSS4Upscaler(DLSS4UpscalerSettings settings)
            : base(settings)
        {
        }

        private DLSSUP.DLSS4Context _dlssContext;
        private UpscalerInitParams _initParams;
        private RenderTexture _intermediateOutput;
        
        public override bool Initialize(CommandBuffer commandBuffer, in UpscalerInitParams initParams)
        {
            _initParams = initParams;

            if (!DLSSUP.IsInitialized && !DLSSUP.InitializePlugin(Settings.advancedSettings.projectID))
            {
                return false;
            }
            
            DLSSUP.FeatureFlags flags = DLSSUP.FeatureFlags.AutoExposure;
            if (initParams.enableHDR) flags |= DLSSUP.FeatureFlags.IsHDR;
            if (initParams.invertedDepth) flags |= DLSSUP.FeatureFlags.DepthInverted;
            if (!initParams.highResMotionVectors) flags |= DLSSUP.FeatureFlags.MVLowRes;
            if (initParams.jitteredMotionVectors) flags |= DLSSUP.FeatureFlags.MVJittered;

            // Determine which render preset DLSS should use, based on the quality mode set
            float scaleRatio = (float)initParams.maxRenderSize.y / initParams.upscaleSize.y;
            DLSSUP.PerfQuality perfQuality = scaleRatio switch
            {
                // NativeAA and UltraQuality modes are not properly supported by DLSS and will cause errors, so those are intentionally omitted here.
                >= 0.66f => DLSSUP.PerfQuality.MaximumQuality,
                >= 0.58f => DLSSUP.PerfQuality.Balanced,
                >= 0.49f => DLSSUP.PerfQuality.MaximumPerformance,
                _ => DLSSUP.PerfQuality.UltraPerformance,
            };

            DLSSUP.InitializationData initData = new()
            {
                quality = perfQuality,
                inputWidth = (uint)initParams.maxRenderSize.x,
                inputHeight = (uint)initParams.maxRenderSize.y,
                outputWidth = (uint)initParams.upscaleSize.x,
                outputHeight = (uint)initParams.upscaleSize.y,
                flags = flags,
            };

            _dlssContext = DLSSUP.CreateFeature(commandBuffer, ref initData, Settings.renderPresets);
            return _dlssContext != null;
        }

        public override void Destroy(CommandBuffer commandBuffer)
        {
            if (_dlssContext != null)
            {
                DLSSUP.DestroyFeature(commandBuffer, _dlssContext);
                _dlssContext = null;
            }
            
            UpscalerUtils.DestroyRenderTexture(ref _intermediateOutput);

            base.Destroy(commandBuffer);
        }

        public override void Dispatch(CommandBuffer commandBuffer, in UpscalerDispatchParams dispatchParams)
        {
            if (_dlssContext == null)
            {
                return;
            }

            commandBuffer.BeginSample(DisplayName);
            
            bool requireIntermediate = dispatchParams.enableSharpening;
            if (requireIntermediate && _intermediateOutput == null)
            {
                _intermediateOutput = _initParams.CreateMatchingRenderTexture("_DLSSOutput", _initParams.upscaleSize, dispatchParams.outputColor.GraphicsFormat, true);
            }
            else if (!requireIntermediate && _intermediateOutput != null)
            {
                UpscalerUtils.DestroyRenderTexture(ref _intermediateOutput);
            }

            DLSSUP.ExecutionData execData = new()
            {
                reset = dispatchParams.resetHistory ? 1 : 0,
                frameTimeDelta = Time.unscaledDeltaTime * 1000.0f,
                mvScale = dispatchParams.motionVectorScale,
                jitterOffset = dispatchParams.jitterOffset,
                preExposure = dispatchParams.preExposure,
                subRectOffsetX = 0u,
                subRectOffsetY = 0u,
                subRectWidth = (uint)dispatchParams.renderSize.x,
                subRectHeight = (uint)dispatchParams.renderSize.y,
                invertXAxis = 0u,
                invertYAxis = 1u,
            };
            
            DLSSUP.TextureTable textureTable = new()
            {
                inputColor = dispatchParams.inputColor.GetTexture(commandBuffer),
                inputDepth = dispatchParams.inputDepth.GetTexture(commandBuffer),
                inputMotionVectors = dispatchParams.inputMotionVectors.GetTexture(commandBuffer),
                inputExposure = dispatchParams.inputExposure.GetTexture(commandBuffer),
                inputReactiveMask = dispatchParams.inputReactiveMask.GetTexture(commandBuffer),
                inputTransparencyMask = Settings.transparencyMask,
                outputColor = dispatchParams.enableSharpening ? _intermediateOutput : dispatchParams.outputColor.GetTexture(commandBuffer),
            };
            
            DLSSUP.ExecuteFeature(commandBuffer, _dlssContext, ref execData, textureTable);
            
            if (dispatchParams.enableSharpening)
            {
                SharpenPass(commandBuffer, _intermediateOutput, dispatchParams.outputColor.GetRenderTargetIdentifier(dispatchParams.viewIndex), dispatchParams.sharpness);
            }
            
            commandBuffer.EndSample(DisplayName);
        }
    }
}
