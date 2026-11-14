using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace TND.Upscaling.DLSS4
{
    public static class DLSS4UnityPlugin
    {
        private const string LibraryName = "DLSSUnityPlugin";
        private const string DefaultProjectId = "231313132";

        public class DLSS4Context: IDisposable
        {
            public readonly uint featureSlot;
            public readonly NativeStruct<InitializationData> initData = new();
            public readonly NativeStruct<ExecutionData> execData = new();

            public DLSS4Context(uint featureSlot)
            {
                this.featureSlot = featureSlot;
            }

            public void Dispose()
            {
                initData.Dispose();
                execData.Dispose();
            }
        }

        private static int s_baseEventId = -1;

        public static bool IsInitialized => s_baseEventId >= 0;
        
        public static bool IsSupported()
        {
            try
            {
                if (!IsInitialized && !InitializePluginInternal())
                {
                    return false;
                }
                
                return IsFeatureAvailable(GraphicsDeviceFeature.DLSS);
            }
            catch (DllNotFoundException)
            {
                // This is a valid case on non-Windows platforms, so don't log an error here
                return false;
            }
        }

        public static bool InitializePlugin(string projectId = null)
        {
            if (IsInitialized)
                return true;
            
            try
            {
                if (string.IsNullOrWhiteSpace(projectId))
                {
                    projectId = DefaultProjectId;
                }

                return InitializePluginInternal(projectId);
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        private static bool InitializePluginInternal(string projectId = DefaultProjectId)
        {
            using InitDeviceContext initData = new(projectId, Application.unityVersion, Application.persistentDataPath);

            if (!Initialize(initData))
                return false;

            s_baseEventId = GetBaseEventId();
            return true;
        }

        public static void ShutdownPlugin()
        {
            if (!IsInitialized)
                return;
            
            s_baseEventId = -1;
            
            try
            {
                Shutdown();
            }
            catch (DllNotFoundException)
            {
                // In case someone tries to shut down the plugin after initialization fails due to a missing plugin.
                // We don't need to see the same error twice.
            }
        }

        public static DLSS4Context CreateFeature(CommandBuffer cmd, ref InitializationData initData, in RenderPresets renderPresets)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("DLSS4 plugin not initialized yet!");
                return null;
            }

            uint featureSlot = CreateFeatureSlot();
            if (featureSlot == uint.MaxValue)
                return null;

            SetRenderPresets(renderPresets);

            DLSS4Context dlssContext = new(featureSlot);
            initData.featureSlot = featureSlot;
            dlssContext.initData.SetData(ref initData);
            cmd.IssuePluginEventAndData(GetRenderEventCallback(), s_baseEventId + (int)PluginEvent.InitFeature, dlssContext.initData.GetPointer());
            
            return dlssContext;
        }

        public static void ExecuteFeature(CommandBuffer cmd, DLSS4Context dlssContext, ref ExecutionData execData, in TextureTable textures)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("DLSS4 plugin not initialized yet!");
                return;
            }

            uint featureSlot = dlssContext.featureSlot;
            if (featureSlot == uint.MaxValue)
            {
                Debug.LogWarning($"Invalid DLSS4 feature slot: {featureSlot}");
                return;
            }

            IntPtr textureCallback = GetSetTextureEventCallback();
            SetTexture(cmd, textureCallback, featureSlot, TextureSlot.ColorInput, textures.inputColor, true);
            SetTexture(cmd, textureCallback, featureSlot, TextureSlot.Depth, textures.inputDepth);
            SetTexture(cmd, textureCallback, featureSlot, TextureSlot.MotionVectors, textures.inputMotionVectors);
            SetTexture(cmd, textureCallback, featureSlot, TextureSlot.ExposureTexture, textures.inputExposure);
            SetTexture(cmd, textureCallback, featureSlot, TextureSlot.BiasColorMask, textures.inputReactiveMask);
            SetTexture(cmd, textureCallback, featureSlot, TextureSlot.TransparencyMask, textures.inputTransparencyMask);
            SetTexture(cmd, textureCallback, featureSlot, TextureSlot.ColorOutput, textures.outputColor);
            
            execData.featureSlot = featureSlot;
            dlssContext.execData.SetData(ref execData);
            cmd.IssuePluginEventAndData(GetRenderEventCallback(), s_baseEventId + (int)PluginEvent.Execute, dlssContext.execData.GetPointer());
        }

        public static void DestroyFeature(CommandBuffer cmd, DLSS4Context dlssContext)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("DLSS4 plugin not initialized yet!");
                return;
            }

            uint featureSlot = dlssContext.featureSlot;
            if (featureSlot == uint.MaxValue)
            {
                Debug.LogWarning($"Invalid DLSS4 feature slot: {featureSlot}");
                return;
            }
            
            cmd.IssuePluginEventAndData(GetRenderEventCallback(), s_baseEventId + (int)PluginEvent.DestroyFeature, (IntPtr)featureSlot);

            dlssContext.Dispose();
        }

        private static void SetTexture(CommandBuffer cmd, IntPtr callback, uint featureSlot, TextureSlot textureSlot, Texture texture, bool clearTextureTable = false)
        {
            if (texture == null)
                return;
            
            uint userData = ((featureSlot & 0xFFFF) << 16) | (((uint)textureSlot & 0xFFFF) << 1) | (clearTextureTable ? 1u : 0u);
            cmd.IssuePluginCustomTextureUpdateV2(callback, texture, userData);
        }

        [DllImport(LibraryName, EntryPoint = "DLSSUP_InitApi")]
        private static extern bool Initialize(in InitDeviceContext initData);

        [DllImport(LibraryName, EntryPoint = "DLSSUP_ShutdownApi")]
        private static extern void Shutdown();

        [DllImport(LibraryName, EntryPoint = "DLSSUP_IsFeatureAvailable")]
        private static extern bool IsFeatureAvailable(GraphicsDeviceFeature feature);

        [DllImport(LibraryName, EntryPoint = "DLSSUP_GetRenderEventCallback")]
        private static extern IntPtr GetRenderEventCallback();

        [DllImport(LibraryName, EntryPoint = "DLSSUP_GetSetTextureEventCallback")]
        private static extern IntPtr GetSetTextureEventCallback();

        [DllImport(LibraryName, EntryPoint = "DLSSUP_SetRenderPresets")]
        private static extern void SetRenderPresets(in RenderPresets renderPresets);

        [DllImport(LibraryName, EntryPoint = "DLSSUP_CreateFeatureSlot")]
        private static extern uint CreateFeatureSlot();

        [DllImport(LibraryName, EntryPoint = "DLSSUP_GetBaseEventId")]
        private static extern int GetBaseEventId();
        
        private enum GraphicsDeviceFeature
        {
            DLSS,
        }

        private enum PluginEvent
        {
            DestroyFeature,
            Execute,
            InitFeature,
        }

        [Flags]
        public enum FeatureFlags
        {
            None = 0,
            IsHDR = 1 << 0,
            MVLowRes = 1 << 1,
            MVJittered = 1 << 2,
            DepthInverted = 1 << 3,
            DoSharpening = 1 << 4,  // Deprecated
            AutoExposure = 1 << 6,
            AlphaUpscaling = 1 << 7,
        }

        public enum PerfQuality
        {
            MaximumPerformance,
            Balanced,
            MaximumQuality,
            UltraPerformance,
            UltraQuality,
            NativeAA,
        }

        public enum RenderPreset
        {
            Default = 0,
            [InspectorName("Preset F (CNN)")]
            PresetF = 6,    // The default preset for Ultra Performance mode.
            [InspectorName("Preset J (Transformer)")]
            PresetJ = 10,   // Similar to preset K. Preset J might exhibit slightly less ghosting at the cost of extra flickering.
            [InspectorName("Preset K (Transformer)")]
            PresetK = 11,   // Default preset for DLAA/Balanced/Quality modes. Less expensive performance wise compared to Preset L.
            [InspectorName("Preset L (Transformer)")]
            PresetL = 12,   // Default preset for UltraPerformance mode. Delivers a sharper, more stable image with less ghosting than Preset J, K but are more expensive performance wise.
            [InspectorName("Preset M (Transformer)")]
            PresetM = 13,   // Default preset for Performance mode. Delivers similar image quality improvements as Preset L but closer in speed to Presets J, K.
        }

        private enum TextureSlot
        {
            ColorInput,
            ColorOutput,
            Depth,
            MotionVectors,
            TransparencyMask,
            ExposureTexture,
            BiasColorMask,
        }
        
        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct InitDeviceContext: IDisposable
        {
            public IntPtr projectId;
            public IntPtr engineVersion;
            public IntPtr appDir;

            public InitDeviceContext(string projectId, string engineVersion, string appDir)
            {
                this.projectId = Marshal.StringToHGlobalUni(projectId);
                this.engineVersion = Marshal.StringToHGlobalUni(engineVersion);
                this.appDir = Marshal.StringToHGlobalUni(appDir);
            }

            public void Dispose()
            {
                Marshal.FreeHGlobal(projectId);
                Marshal.FreeHGlobal(engineVersion);
                Marshal.FreeHGlobal(appDir);
            }
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct RenderPresets
        {
            public RenderPreset dlaa;
            public RenderPreset ultraQuality;
            public RenderPreset quality;
            public RenderPreset balanced;
            public RenderPreset performance;
            public RenderPreset ultraPerformance;

            public bool ValueEquals(in RenderPresets other)
            {
                return dlaa == other.dlaa &&
                       ultraQuality == other.ultraQuality &&
                       quality == other.quality &&
                       balanced == other.balanced &&
                       performance == other.performance &&
                       ultraPerformance == other.ultraPerformance;
            }
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct InitializationData
        {
            public uint inputWidth;
            public uint inputHeight;
            public uint outputWidth;
            public uint outputHeight;
            public PerfQuality quality;     // Used to select render preset
            public FeatureFlags flags;

            public uint featureSlot;
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct ExecutionData
        {
            public int reset;
            public float frameTimeDelta;
            public Vector2 mvScale;
            public Vector2 jitterOffset;
            public float preExposure;
            public uint subRectOffsetX;
            public uint subRectOffsetY;
            public uint subRectWidth;
            public uint subRectHeight;
            public uint invertXAxis;
            public uint invertYAxis;
            
            public uint featureSlot;
        }

        public struct TextureTable
        {
            public Texture inputColor;
            public Texture inputDepth;
            public Texture inputMotionVectors;
            public Texture inputExposure;
            public Texture inputReactiveMask;
            public Texture inputTransparencyMask;
            public Texture outputColor;
        }
    }
}
