using TND.Upscaling.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace TND.Upscaling.DLSS4
{
    public class DLSS4UpscalerPlugin: UpscalerPlugin<DLSS4Upscaler, DLSS4UpscalerSettings>
    {
        public override UpscalerName Name => UpscalerName.DLSS4;
        public override string DisplayName => DLSS4Upscaler.DisplayName;
        public override int Priority => (int)UpscalerName.DLSS4 + 45;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
        public override bool IsSupported => DLSS4UnityPlugin.IsSupported();
#else
        public override bool IsSupported => false;
#endif
        public override bool IsTemporalUpscaler => true;
        public override bool SupportsDynamicResolution => true;
        public override bool UsesMachineLearning => true;
        public override bool IncludesAlphaUpscale => true;
        public override bool AcceptsReactiveMask => true;
        public override GraphicsFormat ReactiveMaskFormat => GraphicsFormat.R8G8B8A8_UNorm; // TODO: see if we can use R8 for DX12 & Vulkan

        protected override bool TryCreateUpscaler(CommandBuffer commandBuffer, DLSS4UpscalerSettings settings, in UpscalerInitParams initParams, out DLSS4Upscaler upscaler)
        {
            upscaler = new DLSS4Upscaler(settings);
            return upscaler.Initialize(commandBuffer, initParams);
        }

        public override void Cleanup()
        {
            DLSS4UnityPlugin.ShutdownPlugin();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void RegisterUpscalerPlugin()
        {
            RegisterUpscalerPlugin<DLSS4UpscalerPlugin>();
        }
    }
}
