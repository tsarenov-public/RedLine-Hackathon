
Shader "NatureManufacture Shaders/URP Cross Road Material"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		_TextureSample1("Second Road Noise Mask", 2D) = "white" {}
		_SecondRoadNoiseMaskPower("Second Road Noise Mask Power", Range( 0 , 10)) = 0.1
		_SecondRoadNoiseMaskTreshold("Second Road Noise Mask Treshold", Range( 0 , 10)) = 1
		_MainRoadColor("Main Road Color", Color) = (1,1,1,1)
		_MainRoadBrightness("Main Road Brightness", Float) = 1
		_MainTex("Main Road Albedo_T", 2D) = "white" {}
		[Toggle]_MainRoadUV3("Main Road UV3", Float) = 0
		_MainRoadAlphaCutOut("Main Road Alpha CutOut", Range( 0 , 2)) = 1
		_BumpMap("Main Road Normal", 2D) = "bump" {}
		_BumpScale("Main Road BumpScale", Range( 0 , 5)) = 0
		_MetalicRAmbientOcclusionGHeightBEmissionA("Main Road Metallic (R) Ambient Occlusion (G) Height (B) Smoothness (A)", 2D) = "white" {}
		_MainRoadMetalicPower("Main Road Metalic Power", Range( 0 , 2)) = 0
		_MainRoadAmbientOcclusionPower("Main Road Ambient Occlusion Power", Range( 0 , 1)) = 1
		_MainRoadSmoothnessPower("Main Road Smoothness Power", Range( 0 , 2)) = 1
		_SecondRoadColor("Second Road Color", Color) = (1,1,1,1)
		_SecondRoadBrightness("Second Road Brightness", Float) = 1
		_TextureSample3("Second Road Albedo_T", 2D) = "white" {}
		[Toggle]_SecondRoadUV3("Second Road UV3", Float) = 0
		[Toggle(_IGNORESECONDROADALPHA_ON)] _IgnoreSecondRoadAlpha("Ignore Second Road Alpha", Float) = 0
		_SecondRoadAlphaCutOut("Second Road Alpha CutOut", Range( 0 , 2)) = 1
		_SecondRoadNormal("Second Road Normal", 2D) = "bump" {}
		_SecondRoadNormalScale("Second Road Normal Scale", Range( 0 , 5)) = 0
		_SecondRoadNormalBlend("Second Road Normal Blend", Range( 0 , 1)) = 0.8
		_SecondRoadMetallicRAmbientocclusionGHeightBSmoothnessA("Second Road Metallic (R) Ambient occlusion (G) Height (B) Smoothness (A)", 2D) = "white" {}
		_SecondRoadMetalicPower("Second Road Metalic Power", Range( 0 , 2)) = 1
		_SecondRoadAmbientOcclusionPower("Second Road Ambient Occlusion Power", Range( 0 , 1)) = 1
		_SecondRoadSmoothnessPower("Second Road Smoothness Power", Range( 0 , 2)) = 1
		_CrossRoadColor("Cross Road Color", Color) = (1,1,1,1)
		_CrossRoadBrightness("Cross Road Brightness", Float) = 1
		_TextureSample4("Cross Road Albedo_T", 2D) = "white" {}
		[Toggle]_CrossRoadUV3("Cross Road UV3", Float) = 0
		[Toggle(_IGNORECROSSROADALPHA_ON)] _IgnoreCrossRoadAlpha("Ignore Cross Road Alpha", Float) = 0
		_CrossRoadAlphaCutOut("Cross Road Alpha CutOut", Range( 0 , 2)) = 1
		_CrossRoadNormal("Cross Road Normal", 2D) = "bump" {}
		_CrossRoadNormalScale("Cross Road Normal Scale", Range( 0 , 5)) = 0
		_CrossRoadMetallicRAmbientocclusionGHeightBSmoothnessA("Cross Road Metallic (R) Ambient occlusion (G) Height (B) Smoothness (A)", 2D) = "white" {}
		_CrossRoadMetallicPower("Cross Road Metallic Power", Range( 0 , 2)) = 1
		_CrossRoadAmbientOcclusionPower("Cross Road Ambient Occlusion Power", Range( 0 , 1)) = 1
		_CrossRoadSmoothnessPower("Cross Road Smoothness Power", Range( 0 , 2)) = 1
		_DetailMask("DetailMask (A)", 2D) = "white" {}
		_DetailAlbedoMap("DetailAlbedoMap", 2D) = "black" {}
		_DetailAlbedoPower("Main Road Detail Albedo Power", Range( 0 , 2)) = 0
		_Float3("Cross Road Detail Albedo Power", Range( 0 , 2)) = 2
		_Float2("Second Road Detail Albedo Power", Range( 0 , 2)) = 0
		_DetailNormalMap("DetailNormalMap", 2D) = "bump" {}
		_DetailNormalMapScale("Main Road DetailNormalMapScale", Range( 0 , 5)) = 0
		_Float0("Cross Road Detail NormalMap Scale", Range( 0 , 5)) = 0
		_Float1("Second Road DetailNormalMapScale", Range( 0 , 5)) = 0

		[Header(Terrain Z Fighting Offset)]
		_OffsetFactor ("Offset Factor", Range(-10.0, 0.0)) = 0
		_OffsetUnit ("Offset Unit", Range(-10.0, 0.0)) = 0
	}

	SubShader
	{
		LOD 0

		
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
		
		Cull Back
		HLSLINCLUDE
		#pragma target 3.0
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }
			
			Blend One Zero , One Zero
			ZWrite On
			ZTest LEqual
			Offset [_OffsetFactor],[_OffsetUnit]
			ColorMask RGBA
			

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 999999

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_FORWARD

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			
			#if ASE_SRP_VERSION <= 70108
			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
			#endif

			#pragma shader_feature _IGNORESECONDROADALPHA_ON
			#pragma shader_feature _IGNORECROSSROADALPHA_ON


			sampler2D _MainTex;
			sampler2D _BumpMap;
			sampler2D _DetailAlbedoMap;
			sampler2D _DetailMask;
			sampler2D _TextureSample3;
			sampler2D _SecondRoadNormal;
			sampler2D _TextureSample1;
			sampler2D _TextureSample4;
			sampler2D _CrossRoadNormal;
			sampler2D _DetailNormalMap;
			sampler2D _MetalicRAmbientOcclusionGHeightBEmissionA;
			sampler2D _SecondRoadMetallicRAmbientocclusionGHeightBSmoothnessA;
			sampler2D _CrossRoadMetallicRAmbientocclusionGHeightBSmoothnessA;
			CBUFFER_START( UnityPerMaterial )
			float _MainRoadBrightness;
			float _MainRoadUV3;
			float4 _MainTex_ST;
			float4 _BumpMap_ST;
			float4 _MainRoadColor;
			float _DetailAlbedoPower;
			float4 _DetailAlbedoMap_ST;
			float4 _DetailMask_ST;
			float _SecondRoadBrightness;
			float _SecondRoadUV3;
			float4 _TextureSample3_ST;
			float4 _SecondRoadNormal_ST;
			float4 _SecondRoadColor;
			float _Float2;
			float4 _TextureSample1_ST;
			float _SecondRoadNoiseMaskPower;
			float _SecondRoadNoiseMaskTreshold;
			float _CrossRoadBrightness;
			float _CrossRoadUV3;
			float4 _TextureSample4_ST;
			float4 _CrossRoadNormal_ST;
			float4 _CrossRoadColor;
			float _Float3;
			float _BumpScale;
			float _DetailNormalMapScale;
			float _SecondRoadNormalScale;
			float _SecondRoadNormalBlend;
			float _Float1;
			float _CrossRoadNormalScale;
			float _Float0;
			float _MainRoadMetalicPower;
			float _SecondRoadMetalicPower;
			float _CrossRoadMetallicPower;
			float _MainRoadSmoothnessPower;
			float _SecondRoadSmoothnessPower;
			float _CrossRoadSmoothnessPower;
			float _MainRoadAmbientOcclusionPower;
			float _SecondRoadAmbientOcclusionPower;
			float _CrossRoadAmbientOcclusionPower;
			float _MainRoadAlphaCutOut;
			float _SecondRoadAlphaCutOut;
			float _CrossRoadAlphaCutOut;
			CBUFFER_END


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_tangent : TANGENT;
				float4 texcoord1 : TEXCOORD1;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 lightmapUVOrVertexSH : TEXCOORD0;
				half4 fogFactorAndVertexLight : TEXCOORD1;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord : TEXCOORD2;
				#endif
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
				#if defined(ASE_NEEDS_FRAG_SCREEN_POSITION)
				float4 screenPos : TEXCOORD6;
				#endif
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			VertexOutput vert ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.ase_texcoord7.xy = v.ase_texcoord.xy;
				o.ase_texcoord7.zw = v.ase_texcoord2.xy;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif
				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float3 positionVS = TransformWorldToView( positionWS );
				float4 positionCS = TransformWorldToHClip( positionWS );

				VertexNormalInputs normalInput = GetVertexNormalInputs( v.ase_normal, v.ase_tangent );

				o.tSpace0 = float4( normalInput.normalWS, positionWS.x);
				o.tSpace1 = float4( normalInput.tangentWS, positionWS.y);
				o.tSpace2 = float4( normalInput.bitangentWS, positionWS.z);

				OUTPUT_LIGHTMAP_UV( v.texcoord1, unity_LightmapST, o.lightmapUVOrVertexSH.xy );
				OUTPUT_SH( normalInput.normalWS.xyz, o.lightmapUVOrVertexSH.xyz );

				half3 vertexLight = VertexLighting( positionWS, normalInput.normalWS );
				#ifdef ASE_FOG
					half fogFactor = ComputeFogFactor( positionCS.z );
				#else
					half fogFactor = 0;
				#endif
				o.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
				
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				
				o.clipPos = positionCS;
				#if defined(ASE_NEEDS_FRAG_SCREEN_POSITION)
				o.screenPos = ComputeScreenPos(positionCS);
				#endif
				return o;
			}

			half4 frag ( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif

				float3 WorldNormal = normalize( IN.tSpace0.xyz );
				float3 WorldTangent = IN.tSpace1.xyz;
				float3 WorldBiTangent = IN.tSpace2.xyz;
				float3 WorldPosition = float3(IN.tSpace0.w,IN.tSpace1.w,IN.tSpace2.w);
				float3 WorldViewDirection = _WorldSpaceCameraPos.xyz  - WorldPosition;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				#if defined(ASE_NEEDS_FRAG_SCREEN_POSITION)
				float4 ScreenPos = IN.screenPos;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = IN.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#endif
	
				#if SHADER_HINT_NICE_QUALITY
					WorldViewDirection = SafeNormalize( WorldViewDirection );
				#endif

				float2 uv0_MainTex = IN.ase_texcoord7.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 uv2_BumpMap = IN.ase_texcoord7.zw * _BumpMap_ST.xy + _BumpMap_ST.zw;
				float4 tex2DNode1 = tex2D( _MainTex, (( _MainRoadUV3 )?( uv2_BumpMap ):( uv0_MainTex )) );
				float4 temp_output_77_0 = ( ( _MainRoadBrightness * tex2DNode1 ) * _MainRoadColor );
				float2 uv0_DetailAlbedoMap = IN.ase_texcoord7.xy * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
				float4 tex2DNode486 = tex2D( _DetailAlbedoMap, uv0_DetailAlbedoMap );
				float4 blendOpSrc474 = temp_output_77_0;
				float4 blendOpDest474 = ( _DetailAlbedoPower * tex2DNode486 );
				float2 uv0_DetailMask = IN.ase_texcoord7.xy * _DetailMask_ST.xy + _DetailMask_ST.zw;
				float4 tex2DNode481 = tex2D( _DetailMask, uv0_DetailMask );
				float4 lerpResult480 = lerp( temp_output_77_0 , (( blendOpDest474 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest474 ) * ( 1.0 - blendOpSrc474 ) ) : ( 2.0 * blendOpDest474 * blendOpSrc474 ) ) , ( _DetailAlbedoPower * tex2DNode481.a ));
				float2 uv0_TextureSample3 = IN.ase_texcoord7.xy * _TextureSample3_ST.xy + _TextureSample3_ST.zw;
				float2 uv2_SecondRoadNormal = IN.ase_texcoord7.zw * _SecondRoadNormal_ST.xy + _SecondRoadNormal_ST.zw;
				float4 tex2DNode537 = tex2D( _TextureSample3, (( _SecondRoadUV3 )?( uv2_SecondRoadNormal ):( uv0_TextureSample3 )) );
				float4 temp_output_540_0 = ( ( _SecondRoadBrightness * tex2DNode537 ) * _SecondRoadColor );
				float4 blendOpSrc619 = temp_output_540_0;
				float4 blendOpDest619 = ( tex2DNode486 * _Float2 );
				float4 lerpResult618 = lerp( temp_output_540_0 , (( blendOpDest619 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest619 ) * ( 1.0 - blendOpSrc619 ) ) : ( 2.0 * blendOpDest619 * blendOpSrc619 ) ) , ( _Float2 * tex2DNode481.a ));
				float4 break666 = ( IN.ase_color / float4( 1,1,1,1 ) );
				float2 uv0_TextureSample1 = IN.ase_texcoord7.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float temp_output_682_0 = min( min( tex2D( _TextureSample1, uv0_TextureSample1 ).r , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.4,0.4 ) ) ).r ) , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.2,0.2 ) ) ).r );
				float clampResult673 = clamp( pow( abs( ( temp_output_682_0 * _SecondRoadNoiseMaskPower ) ) , abs( _SecondRoadNoiseMaskTreshold ) ) , 0.0 , 1.0 );
				float4 appendResult665 = (float4(( ( 1.0 - break666.r ) - clampResult673 ) , ( 1.0 - break666.g ) , break666.b , break666.a));
				float4 clampResult672 = clamp( appendResult665 , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
				float4 break496 = ( 1.0 - clampResult672 );
				float4 lerpResult592 = lerp( lerpResult480 , lerpResult618 , break496.x);
				float2 uv0_TextureSample4 = IN.ase_texcoord7.xy * _TextureSample4_ST.xy + _TextureSample4_ST.zw;
				float2 uv2_CrossRoadNormal = IN.ase_texcoord7.zw * _CrossRoadNormal_ST.xy + _CrossRoadNormal_ST.zw;
				float4 tex2DNode638 = tex2D( _TextureSample4, (( _CrossRoadUV3 )?( uv2_CrossRoadNormal ):( uv0_TextureSample4 )) );
				float4 temp_output_654_0 = ( ( _CrossRoadBrightness * tex2DNode638 ) * _CrossRoadColor );
				float4 blendOpSrc652 = temp_output_654_0;
				float4 blendOpDest652 = tex2DNode486;
				float4 lerpResult653 = lerp( temp_output_654_0 , (( blendOpDest652 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest652 ) * ( 1.0 - blendOpSrc652 ) ) : ( 2.0 * blendOpDest652 * blendOpSrc652 ) ) , ( tex2DNode481.a * _Float3 ));
				float4 lerpResult852 = lerp( lerpResult653 , lerpResult618 , break496.x);
				float4 lerpResult644 = lerp( lerpResult592 , lerpResult852 , break496.y);
				
				float3 tex2DNode4 = UnpackNormalScale( tex2D( _BumpMap, (( _MainRoadUV3 )?( uv2_BumpMap ):( uv0_MainTex )) ), _BumpScale );
				float3 lerpResult479 = lerp( tex2DNode4 , BlendNormal( tex2DNode4 , UnpackNormalScale( tex2D( _DetailNormalMap, uv0_DetailAlbedoMap ), _DetailNormalMapScale ) ) , tex2DNode481.a);
				float3 tex2DNode535 = UnpackNormalScale( tex2D( _SecondRoadNormal, (( _SecondRoadUV3 )?( uv2_SecondRoadNormal ):( uv0_TextureSample3 )) ), _SecondRoadNormalScale );
				float3 lerpResult570 = lerp( lerpResult479 , tex2DNode535 , _SecondRoadNormalBlend);
				float3 lerpResult617 = lerp( tex2DNode535 , BlendNormal( lerpResult570 , UnpackNormalScale( tex2D( _DetailNormalMap, uv0_DetailAlbedoMap ), _Float1 ) ) , tex2DNode481.a);
				float3 lerpResult593 = lerp( lerpResult479 , lerpResult617 , break496.x);
				float3 tex2DNode637 = UnpackNormalScale( tex2D( _CrossRoadNormal, (( _CrossRoadUV3 )?( uv2_CrossRoadNormal ):( uv0_TextureSample4 )) ), _CrossRoadNormalScale );
				float3 lerpResult647 = lerp( tex2DNode637 , BlendNormal( tex2DNode637 , UnpackNormalScale( tex2D( _DetailNormalMap, uv0_DetailAlbedoMap ), _Float0 ) ) , tex2DNode481.a);
				float3 lerpResult848 = lerp( lerpResult647 , lerpResult617 , break496.x);
				float3 lerpResult640 = lerp( lerpResult593 , lerpResult848 , break496.y);
				
				float4 tex2DNode2 = tex2D( _MetalicRAmbientOcclusionGHeightBEmissionA, (( _MainRoadUV3 )?( uv2_BumpMap ):( uv0_MainTex )) );
				float4 tex2DNode536 = tex2D( _SecondRoadMetallicRAmbientocclusionGHeightBSmoothnessA, (( _SecondRoadUV3 )?( uv2_SecondRoadNormal ):( uv0_TextureSample3 )) );
				float temp_output_547_0 = ( tex2DNode536.r * _SecondRoadMetalicPower );
				float lerpResult601 = lerp( ( tex2DNode2.r * _MainRoadMetalicPower ) , temp_output_547_0 , break496.x);
				float4 tex2DNode639 = tex2D( _CrossRoadMetallicRAmbientocclusionGHeightBSmoothnessA, (( _CrossRoadUV3 )?( uv2_CrossRoadNormal ):( uv0_TextureSample4 )) );
				float lerpResult850 = lerp( ( _CrossRoadMetallicPower * tex2DNode639.r ) , temp_output_547_0 , 0.0);
				float lerpResult643 = lerp( lerpResult601 , lerpResult850 , break496.y);
				
				float temp_output_548_0 = ( _SecondRoadSmoothnessPower * tex2DNode536.a );
				float lerpResult594 = lerp( ( tex2DNode2.a * _MainRoadSmoothnessPower ) , temp_output_548_0 , break496.x);
				float lerpResult847 = lerp( ( tex2DNode639.a * _CrossRoadSmoothnessPower ) , temp_output_548_0 , break496.x);
				float lerpResult645 = lerp( lerpResult594 , lerpResult847 , break496.y);
				
				float clampResult96 = clamp( tex2DNode2.g , ( 1.0 - _MainRoadAmbientOcclusionPower ) , 1.0 );
				float clampResult546 = clamp( tex2DNode536.g , ( 1.0 - _SecondRoadAmbientOcclusionPower ) , 1.0 );
				float lerpResult602 = lerp( clampResult96 , clampResult546 , break496.x);
				float clampResult662 = clamp( tex2DNode639.g , ( 1.0 - _CrossRoadAmbientOcclusionPower ) , 1.0 );
				float lerpResult851 = lerp( clampResult662 , clampResult546 , break496.x);
				float lerpResult642 = lerp( lerpResult602 , lerpResult851 , break496.y);
				
				float temp_output_629_0 = ( tex2DNode1.a * _MainRoadAlphaCutOut );
				#ifdef _IGNORESECONDROADALPHA_ON
				float staticSwitch693 = temp_output_629_0;
				#else
				float staticSwitch693 = ( tex2DNode537.a * _SecondRoadAlphaCutOut );
				#endif
				float lerpResult628 = lerp( temp_output_629_0 , staticSwitch693 , break496.x);
				#ifdef _IGNORECROSSROADALPHA_ON
				float staticSwitch696 = lerpResult628;
				#else
				float staticSwitch696 = ( tex2DNode638.a * _CrossRoadAlphaCutOut );
				#endif
				float lerpResult849 = lerp( staticSwitch696 , staticSwitch693 , break496.x);
				float lerpResult641 = lerp( lerpResult628 , lerpResult849 , break496.y);
				
				float3 Albedo = lerpResult644.rgb;
				float3 Normal = lerpResult640;
				float3 Emission = 0;
				float3 Specular = 0.5;
				float Metallic = lerpResult643;
				float Smoothness = lerpResult645;
				float Occlusion = lerpResult642;
				float Alpha = lerpResult641;
				float AlphaClipThreshold = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				
				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData;
				inputData.positionWS = WorldPosition;
				inputData.viewDirectionWS = WorldViewDirection;
				inputData.shadowCoord = ShadowCoords;

				#ifdef _NORMALMAP
					inputData.normalWS = normalize(TransformTangentToWorld(Normal, half3x3( WorldTangent, WorldBiTangent, WorldNormal )));
				#else
					#if !SHADER_HINT_NICE_QUALITY
						inputData.normalWS = WorldNormal;
					#else
						inputData.normalWS = normalize( WorldNormal );
					#endif
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = IN.fogFactorAndVertexLight.x;
				#endif

				inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
				inputData.bakedGI = SAMPLE_GI( IN.lightmapUVOrVertexSH.xy, IN.lightmapUVOrVertexSH.xyz, inputData.normalWS );
				#ifdef _ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif
				half4 color = UniversalFragmentPBR(
					inputData, 
					Albedo, 
					Metallic, 
					Specular, 
					Smoothness, 
					Occlusion, 
					Emission, 
					Alpha);

				#ifdef _REFRACTION_ASE
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, WorldNormal ).xyz * ( 1.0 / ( ScreenPos.z + 1.0 ) ) * ( 1.0 - dot( WorldNormal, WorldViewDirection ) );
					float2 cameraRefraction = float2( refractionOffset.x, -( refractionOffset.y * _ProjectionParams.x ) );
					projScreenPos.xy += cameraRefraction;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3( 0, 0, 0 ), IN.fogFactorAndVertexLight.x );
					#else
						color.rgb = MixFog(color.rgb, IN.fogFactorAndVertexLight.x);
					#endif
				#endif
				
				return color;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 999999

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment

			#define SHADERPASS_SHADOWCASTER

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#pragma shader_feature _IGNORESECONDROADALPHA_ON
			#pragma shader_feature _IGNORECROSSROADALPHA_ON


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			sampler2D _MainTex;
			sampler2D _BumpMap;
			sampler2D _TextureSample3;
			sampler2D _SecondRoadNormal;
			sampler2D _TextureSample1;
			sampler2D _TextureSample4;
			sampler2D _CrossRoadNormal;
			CBUFFER_START( UnityPerMaterial )
			float _MainRoadBrightness;
			float _MainRoadUV3;
			float4 _MainTex_ST;
			float4 _BumpMap_ST;
			float4 _MainRoadColor;
			float _DetailAlbedoPower;
			float4 _DetailAlbedoMap_ST;
			float4 _DetailMask_ST;
			float _SecondRoadBrightness;
			float _SecondRoadUV3;
			float4 _TextureSample3_ST;
			float4 _SecondRoadNormal_ST;
			float4 _SecondRoadColor;
			float _Float2;
			float4 _TextureSample1_ST;
			float _SecondRoadNoiseMaskPower;
			float _SecondRoadNoiseMaskTreshold;
			float _CrossRoadBrightness;
			float _CrossRoadUV3;
			float4 _TextureSample4_ST;
			float4 _CrossRoadNormal_ST;
			float4 _CrossRoadColor;
			float _Float3;
			float _BumpScale;
			float _DetailNormalMapScale;
			float _SecondRoadNormalScale;
			float _SecondRoadNormalBlend;
			float _Float1;
			float _CrossRoadNormalScale;
			float _Float0;
			float _MainRoadMetalicPower;
			float _SecondRoadMetalicPower;
			float _CrossRoadMetallicPower;
			float _MainRoadSmoothnessPower;
			float _SecondRoadSmoothnessPower;
			float _CrossRoadSmoothnessPower;
			float _MainRoadAmbientOcclusionPower;
			float _SecondRoadAmbientOcclusionPower;
			float _CrossRoadAmbientOcclusionPower;
			float _MainRoadAlphaCutOut;
			float _SecondRoadAlphaCutOut;
			float _CrossRoadAlphaCutOut;
			CBUFFER_END


			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			float3 _LightDirection;

			VertexOutput ShadowPassVertex( VertexInput v )
			{
				VertexOutput o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord2.zw = v.ase_texcoord2.xy;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif
				float3 normalWS = TransformObjectToWorldDir(v.ase_normal);

				float4 clipPos = TransformWorldToHClip( ApplyShadowBias( positionWS, normalWS, _LightDirection ) );

				#if UNITY_REVERSED_Z
					clipPos.z = min(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#else
					clipPos.z = max(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				o.clipPos = clipPos;
				return o;
			}

			half4 ShadowPassFragment(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
				
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv0_MainTex = IN.ase_texcoord2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 uv2_BumpMap = IN.ase_texcoord2.zw * _BumpMap_ST.xy + _BumpMap_ST.zw;
				float4 tex2DNode1 = tex2D( _MainTex, (( _MainRoadUV3 )?( uv2_BumpMap ):( uv0_MainTex )) );
				float temp_output_629_0 = ( tex2DNode1.a * _MainRoadAlphaCutOut );
				float2 uv0_TextureSample3 = IN.ase_texcoord2.xy * _TextureSample3_ST.xy + _TextureSample3_ST.zw;
				float2 uv2_SecondRoadNormal = IN.ase_texcoord2.zw * _SecondRoadNormal_ST.xy + _SecondRoadNormal_ST.zw;
				float4 tex2DNode537 = tex2D( _TextureSample3, (( _SecondRoadUV3 )?( uv2_SecondRoadNormal ):( uv0_TextureSample3 )) );
				#ifdef _IGNORESECONDROADALPHA_ON
				float staticSwitch693 = temp_output_629_0;
				#else
				float staticSwitch693 = ( tex2DNode537.a * _SecondRoadAlphaCutOut );
				#endif
				float4 break666 = ( IN.ase_color / float4( 1,1,1,1 ) );
				float2 uv0_TextureSample1 = IN.ase_texcoord2.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float temp_output_682_0 = min( min( tex2D( _TextureSample1, uv0_TextureSample1 ).r , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.4,0.4 ) ) ).r ) , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.2,0.2 ) ) ).r );
				float clampResult673 = clamp( pow( abs( ( temp_output_682_0 * _SecondRoadNoiseMaskPower ) ) , abs( _SecondRoadNoiseMaskTreshold ) ) , 0.0 , 1.0 );
				float4 appendResult665 = (float4(( ( 1.0 - break666.r ) - clampResult673 ) , ( 1.0 - break666.g ) , break666.b , break666.a));
				float4 clampResult672 = clamp( appendResult665 , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
				float4 break496 = ( 1.0 - clampResult672 );
				float lerpResult628 = lerp( temp_output_629_0 , staticSwitch693 , break496.x);
				float2 uv0_TextureSample4 = IN.ase_texcoord2.xy * _TextureSample4_ST.xy + _TextureSample4_ST.zw;
				float2 uv2_CrossRoadNormal = IN.ase_texcoord2.zw * _CrossRoadNormal_ST.xy + _CrossRoadNormal_ST.zw;
				float4 tex2DNode638 = tex2D( _TextureSample4, (( _CrossRoadUV3 )?( uv2_CrossRoadNormal ):( uv0_TextureSample4 )) );
				#ifdef _IGNORECROSSROADALPHA_ON
				float staticSwitch696 = lerpResult628;
				#else
				float staticSwitch696 = ( tex2DNode638.a * _CrossRoadAlphaCutOut );
				#endif
				float lerpResult849 = lerp( staticSwitch696 , staticSwitch693 , break496.x);
				float lerpResult641 = lerp( lerpResult628 , lerpResult849 , break496.y);
				
				float Alpha = lerpResult641;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				return 0;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 999999

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#pragma shader_feature _IGNORESECONDROADALPHA_ON
			#pragma shader_feature _IGNORECROSSROADALPHA_ON


			sampler2D _MainTex;
			sampler2D _BumpMap;
			sampler2D _TextureSample3;
			sampler2D _SecondRoadNormal;
			sampler2D _TextureSample1;
			sampler2D _TextureSample4;
			sampler2D _CrossRoadNormal;
			CBUFFER_START( UnityPerMaterial )
			float _MainRoadBrightness;
			float _MainRoadUV3;
			float4 _MainTex_ST;
			float4 _BumpMap_ST;
			float4 _MainRoadColor;
			float _DetailAlbedoPower;
			float4 _DetailAlbedoMap_ST;
			float4 _DetailMask_ST;
			float _SecondRoadBrightness;
			float _SecondRoadUV3;
			float4 _TextureSample3_ST;
			float4 _SecondRoadNormal_ST;
			float4 _SecondRoadColor;
			float _Float2;
			float4 _TextureSample1_ST;
			float _SecondRoadNoiseMaskPower;
			float _SecondRoadNoiseMaskTreshold;
			float _CrossRoadBrightness;
			float _CrossRoadUV3;
			float4 _TextureSample4_ST;
			float4 _CrossRoadNormal_ST;
			float4 _CrossRoadColor;
			float _Float3;
			float _BumpScale;
			float _DetailNormalMapScale;
			float _SecondRoadNormalScale;
			float _SecondRoadNormalBlend;
			float _Float1;
			float _CrossRoadNormalScale;
			float _Float0;
			float _MainRoadMetalicPower;
			float _SecondRoadMetalicPower;
			float _CrossRoadMetallicPower;
			float _MainRoadSmoothnessPower;
			float _SecondRoadSmoothnessPower;
			float _CrossRoadSmoothnessPower;
			float _MainRoadAmbientOcclusionPower;
			float _SecondRoadAmbientOcclusionPower;
			float _CrossRoadAmbientOcclusionPower;
			float _MainRoadAlphaCutOut;
			float _SecondRoadAlphaCutOut;
			float _CrossRoadAlphaCutOut;
			CBUFFER_END


			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord2.zw = v.ase_texcoord2.xy;
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;
				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = positionCS;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				o.clipPos = positionCS;
				return o;
			}

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv0_MainTex = IN.ase_texcoord2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 uv2_BumpMap = IN.ase_texcoord2.zw * _BumpMap_ST.xy + _BumpMap_ST.zw;
				float4 tex2DNode1 = tex2D( _MainTex, (( _MainRoadUV3 )?( uv2_BumpMap ):( uv0_MainTex )) );
				float temp_output_629_0 = ( tex2DNode1.a * _MainRoadAlphaCutOut );
				float2 uv0_TextureSample3 = IN.ase_texcoord2.xy * _TextureSample3_ST.xy + _TextureSample3_ST.zw;
				float2 uv2_SecondRoadNormal = IN.ase_texcoord2.zw * _SecondRoadNormal_ST.xy + _SecondRoadNormal_ST.zw;
				float4 tex2DNode537 = tex2D( _TextureSample3, (( _SecondRoadUV3 )?( uv2_SecondRoadNormal ):( uv0_TextureSample3 )) );
				#ifdef _IGNORESECONDROADALPHA_ON
				float staticSwitch693 = temp_output_629_0;
				#else
				float staticSwitch693 = ( tex2DNode537.a * _SecondRoadAlphaCutOut );
				#endif
				float4 break666 = ( IN.ase_color / float4( 1,1,1,1 ) );
				float2 uv0_TextureSample1 = IN.ase_texcoord2.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float temp_output_682_0 = min( min( tex2D( _TextureSample1, uv0_TextureSample1 ).r , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.4,0.4 ) ) ).r ) , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.2,0.2 ) ) ).r );
				float clampResult673 = clamp( pow( abs( ( temp_output_682_0 * _SecondRoadNoiseMaskPower ) ) , abs( _SecondRoadNoiseMaskTreshold ) ) , 0.0 , 1.0 );
				float4 appendResult665 = (float4(( ( 1.0 - break666.r ) - clampResult673 ) , ( 1.0 - break666.g ) , break666.b , break666.a));
				float4 clampResult672 = clamp( appendResult665 , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
				float4 break496 = ( 1.0 - clampResult672 );
				float lerpResult628 = lerp( temp_output_629_0 , staticSwitch693 , break496.x);
				float2 uv0_TextureSample4 = IN.ase_texcoord2.xy * _TextureSample4_ST.xy + _TextureSample4_ST.zw;
				float2 uv2_CrossRoadNormal = IN.ase_texcoord2.zw * _CrossRoadNormal_ST.xy + _CrossRoadNormal_ST.zw;
				float4 tex2DNode638 = tex2D( _TextureSample4, (( _CrossRoadUV3 )?( uv2_CrossRoadNormal ):( uv0_TextureSample4 )) );
				#ifdef _IGNORECROSSROADALPHA_ON
				float staticSwitch696 = lerpResult628;
				#else
				float staticSwitch696 = ( tex2DNode638.a * _CrossRoadAlphaCutOut );
				#endif
				float lerpResult849 = lerp( staticSwitch696 , staticSwitch693 , break496.x);
				float lerpResult641 = lerp( lerpResult628 , lerpResult849 , break496.y);
				
				float Alpha = lerpResult641;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODDitheringTransition( IN.clipPos.xyz, unity_LODFade.x );
				#endif
				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 999999

			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_META

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

			#pragma shader_feature _IGNORESECONDROADALPHA_ON
			#pragma shader_feature _IGNORECROSSROADALPHA_ON


			sampler2D _MainTex;
			sampler2D _BumpMap;
			sampler2D _DetailAlbedoMap;
			sampler2D _DetailMask;
			sampler2D _TextureSample3;
			sampler2D _SecondRoadNormal;
			sampler2D _TextureSample1;
			sampler2D _TextureSample4;
			sampler2D _CrossRoadNormal;
			CBUFFER_START( UnityPerMaterial )
			float _MainRoadBrightness;
			float _MainRoadUV3;
			float4 _MainTex_ST;
			float4 _BumpMap_ST;
			float4 _MainRoadColor;
			float _DetailAlbedoPower;
			float4 _DetailAlbedoMap_ST;
			float4 _DetailMask_ST;
			float _SecondRoadBrightness;
			float _SecondRoadUV3;
			float4 _TextureSample3_ST;
			float4 _SecondRoadNormal_ST;
			float4 _SecondRoadColor;
			float _Float2;
			float4 _TextureSample1_ST;
			float _SecondRoadNoiseMaskPower;
			float _SecondRoadNoiseMaskTreshold;
			float _CrossRoadBrightness;
			float _CrossRoadUV3;
			float4 _TextureSample4_ST;
			float4 _CrossRoadNormal_ST;
			float4 _CrossRoadColor;
			float _Float3;
			float _BumpScale;
			float _DetailNormalMapScale;
			float _SecondRoadNormalScale;
			float _SecondRoadNormalBlend;
			float _Float1;
			float _CrossRoadNormalScale;
			float _Float0;
			float _MainRoadMetalicPower;
			float _SecondRoadMetalicPower;
			float _CrossRoadMetallicPower;
			float _MainRoadSmoothnessPower;
			float _SecondRoadSmoothnessPower;
			float _CrossRoadSmoothnessPower;
			float _MainRoadAmbientOcclusionPower;
			float _SecondRoadAmbientOcclusionPower;
			float _CrossRoadAmbientOcclusionPower;
			float _MainRoadAlphaCutOut;
			float _SecondRoadAlphaCutOut;
			float _CrossRoadAlphaCutOut;
			CBUFFER_END


			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord2.zw = v.texcoord2.xy;
				o.ase_color = v.ase_color;
				
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				o.clipPos = MetaVertexPosition( v.vertex, v.texcoord1.xy, v.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = o.clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				return o;
			}

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv0_MainTex = IN.ase_texcoord2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 uv2_BumpMap = IN.ase_texcoord2.zw * _BumpMap_ST.xy + _BumpMap_ST.zw;
				float4 tex2DNode1 = tex2D( _MainTex, (( _MainRoadUV3 )?( uv2_BumpMap ):( uv0_MainTex )) );
				float4 temp_output_77_0 = ( ( _MainRoadBrightness * tex2DNode1 ) * _MainRoadColor );
				float2 uv0_DetailAlbedoMap = IN.ase_texcoord2.xy * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
				float4 tex2DNode486 = tex2D( _DetailAlbedoMap, uv0_DetailAlbedoMap );
				float4 blendOpSrc474 = temp_output_77_0;
				float4 blendOpDest474 = ( _DetailAlbedoPower * tex2DNode486 );
				float2 uv0_DetailMask = IN.ase_texcoord2.xy * _DetailMask_ST.xy + _DetailMask_ST.zw;
				float4 tex2DNode481 = tex2D( _DetailMask, uv0_DetailMask );
				float4 lerpResult480 = lerp( temp_output_77_0 , (( blendOpDest474 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest474 ) * ( 1.0 - blendOpSrc474 ) ) : ( 2.0 * blendOpDest474 * blendOpSrc474 ) ) , ( _DetailAlbedoPower * tex2DNode481.a ));
				float2 uv0_TextureSample3 = IN.ase_texcoord2.xy * _TextureSample3_ST.xy + _TextureSample3_ST.zw;
				float2 uv2_SecondRoadNormal = IN.ase_texcoord2.zw * _SecondRoadNormal_ST.xy + _SecondRoadNormal_ST.zw;
				float4 tex2DNode537 = tex2D( _TextureSample3, (( _SecondRoadUV3 )?( uv2_SecondRoadNormal ):( uv0_TextureSample3 )) );
				float4 temp_output_540_0 = ( ( _SecondRoadBrightness * tex2DNode537 ) * _SecondRoadColor );
				float4 blendOpSrc619 = temp_output_540_0;
				float4 blendOpDest619 = ( tex2DNode486 * _Float2 );
				float4 lerpResult618 = lerp( temp_output_540_0 , (( blendOpDest619 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest619 ) * ( 1.0 - blendOpSrc619 ) ) : ( 2.0 * blendOpDest619 * blendOpSrc619 ) ) , ( _Float2 * tex2DNode481.a ));
				float4 break666 = ( IN.ase_color / float4( 1,1,1,1 ) );
				float2 uv0_TextureSample1 = IN.ase_texcoord2.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float temp_output_682_0 = min( min( tex2D( _TextureSample1, uv0_TextureSample1 ).r , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.4,0.4 ) ) ).r ) , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.2,0.2 ) ) ).r );
				float clampResult673 = clamp( pow( abs( ( temp_output_682_0 * _SecondRoadNoiseMaskPower ) ) , abs( _SecondRoadNoiseMaskTreshold ) ) , 0.0 , 1.0 );
				float4 appendResult665 = (float4(( ( 1.0 - break666.r ) - clampResult673 ) , ( 1.0 - break666.g ) , break666.b , break666.a));
				float4 clampResult672 = clamp( appendResult665 , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
				float4 break496 = ( 1.0 - clampResult672 );
				float4 lerpResult592 = lerp( lerpResult480 , lerpResult618 , break496.x);
				float2 uv0_TextureSample4 = IN.ase_texcoord2.xy * _TextureSample4_ST.xy + _TextureSample4_ST.zw;
				float2 uv2_CrossRoadNormal = IN.ase_texcoord2.zw * _CrossRoadNormal_ST.xy + _CrossRoadNormal_ST.zw;
				float4 tex2DNode638 = tex2D( _TextureSample4, (( _CrossRoadUV3 )?( uv2_CrossRoadNormal ):( uv0_TextureSample4 )) );
				float4 temp_output_654_0 = ( ( _CrossRoadBrightness * tex2DNode638 ) * _CrossRoadColor );
				float4 blendOpSrc652 = temp_output_654_0;
				float4 blendOpDest652 = tex2DNode486;
				float4 lerpResult653 = lerp( temp_output_654_0 , (( blendOpDest652 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest652 ) * ( 1.0 - blendOpSrc652 ) ) : ( 2.0 * blendOpDest652 * blendOpSrc652 ) ) , ( tex2DNode481.a * _Float3 ));
				float4 lerpResult852 = lerp( lerpResult653 , lerpResult618 , break496.x);
				float4 lerpResult644 = lerp( lerpResult592 , lerpResult852 , break496.y);
				
				float temp_output_629_0 = ( tex2DNode1.a * _MainRoadAlphaCutOut );
				#ifdef _IGNORESECONDROADALPHA_ON
				float staticSwitch693 = temp_output_629_0;
				#else
				float staticSwitch693 = ( tex2DNode537.a * _SecondRoadAlphaCutOut );
				#endif
				float lerpResult628 = lerp( temp_output_629_0 , staticSwitch693 , break496.x);
				#ifdef _IGNORECROSSROADALPHA_ON
				float staticSwitch696 = lerpResult628;
				#else
				float staticSwitch696 = ( tex2DNode638.a * _CrossRoadAlphaCutOut );
				#endif
				float lerpResult849 = lerp( staticSwitch696 , staticSwitch693 , break496.x);
				float lerpResult641 = lerp( lerpResult628 , lerpResult849 , break496.y);
				
				
				float3 Albedo = lerpResult644.rgb;
				float3 Emission = 0;
				float Alpha = lerpResult641;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = Albedo;
				metaInput.Emission = Emission;
				
				return MetaFragment(metaInput);
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }

			Blend One Zero , One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _ALPHATEST_ON 1
			#define _NORMALMAP 1
			#define ASE_SRP_VERSION 999999

			#pragma enable_d3d11_debug_symbols
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS_2D

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			
			#pragma shader_feature _IGNORESECONDROADALPHA_ON
			#pragma shader_feature _IGNORECROSSROADALPHA_ON


			sampler2D _MainTex;
			sampler2D _BumpMap;
			sampler2D _DetailAlbedoMap;
			sampler2D _DetailMask;
			sampler2D _TextureSample3;
			sampler2D _SecondRoadNormal;
			sampler2D _TextureSample1;
			sampler2D _TextureSample4;
			sampler2D _CrossRoadNormal;
			CBUFFER_START( UnityPerMaterial )
			float _MainRoadBrightness;
			float _MainRoadUV3;
			float4 _MainTex_ST;
			float4 _BumpMap_ST;
			float4 _MainRoadColor;
			float _DetailAlbedoPower;
			float4 _DetailAlbedoMap_ST;
			float4 _DetailMask_ST;
			float _SecondRoadBrightness;
			float _SecondRoadUV3;
			float4 _TextureSample3_ST;
			float4 _SecondRoadNormal_ST;
			float4 _SecondRoadColor;
			float _Float2;
			float4 _TextureSample1_ST;
			float _SecondRoadNoiseMaskPower;
			float _SecondRoadNoiseMaskTreshold;
			float _CrossRoadBrightness;
			float _CrossRoadUV3;
			float4 _TextureSample4_ST;
			float4 _CrossRoadNormal_ST;
			float4 _CrossRoadColor;
			float _Float3;
			float _BumpScale;
			float _DetailNormalMapScale;
			float _SecondRoadNormalScale;
			float _SecondRoadNormalBlend;
			float _Float1;
			float _CrossRoadNormalScale;
			float _Float0;
			float _MainRoadMetalicPower;
			float _SecondRoadMetalicPower;
			float _CrossRoadMetallicPower;
			float _MainRoadSmoothnessPower;
			float _SecondRoadSmoothnessPower;
			float _CrossRoadSmoothnessPower;
			float _MainRoadAmbientOcclusionPower;
			float _SecondRoadAmbientOcclusionPower;
			float _CrossRoadAmbientOcclusionPower;
			float _MainRoadAlphaCutOut;
			float _SecondRoadAlphaCutOut;
			float _CrossRoadAlphaCutOut;
			CBUFFER_END


			#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 ase_normal : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 worldPos : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord2.zw = v.ase_texcoord2.xy;
				o.ase_color = v.ase_color;
				
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.vertex.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.vertex.xyz = vertexValue;
				#else
					v.vertex.xyz += vertexValue;
				#endif

				v.ase_normal = v.ase_normal;

				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				o.worldPos = positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = positionCS;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				o.clipPos = positionCS;
				return o;
			}

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.worldPos;
				#endif
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv0_MainTex = IN.ase_texcoord2.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 uv2_BumpMap = IN.ase_texcoord2.zw * _BumpMap_ST.xy + _BumpMap_ST.zw;
				float4 tex2DNode1 = tex2D( _MainTex, (( _MainRoadUV3 )?( uv2_BumpMap ):( uv0_MainTex )) );
				float4 temp_output_77_0 = ( ( _MainRoadBrightness * tex2DNode1 ) * _MainRoadColor );
				float2 uv0_DetailAlbedoMap = IN.ase_texcoord2.xy * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
				float4 tex2DNode486 = tex2D( _DetailAlbedoMap, uv0_DetailAlbedoMap );
				float4 blendOpSrc474 = temp_output_77_0;
				float4 blendOpDest474 = ( _DetailAlbedoPower * tex2DNode486 );
				float2 uv0_DetailMask = IN.ase_texcoord2.xy * _DetailMask_ST.xy + _DetailMask_ST.zw;
				float4 tex2DNode481 = tex2D( _DetailMask, uv0_DetailMask );
				float4 lerpResult480 = lerp( temp_output_77_0 , (( blendOpDest474 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest474 ) * ( 1.0 - blendOpSrc474 ) ) : ( 2.0 * blendOpDest474 * blendOpSrc474 ) ) , ( _DetailAlbedoPower * tex2DNode481.a ));
				float2 uv0_TextureSample3 = IN.ase_texcoord2.xy * _TextureSample3_ST.xy + _TextureSample3_ST.zw;
				float2 uv2_SecondRoadNormal = IN.ase_texcoord2.zw * _SecondRoadNormal_ST.xy + _SecondRoadNormal_ST.zw;
				float4 tex2DNode537 = tex2D( _TextureSample3, (( _SecondRoadUV3 )?( uv2_SecondRoadNormal ):( uv0_TextureSample3 )) );
				float4 temp_output_540_0 = ( ( _SecondRoadBrightness * tex2DNode537 ) * _SecondRoadColor );
				float4 blendOpSrc619 = temp_output_540_0;
				float4 blendOpDest619 = ( tex2DNode486 * _Float2 );
				float4 lerpResult618 = lerp( temp_output_540_0 , (( blendOpDest619 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest619 ) * ( 1.0 - blendOpSrc619 ) ) : ( 2.0 * blendOpDest619 * blendOpSrc619 ) ) , ( _Float2 * tex2DNode481.a ));
				float4 break666 = ( IN.ase_color / float4( 1,1,1,1 ) );
				float2 uv0_TextureSample1 = IN.ase_texcoord2.xy * _TextureSample1_ST.xy + _TextureSample1_ST.zw;
				float temp_output_682_0 = min( min( tex2D( _TextureSample1, uv0_TextureSample1 ).r , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.4,0.4 ) ) ).r ) , tex2D( _TextureSample1, ( uv0_TextureSample1 * float2( 0.2,0.2 ) ) ).r );
				float clampResult673 = clamp( pow( abs( ( temp_output_682_0 * _SecondRoadNoiseMaskPower ) ) , abs( _SecondRoadNoiseMaskTreshold ) ) , 0.0 , 1.0 );
				float4 appendResult665 = (float4(( ( 1.0 - break666.r ) - clampResult673 ) , ( 1.0 - break666.g ) , break666.b , break666.a));
				float4 clampResult672 = clamp( appendResult665 , float4( 0,0,0,0 ) , float4( 1,1,1,1 ) );
				float4 break496 = ( 1.0 - clampResult672 );
				float4 lerpResult592 = lerp( lerpResult480 , lerpResult618 , break496.x);
				float2 uv0_TextureSample4 = IN.ase_texcoord2.xy * _TextureSample4_ST.xy + _TextureSample4_ST.zw;
				float2 uv2_CrossRoadNormal = IN.ase_texcoord2.zw * _CrossRoadNormal_ST.xy + _CrossRoadNormal_ST.zw;
				float4 tex2DNode638 = tex2D( _TextureSample4, (( _CrossRoadUV3 )?( uv2_CrossRoadNormal ):( uv0_TextureSample4 )) );
				float4 temp_output_654_0 = ( ( _CrossRoadBrightness * tex2DNode638 ) * _CrossRoadColor );
				float4 blendOpSrc652 = temp_output_654_0;
				float4 blendOpDest652 = tex2DNode486;
				float4 lerpResult653 = lerp( temp_output_654_0 , (( blendOpDest652 > 0.5 ) ? ( 1.0 - 2.0 * ( 1.0 - blendOpDest652 ) * ( 1.0 - blendOpSrc652 ) ) : ( 2.0 * blendOpDest652 * blendOpSrc652 ) ) , ( tex2DNode481.a * _Float3 ));
				float4 lerpResult852 = lerp( lerpResult653 , lerpResult618 , break496.x);
				float4 lerpResult644 = lerp( lerpResult592 , lerpResult852 , break496.y);
				
				float temp_output_629_0 = ( tex2DNode1.a * _MainRoadAlphaCutOut );
				#ifdef _IGNORESECONDROADALPHA_ON
				float staticSwitch693 = temp_output_629_0;
				#else
				float staticSwitch693 = ( tex2DNode537.a * _SecondRoadAlphaCutOut );
				#endif
				float lerpResult628 = lerp( temp_output_629_0 , staticSwitch693 , break496.x);
				#ifdef _IGNORECROSSROADALPHA_ON
				float staticSwitch696 = lerpResult628;
				#else
				float staticSwitch696 = ( tex2DNode638.a * _CrossRoadAlphaCutOut );
				#endif
				float lerpResult849 = lerp( staticSwitch696 , staticSwitch693 , break496.x);
				float lerpResult641 = lerp( lerpResult628 , lerpResult849 , break496.y);
				
				
				float3 Albedo = lerpResult644.rgb;
				float Alpha = lerpResult641;
				float AlphaClipThreshold = 0.5;

				half4 color = half4( Albedo, Alpha );

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				return color;
			}
			ENDHLSL
		}
		
	}
	CustomEditor "UnityEditor.ShaderGraph.PBRMasterGUI"
	Fallback "Hidden/InternalErrorShader"
	
}