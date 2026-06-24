// Cubemapped world geometry without depth processing.
// This is truly evil stuff.
// To use this, you must disable polygon order optimization for your mesh.
// You must be sure to sort vertices in a back-to-front draw order, unless you're sure a conflict won't arise.
// Ultimately, the goal of this is to cheaply produce a '3D Skybox' similar to Source.

Shader "z 20kdc kvtools/Pre-Rendered Cubemap (No Depth)" {
	Properties {
		_Cubemap("Cubemap", Cube) = "" {}
		_Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_CubemapOrigin("Cubemap Origin", Vector) = (0.0, 1.0, 0.0, 0.0)
	}
	SubShader {
		Tags { "Queue"="AlphaTest" "RenderType"="Opaque" "PreviewType"="Skybox" }
		// Now, in Source, the workflow is basically render 3D skybox with scaled depth -> clear depth -> render world.
		// Here, we don't have that option, but we'd still like early-Z. Actually, we'd really like it if the skybox could be clipped by world geometry for performance reasons.
		// This in mind, we mess with things a little to try to not impact overdraw so much.
		// https://discussions.unity.com/t/overdraw-on-quest-2-tiled-gpu-my-findings-are-not-consistent-with-the-theory/866401/3
		ZWrite Off
		ZClip False
		// Important for LRZ: https://blogs.igalia.com/dpiliaiev/adreno-lrz/
		ZTest LEqual
		LOD 100

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#include "UnityCG.cginc"

			UNITY_DECLARE_TEXCUBE(_Cubemap);
			half4 _Cubemap_HDR;
			half3 _Color;
			float3 _CubemapOrigin;

			struct appdata {
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float3 crPos : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.crPos = mul(unity_ObjectToWorld, v.vertex).xyz - _CubemapOrigin;
				return o;
			}

			half4 frag(v2f i) : SV_Target {
				half3 res = DecodeHDR(UNITY_SAMPLE_TEXCUBE(_Cubemap, normalize(i.crPos)), _Cubemap_HDR);
				return half4(res.rgb * _Color, 1.0);
			}
			ENDCG
		}
	}
}
