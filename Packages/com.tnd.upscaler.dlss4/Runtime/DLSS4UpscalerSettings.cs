using System;
using UnityEngine;
using TND.Upscaling.Framework;

namespace TND.Upscaling.DLSS4
{
    [Serializable]
    public class DLSS4UpscalerSettings: UpscalerSettingsBase
    {
        public DLSS4UnityPlugin.RenderPresets renderPresets;
        
        public Texture transparencyMask;
        
        public AdvancedSettings advancedSettings;
        
        [Serializable]
        public struct AdvancedSettings
        {
            [Tooltip("Project ID provided by NVIDIA, used for validating the DLSS libraries. Leave this empty to use Unity's default Project ID.")]
            public string projectID;
        }

        private DLSS4UnityPlugin.RenderPresets _prevRenderPresets;

        public override bool RestartRequired()
        {
            return !renderPresets.ValueEquals(_prevRenderPresets);
        }

        public override void UpdateCachedValues()
        {
            _prevRenderPresets = renderPresets;
        }
    }
}
