// Mobile planar reflection shader.
// This was replaced by the two-plane shader before I even finished the commit.
// That in mind, the metallic stuff is expected to be buggy until I unify some stuff into includes.
// Experimental:
// 1. I suspect lightmapping may act funny, but it seems fine???
// 2. Performance has not been verified.
// This shader is designed assuming a single 'hero' reflection (such as a very obvious light source)
// Reflection visibility mapping allows shaping the reflection without using cutout (essentially extremely fake smoothness).
// Use of the A channel for this turns that part mostly 'free', and the reflection itself is paid for by not sampling a cubemap.
// Note that you need to use Replicate Border on the texture you use for the reflection.

Shader "z 20kdc kvassets/Mobile Planar Reflection Shader" {
	Properties {
		_MainTex("Base (RGB) / Reflectivity (A)", 2D) = "black" {}

		_PSRReflectionImage("Reflection Image", 2D) = "white" {}

		_PSRReflectionAmbient("Reflection Ambient", Color) = (0.0, 0.0, 0.0)

		_PSROrigin("Reflection World Origin", Vector) = (0.0, 1.0, 0.0, 0.0)
		_PSRPlane("Reflection Plane Normal", Vector) = (0.0, -1.0, 0.0, 0.0)

		[Gamma] _Metallic("Metallic", Range(0,1)) = 0.0
		_Glossiness("(Fallback/Bake) Smoothness", Range(0,1)) = 0.5

		[KeywordEnum(XZ, XY, YZ)] _PSRTexPlane ("Reflection Texturing Plane", int) = 0
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
		#pragma shader_feature _PSRTEXPLANE_XZ _PSRTEXPLANE_XY _PSRTEXPLANE_YZ
		#define _SPECULARHIGHLIGHTS_OFF
		#define _GLOSSYREFLECTIONS_OFF
		#include "/Packages/com.vrchat.base/Runtime/VRCSDK/Sample Assets/Shaders/Mobile/VRChat.cginc"
		#pragma target 3.0
		#pragma surface surf LambertVRC exclude_path:prepass exclude_path:deferred noforwardadd noshadow nodynlightmap nolppv noshadowmask

		UNITY_DECLARE_TEX2D(_MainTex);
		UNITY_DECLARE_TEX2D(_PSRReflectionImage);

		uniform float4 _PSROrigin;
		uniform fixed3 _PSRReflectionAmbient;
		uniform float4 _PSRReflectionImage_ST;
		uniform float4 _PSRPlane;
		uniform float _Metallic;

		struct Input {
			float2 uv_MainTex;
			float3 worldPos;
			float3 worldRefl;
			fixed4 color : COLOR;
		};

		void surf (Input IN, inout SurfaceOutputVRC o) {
			fixed4 c = UNITY_SAMPLE_TEX2D(_MainTex, IN.uv_MainTex);
			fixed3 albedo = c.rgb * IN.color;
			o.Albedo = albedo * (1.0 - _Metallic);
			fixed3 reflection = _PSRReflectionAmbient;
			o.Alpha = 1.0f;
			// 'add in' magic
			half3 relOfs = _PSROrigin.xyz - IN.worldPos;
			// this dot should be negative
			half theDot = dot(IN.worldRefl, _PSRPlane.xyz);
			if (theDot < 0.0) {
				// if this dot is not positive, we're behind the plane
				half distToPlane = dot(relOfs, _PSRPlane.xyz);
				if (distToPlane < 0.0) {
					half timeToImpact = distToPlane / -theDot;
					half3 oRelImpactPoint = relOfs + (IN.worldRefl * timeToImpact);
					half2 impactUV = (
#if defined(_PSRTEXPLANE_XY)
						half2(oRelImpactPoint.x, oRelImpactPoint.y)
#else
#if defined(_PSRTEXPLANE_YZ)
						half2(oRelImpactPoint.y, oRelImpactPoint.z)
#else
						half2(oRelImpactPoint.x, oRelImpactPoint.z)
#endif
#endif
					);
					impactUV = impactUV * _PSRReflectionImage_ST.xy + _PSRReflectionImage_ST.zw;
					fixed4 c2 = UNITY_SAMPLE_TEX2D(_PSRReflectionImage, impactUV);
					reflection += c2.rgb;
				}
			}
			// reduce reflection according to simulated smoothness
			reflection *= c.a;
			// note that we always use emission here
			// this is secretly a workaround for SDR on Quest
			o.Emission = lerp(reflection, reflection * albedo, _Metallic);
		}
		ENDCG
	}

	FallBack "VRChat/Mobile/Diffuse"
}
