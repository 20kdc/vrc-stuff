// Cubemapped world geometry. (Think 'Ocarina Of Time prerenders'.)
// This is good for a MirrorReflection-layer object for a 'faux cutout mirror' effect.
// CURRENT CAVEAT: didn't work on PC in last test
// may have since fixed this, will need to rebuild preproom2 with new code.

Shader "z 20kdc kvassets/Cubemapped World Geometry" {
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
			#include "UnityCG.cginc"

			UNITY_DECLARE_TEXCUBE(_Cubemap);
			half4 _Cubemap_HDR;
			half3 _Color;
			float3 _CubemapOrigin;

			struct appdata {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 worldPos : TEXCOORD0;
			};

			v2f vert(appdata v) {
				v2f o;

				UNITY_INITIALIZE_OUTPUT(v2f, o);

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
