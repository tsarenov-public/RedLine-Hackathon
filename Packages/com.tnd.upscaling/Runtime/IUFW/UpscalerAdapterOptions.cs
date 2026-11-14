using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework.IUFW
{
    [Serializable]
    public class UpscalerAdapterOptions : UpscalerOptions
    {
        [SerializeField]
        internal List<string> upscalerChain = new();
        
        [SerializeField]
        private UpscalerQuality qualityMode = UpscalerQuality.Quality;

        [SerializeField]
        private bool enableSharpening = true;

        [SerializeField, Range(0.0f, 1.0f)]
        private float sharpness = 0.5f;
        
        [Serializable]
        internal struct UpscalerSettingsPair
        {
            public string identifier;
            public UpscalerSettingsBase settings;
        }

        [SerializeField]
        internal List<UpscalerSettingsPair> upscalerSettings = new();

        public UpscalerQuality QualityMode
        {
            get => qualityMode;
            set => qualityMode = value;
        }

        public bool EnableSharpening
        {
            get => enableSharpening;
            set => enableSharpening = value;
        }

        public float Sharpness
        {
            get => sharpness;
            set => sharpness = value;
        }
        
        internal UpscalerName GetPrimaryUpscalerName()
        {
            if (upscalerChain == null || upscalerChain.Count == 0)
                return UpscalerName.None;
            
            IUpscalerPlugin plugin = UpscalerPluginRegistry.FindUpscalerPlugin(upscalerChain[0]);
            return plugin?.Name ?? UpscalerName.None;
        }
        
        internal string GetPrimaryUpscalerIdentifier()
        {
            if (upscalerChain == null || upscalerChain.Count == 0)
                return null;

            return upscalerChain[0];
        }
        
        internal bool SetPrimaryUpscaler(UpscalerName upscalerName)
        {
            var upscalerPlugins = UpscalerPluginRegistry.GetUpscalerPlugins();
            for (int i = 0; i < upscalerPlugins.Count; ++i)
            {
                if (upscalerPlugins[i].Name == upscalerName)
                {
                    return SetPrimaryUpscaler(upscalerPlugins[i]);
                }
            }
            
            Debug.LogWarning($"Could not find upscaler with name: {upscalerName}");
            return false;
        }
        
        internal bool SetPrimaryUpscaler(IUpscalerPlugin upscalerPlugin)
        {
            if (upscalerPlugin == null)
                return false;

            string identifier = upscalerPlugin.Identifier;
            upscalerChain.Remove(identifier);
            upscalerChain.Insert(0, identifier);
            return true;
        }
        
        internal IEnumerator<IUpscalerPlugin> EnumerateUpscalerChain()
        {
            if (upscalerChain == null)
                yield break;
            
            for (int i = 0; i < upscalerChain.Count; ++i)
            {
                IUpscalerPlugin plugin = UpscalerPluginRegistry.FindUpscalerPlugin(upscalerChain[i]);
                if (plugin != null)
                    yield return plugin;
            }
        }
        
        internal bool TryGetUpscalerSettings(IUpscalerPlugin upscalerPlugin, out UpscalerSettingsBase settings)
        {
            if (upscalerPlugin == null)
            {
                settings = null;
                return false;
            }

            string identifier = upscalerPlugin.Identifier;
            for (int i = 0; i < upscalerSettings.Count; ++i)
            {
                if (upscalerSettings[i].identifier == identifier)
                {
                    settings = upscalerSettings[i].settings;
                    return true;
                }
            }

            settings = null;
            return false;
        }

        internal void UpdateSettingsCaches()
        {
            for (int i = 0; i < upscalerSettings.Count; ++i)
            {
                upscalerSettings[i].settings?.UpdateCachedValues();
            }
        }
    }
}
