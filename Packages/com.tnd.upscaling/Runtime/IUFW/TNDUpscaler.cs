using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if TND_URP
using UnityEngine.Rendering.Universal;
#elif TND_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace TND.Upscaling.Framework.IUFW
{
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    static class RegisterUpscalerAdapter
    {
        static RegisterUpscalerAdapter() => UpscalerRegistry.Register<TNDUpscaler, UpscalerAdapterOptions>(UpscalerAdapter.UpscalerId, UpscalerAdapter.UpscalerDisplayName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitRuntime() => UpscalerRegistry.Register<TNDUpscaler, UpscalerAdapterOptions>(UpscalerAdapter.UpscalerId, UpscalerAdapter.UpscalerDisplayName);
    }
    
    public partial class TNDUpscaler: UpscalerAdapter
    {
        [AutoStaticsCleanup]
        private static TNDUpscaler s_instance;
        public static TNDUpscaler Instance => s_instance;

        [AutoStaticsCleanup]
        private static WeakReference<RenderPipeline> s_renderPipeline = new(null);

        [AutoStaticsCleanup]
        private static WeakReference<UnityEngine.Rendering.Upscaling> s_upscaling = new(null);

        private static UnityEngine.Rendering.Upscaling Upscaling
        {
            get
            {
                // Check if the render pipeline might have changed
                RenderPipeline currentPipeline = RenderPipelineManager.currentPipeline;
                if (!s_renderPipeline.TryGetTarget(out RenderPipeline pipeline) || pipeline != currentPipeline)
                {
                    // Reset cached values
                    s_renderPipeline.SetTarget(currentPipeline);
                    s_upscaling.SetTarget(null);
                    
                    if (currentPipeline == null)
                        return null;
                    
                    // Use reflection to get the Upscaling object from either a static (URP) or instance (HDRP) field on the render pipeline 
                    foreach (var bindingFlags in new[] { BindingFlags.NonPublic | BindingFlags.Static, BindingFlags.NonPublic | BindingFlags.Instance })
                    {
                        var upscalingField = currentPipeline.GetType().GetField("upscaling", bindingFlags);
                        if (upscalingField == null)
                            continue;

                        if (upscalingField.GetValue(currentPipeline) is UnityEngine.Rendering.Upscaling upscaling)
                        {
                            // Cache upscaling object reference for subsequent calls
                            s_upscaling.SetTarget(upscaling);
                            break;
                        }
                    }
                }

                return s_upscaling.TryGetTarget(out UnityEngine.Rendering.Upscaling result) ? result : null;
            }
        }

        public TNDUpscaler()
        {
            s_instance = this;
            
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
        }
        
        private void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            // Update cached values at the end of the frame, after Unity is done rendering every upscaling camera
            GetOptions(null).UpdateSettingsCaches();
            // Once Unity implements per-camera settings overrides, also call this for every camera that has its own options
        }
        
        private UpscalerAdapterOptions GetOptions(Camera camera)
        {
            // Once Unity implements it, this will need to call ResolveOptions when `camera` is not null.
            return Upscaling.GetGlobalOptions(this) as UpscalerAdapterOptions;
        }

        /// <summary>
        /// Sets the currently selected upscaler.
        /// Returns whether the upscaler was found and successfully set.
        /// This does not guarantee that the upscaler is supported or successfully initialized.
        /// </summary>
        public bool SetUpscaler(UpscalerName upscalerName, Camera camera = null)
        {
            return GetOptions(camera)?.SetPrimaryUpscaler(upscalerName) ?? false;
        }
        
        /// <summary>
        /// Gets the currently selected upscaler.
        /// This may differ from the currently active upscaler; if the selected upscaler is not supported then a different upscaler will be activated.
        /// </summary>
        public UpscalerName GetSelectedUpscaler(Camera camera = null)
        {
            return GetOptions(camera)?.GetPrimaryUpscalerName() ?? UpscalerName.None;
        }
        
        /// <summary>
        /// Gets the currently active upscaler, if any.
        /// This may differ from the currently selected upscaler; if the selected upscaler is not supported then a different upscaler will be activated.
        /// </summary>
        public UpscalerName GetActiveUpscaler(Camera camera)
        {
            var context = UpscalerAdapterContext.FindContextForCamera(camera);
            if (context == null)
                return UpscalerName.None;

            return context.ActiveUpscalerPlugin?.Name ?? UpscalerName.None;
        }
        
        /// <summary>
        /// Use this method to adjust the upscaler quality level.
        /// </summary>
        public void SetQuality(UpscalerQuality value, Camera camera = null)
        {
            var options = GetOptions(camera);
            if (options != null)
                options.QualityMode = value;
        }
        
        /// <summary>
        /// Returns the currently selected upscaler quality level.
        /// </summary>
        public UpscalerQuality GetQuality(Camera camera = null)
        {
            return GetOptions(camera)?.QualityMode ?? UpscalerQuality.Off;
        }
        
        /// <summary>
        /// Returns the scale factor (e.g. 1.5x, 2.0x, 3.0x) for the currently selected upscaler quality level.
        /// </summary>
        public float GetScaling(Camera camera = null)
        {
            return GetScaleFactor(GetQuality(camera));
        }
        
        /// <summary>
        /// Returns the render scale (e.g. 0.66x, 0.5x, 0.33x) for the currently selected upscaler quality level.
        /// </summary>
        public float GetRenderScale(Camera camera = null)
        {
            return 1.0f / GetScaling(camera);
        }
        
        /// <summary>
        /// Use this method to enable or disable sharpening.
        /// </summary>
        public void SetSharpening(bool value, Camera camera = null)
        {
            var options = GetOptions(camera);
            if (options != null)
                options.EnableSharpening = value;
        }
        
        /// <summary>
        /// Returns whether sharpening is currently enabled.
        /// </summary>
        public bool GetSharpening(Camera camera = null)
        {
            return GetOptions(camera)?.EnableSharpening ?? false;
        }
        
        /// <summary>
        /// Use this method to set the sharpening intensity.
        /// </summary>
        public void SetSharpness(float value, Camera camera = null)
        {
            var options = GetOptions(camera);
            if (options != null)
                options.Sharpness = value;
        }
        
        /// <summary>
        /// Returns the current sharpening intensity value.
        /// </summary>
        public float GetSharpness(Camera camera = null)
        {
            return GetOptions(camera)?.Sharpness ?? 0.0f;
        }
        
        /// <summary>
        /// Sets whether to enable or disable the automatic reactive mask generation feature. 
        /// </summary>
        public void SetAutoReactive(bool value, Camera camera = null)
        {
            // TODO: enableReactiveMaskPass is read-only >:(
            Debug.LogWarning("TNDUpscaler.SetAutoReactive is not implemented for the Unity upscaler framework.");
        }
        
        /// <summary>
        /// Returns whether automatic reactive mask generation is currently enabled.
        /// </summary>
        public bool GetAutoReactive(Camera camera = null)
        {
            return GetOptions(camera)?.enableReactiveMaskPass ?? false;
        }
        
        /// <summary>
        /// Sets where in the render pipeline upscaling should occur relative to post-processing.
        /// </summary>
        public void SetInjectionPoint(UpscalerInjectionPoint upscalerInjectionPoint, Camera camera = null)
        {
            var options = GetOptions(camera);
            if (options != null)
            {
                options.injectionPoint = upscalerInjectionPoint switch
                {
                    UpscalerInjectionPoint.BeforePostProcessing => DynamicResolutionHandler.UpsamplerScheduleType.BeforePost,
                    UpscalerInjectionPoint.AfterPostProcessing => DynamicResolutionHandler.UpsamplerScheduleType.AfterPost,
                    _ => DynamicResolutionHandler.UpsamplerScheduleType.BeforePost,
                };
            }
        }
        
        /// <summary>
        /// Returns where in the render pipeline upscaling occurs relative to post-processing.
        /// </summary>
        public UpscalerInjectionPoint GetInjectionPoint(Camera camera = null)
        {
            return GetOptions(camera)?.injectionPoint switch
            {
                DynamicResolutionHandler.UpsamplerScheduleType.BeforePost => UpscalerInjectionPoint.BeforePostProcessing,
                DynamicResolutionHandler.UpsamplerScheduleType.AfterPost => UpscalerInjectionPoint.AfterPostProcessing,
                _ => UpscalerInjectionPoint.BeforePostProcessing,
            };
        }
        
        /// <summary>
        /// Retrieve the upscaler-specific settings for the given upscaler.
        /// Returns null if the settings could not be found.
        /// </summary>
        public UpscalerSettingsBase GetUpscalerSettings(UpscalerName upscalerName, Camera camera = null)
        {
            IUpscalerPlugin upscalerPlugin = UpscalerPluginRegistry.FindUpscalerPlugin(upscalerName);
            if (GetOptions(camera)?.TryGetUpscalerSettings(upscalerPlugin, out UpscalerSettingsBase settings) ?? false)
            {
                return settings;
            }
        
            return null;
        }
        
        /// <summary>
        /// Retrieve the upscaler-specific settings for the given upscaler, cast to a specific type.
        /// Returns null if the settings could not be found.
        /// </summary>
        public TSettings GetUpscalerSettings<TSettings>(UpscalerName upscalerName, Camera camera = null) where TSettings : UpscalerSettingsBase
        {
            return GetUpscalerSettings(upscalerName, camera) as TSettings;
        }
        
        /// <summary>
        /// Use this method to Reset the camera for the next frame by clearing all buffers from previous frames, preventing visual artifacts.
        /// Use this on camera cuts, or when the camera teleports or moves abruptly.
        /// </summary>
        public void ResetCamera(Camera camera)
        {
        #if TND_URP
            if (camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                cameraData.resetHistory = true;
            }
        #elif TND_HDRP
            HDCamera.GetOrCreate(camera).Reset();
        #else
            Debug.LogWarning("TNDUpscaler.ResetCamera called on an unsupported Render Pipeline.");
        #endif
        }
        
        /// <summary>
        /// Use this method to retrieve a list of all currently supported upscalers.
        /// </summary>
        public static List<UpscalerName> GetSupported()
        {
            List<UpscalerName> supported = new();
            GetSupported(supported);
            return supported;
        }
        
        /// <summary>
        /// Use this method to retrieve a list of all currently supported upscalers.
        /// This method takes a pre-allocated list, so you can prevent unnecessary GC allocations. 
        /// </summary>
        public static void GetSupported(List<UpscalerName> supported)
        {
            ForEachUpscalerPlugin(upscalerPlugin =>
            {
                if (upscalerPlugin.IsSupported)
                {
                    supported.Add(upscalerPlugin.Name);
                }
            });
        }
        
        /// <summary>
        /// Returns if the given upscaler is available and supported on the current hardware.
        /// </summary>
        public static bool IsSupported(UpscalerName upscalerName)
        {
            bool isSupported = false;
            
            ForEachUpscalerPlugin(upscalerPlugin =>
            {
                if (upscalerPlugin.Name == upscalerName && upscalerPlugin.IsSupported)
                {
                    isSupported = true;
                }
            });
        
            return isSupported;
        }
        
        private static void ForEachUpscalerPlugin(Action<IUpscalerPlugin> callback)
        {
            var upscalerPlugins = UpscalerPluginRegistry.GetUpscalerPlugins();
            for (int i = 0; i < upscalerPlugins.Count; ++i)
            {
                callback(upscalerPlugins[i]);
            }
        }
    }
}
