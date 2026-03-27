// Cubemapped world geometry. (Think 'Ocarina Of Time prerenders'.)
// This is good for a MirrorReflection-layer object for a 'faux cutout mirror' effect.

Shader "z 20kdc kvtools/Pre-Rendered Cubemap" {
	Properties {
		_Cubemap("Cubemap", Cube) = "" {}
		_Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_CubemapOrigin("Cubemap Origin", Vector) = (0.0, 1.0, 0.0, 0.0)
	}
	SubShader {
		Tags{ "RenderType" = "Opaque" }
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
				float4 worldPos : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				return o;
			}

			half4 frag(v2f i) : SV_Target {
				half3 res = DecodeHDR(UNITY_SAMPLE_TEXCUBE(_Cubemap, normalize(i.worldPos - _CubemapOrigin)), _Cubemap_HDR);
				return half4(res.rgb * _Color, 1.0);
			}
			ENDCG
		}
	}
}
