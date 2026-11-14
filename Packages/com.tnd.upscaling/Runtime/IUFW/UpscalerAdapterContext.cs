using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TND.Upscaling.Framework.IUFW
{
    public partial class UpscalerAdapterContext : IUpscalerContext
    {
        [AutoStaticsCleanup]
        private static readonly HashSet<UpscalerAdapterContext> Contexts = new();
        
        private readonly UpscalerAdapterOptions _options;
        private readonly UpscalerQuality _createdWithQuality;

        private EntityId _associatedCameraId;
        private IUpscaler _activeUpscaler;
        private IUpscalerPlugin _activeUpscalerPlugin;
        private string _selectedUpscalerIdentifier;
        
        public Vector2Int createdForDisplayResolution { get; }
        public int lastUsedFrame { get; set; }

        public IUpscalerPlugin ActiveUpscalerPlugin => _activeUpscalerPlugin;

        public UpscalerAdapterContext(UpscalerAdapterOptions options, Vector2Int displayResolution)
        {
            _options = options;
            createdForDisplayResolution = displayResolution;
            _createdWithQuality = options.QualityMode;
            
            Contexts.Add(this);
        }

        public IUpscaler GetOrCreateUpscaler(CommandBuffer cmd, in UpscalerInitParams initParams)
        {
            if (_activeUpscaler == null)
            {
                IEnumerator<IUpscalerPlugin> fallbackChain = _options.EnumerateUpscalerChain();
                
                // Pick the appropriate upscaler plugin based on the selection in the options
                _selectedUpscalerIdentifier = _options.GetPrimaryUpscalerIdentifier();
                IUpscalerPlugin upscalerPlugin = UpscalerPluginRegistry.FindUpscalerPlugin(_selectedUpscalerIdentifier);
                
                while (true)
                {
                    if (upscalerPlugin != null && upscalerPlugin.IsSupported)
                    {
                        _options.TryGetUpscalerSettings(upscalerPlugin, out var upscalerSettings);
                        if (upscalerPlugin.TryCreateUpscaler(cmd, upscalerSettings ?? upscalerPlugin.CreateSettings(), initParams, out _activeUpscaler))
                        {
                            _activeUpscalerPlugin = upscalerPlugin;
                            UpscalerDebug.Log($"Initialized upscaler plugin of type: {_activeUpscalerPlugin.DisplayName}");
                            break;
                        }
                    }

                    // Go through the fallback chain in order until we find an upscaler that works
                    if (!fallbackChain.MoveNext())
                    {
                        _activeUpscaler = null;
                        _activeUpscalerPlugin = null;
                        break;
                    }

                    upscalerPlugin = fallbackChain.Current;
                }

                if (initParams.camera != null)
                {
                    _associatedCameraId = initParams.camera.GetEntityId();
                }
            }

            return _activeUpscaler;
        }
        
        public bool IsValidForOptions(UpscalerOptions options)
        {
            // Check if the provided upscaler options object is valid
            if (options is not UpscalerAdapterOptions upscalerOptions)
                return false;

            // Check if the selected quality mode has changed
            if (upscalerOptions.QualityMode != _createdWithQuality)
                return false;

            // Check if the selected upscaler has changed
            if (upscalerOptions.GetPrimaryUpscalerIdentifier() != _selectedUpscalerIdentifier)
                return false;

            // Check if settings for the currently active upscaler have changed significantly
            if (_activeUpscaler != null && _activeUpscaler.RestartRequired())
                return false;
            
            return true;
        }

        public void Cleanup(CommandBuffer cmd)
        {
            if (_activeUpscaler != null)
            {
                _activeUpscaler.Destroy(cmd);
                _activeUpscaler = null;
            }

            Contexts.Remove(this);
        }

        public static UpscalerAdapterContext FindContextForCamera(Camera camera)
        {
            if (camera == null)
                return null;

            EntityId cameraId = camera.GetEntityId();
            
            foreach (var context in Contexts)
            {
                if (context._associatedCameraId == cameraId)
                    return context;
            }

            return null;
        }
        
        public Vector2 CalculateJitter(int frameIndex, float upscaleRatio)
        {
            if (_activeUpscalerPlugin != null && !_activeUpscalerPlugin.IsTemporalUpscaler)
            {
                return Vector2.zero;
            }
            
            if (_activeUpscaler != null)
            {
                const int upscaleWidth = 3840;
                int renderWidth = Mathf.CeilToInt(upscaleWidth / upscaleRatio);
                return _activeUpscaler.GetJitterOffset(frameIndex, renderWidth, upscaleWidth);
            }

            // Fall back to a standard jitter pattern
            return CalculateDefaultJitter(frameIndex, upscaleRatio);
        }

        public static Vector2 CalculateDefaultJitter(int frameIndex, float upscaleRatio)
        {
            int jitterPhaseCount = GetJitterPhaseCount(upscaleRatio);
            GetJitterOffset(out float jitterX, out float jitterY, frameIndex, jitterPhaseCount);
            return new Vector2(jitterX, jitterY);
        }
        
        private static int GetJitterPhaseCount(float upscaleRatio)
        {
            const float basePhaseCount = 8.0f;
            return (int)(basePhaseCount * upscaleRatio * upscaleRatio);
        }

        private static void GetJitterOffset(out float outX, out float outY, int index, int phaseCount)
        {
            phaseCount = phaseCount > 0 ? phaseCount : 1;
            outX = HaltonSequence.Get((index % phaseCount) + 1, 2) - 0.5f;
            outY = HaltonSequence.Get((index % phaseCount) + 1, 3) - 0.5f;
        }
    }
}
