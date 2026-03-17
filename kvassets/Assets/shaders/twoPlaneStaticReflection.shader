// Mobile cubemap-based two-plane reflection shader.
// Checked on Q3S to be able to maintain 72FPS -- please still show restraint about overdraw.
//
// The idea is a specific 'hero' reflection pair (such as floor/ceiling, or two walls) that you want to preserve 'at expense of all else'.
// The rule is that the planes are centred on the plane origin, one at -breadth and one at +breadth.
//
// Additional fun features:
// _MainTex alpha is used for reflection visibility mapping. This allows shaping the reflection without using cutout for 'fake smoothness'.
//
// Performance thoughts:
// This probably approaches Standard Lite complexity. However, we don't have cubemap-array blending support and don't want it (it sucks anyway as it's hard to control).

Shader "z 20kdc kvassets/Mobile Cube-Plane Reflection Shader" {
	Properties {
		_MainTex("Base (RGB) / Smoothness (A)", 2D) = "white" {}
		_Color("Multiplier", Color) = (1,1,1,1)
		[Gamma] _Metallic("Metallic", Range(0,1)) = 1.0
		_Glossiness("(Fallback Only) Smoothness", Range(0,1)) = 1.0

		_PSRCubemap("Reflection Cubemap", Cube) = "" {}
		_PSRCubemapOrigin("Reflection Cubemap Origin", Vector) = (0.0, 1.0, 0.0, 0.0)

		[KeywordEnum(X, Y, Z)] _PSRPlane ("Reflection Plane Axis", int) = 0
		_PSRPlaneOrigin("Reflection Plane Origin", Float) = 0.0
		_PSRPlaneBreadth("Reflection Plane Breadth", Float) = 0.0
	}
	SubShader {

		Pass {
			Name "META"
			Tags {"LightMode"="Meta"}

			Cull Off

			CGPROGRAM
			#pragma vertex vert_meta
			#pragma fragment frag_meta

			#include "UnityStandardMeta.cginc"
			ENDCG
		}

		Tags { "RenderType"="Opaque" "VRCFallback"="VertexLit" }
		LOD 150

		CGPROGRAM
		#pragma shader_feature _PSRPLANE_X _PSRPLANE_Y _PSRPLANE_Z
		#define _SPECULARHIGHLIGHTS_OFF
		#define _GLOSSYREFLECTIONS_OFF
		#include "/Packages/com.vrchat.base/Runtime/VRCSDK/Sample Assets/Shaders/Mobile/VRChat.cginc"
		#pragma target 3.0
		#pragma surface surf LambertVRC exclude_path:prepass exclude_path:deferred noforwardadd noshadow nodynlightmap nolppv noshadowmask

		UNITY_DECLARE_TEX2D(_MainTex);
		uniform fixed4 _Color;

		UNITY_DECLARE_TEXCUBE(_PSRCubemap);
		uniform float4 _PSRCubemapOrigin;
		uniform float _PSRPlaneOrigin;
		uniform float _PSRPlaneBreadth;
		uniform fixed _Metallic;
		uniform fixed _Glossiness;

		struct Input {
			float2 uv_MainTex;
			float3 worldPos;
			float3 worldRefl;
			fixed4 color : COLOR;
		};

		void surf (Input IN, inout SurfaceOutputVRC o) {
			fixed4 c = UNITY_SAMPLE_TEX2D(_MainTex, IN.uv_MainTex) * _Color;
			fixed3 albedo = c.rgb * IN.color;
			fixed smoothness = c.a;

			// 'add in' magic
#if defined(_PSRPLANE_X)
			half theDot = IN.worldRefl.x;
			half distToPlane = _PSRPlaneOrigin - IN.worldPos.x;
#else
#if defined(_PSRPLANE_Y)
			half theDot = IN.worldRefl.y;
			half distToPlane = _PSRPlaneOrigin - IN.worldPos.y;
#else
			half theDot = IN.worldRefl.z;
			half distToPlane = _PSRPlaneOrigin - IN.worldPos.z;
#endif
#endif
			// if theDot is positive, we're hitting the negatively-aimed plane
			half timeToImpact = (distToPlane + (_PSRPlaneBreadth * sign(theDot))) / theDot;
			float3 oRelImpactPoint = IN.worldPos + (IN.worldRefl * timeToImpact);
			half3 reflDir = normalize(oRelImpactPoint - _PSRCubemapOrigin);
			// get reflection and reduce according to simulated smoothness
			fixed3 reflection = UNITY_SAMPLE_TEXCUBE_LOD(_PSRCubemap, reflDir, (1.0 - smoothness) * UNITY_SPECCUBE_LOD_STEPS);

			// this took way too long to get roughly-consistent with Unity
			o.Albedo = albedo * (1.0 - _Metallic);
			o.Emission = (reflection * 0.05) + (reflection * albedo * _Metallic * 0.95);
			o.Alpha = 1.0f;
		}
		ENDCG
	}

	FallBack "VRChat/Mobile/Diffuse"
}
