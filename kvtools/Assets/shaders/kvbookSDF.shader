// SDF shader for kvbook.
// This supports a 'nearly bare minimum' arrangement for kvbook to work.
// Importantly, it actually uses derivatives so smoothness works consistently.

Shader "z 20kdc kvtools/kvbook SDF" {
	Properties {
		_MainTex("SDF Texture", 2D) = "white" {}
		_Inversion("Inversion", Range(0, 1)) = 0.0
		_Smoothness("Smoothness", Range(0, 256)) = 50.0
	}
	SubShader {

		Tags {
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
			"IgnoreProjector" = "True"
		}

		LOD 150

		Cull Back
		ZWrite Off
		Lighting Off
		Fog { Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha

		Pass {
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"

			UNITY_DECLARE_TEX2D(_MainTex);
			uniform fixed _Inversion;
			uniform float _Smoothness;

			struct appdata {
				UNITY_VERTEX_INPUT_INSTANCE_ID
				float4 vertex: POSITION;
				fixed4 colour: COLOR;
				float2 texcoord0: TEXCOORD0;
			};

			struct v2f {
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				float4 vertex: SV_POSITION;
				fixed4 colour: COLOR;
				float2 texcoord0: TEXCOORD0;
			};

			v2f vert(appdata v) {
				v2f o;
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.colour = v.colour;
				o.texcoord0 = v.texcoord0;
				return o;
			}

			fixed4 frag(v2f input) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(input);
				fixed4 c = UNITY_SAMPLE_TEX2D(_MainTex, input.texcoord0);
				float2 uvd = fwidth(input.texcoord0);
				if (c.b > 0.0) {
					// truecolour
					fixed3 invrgbsrc = c.rgb;
					fixed3 invrgb = lerp(invrgbsrc.rgb, 1.0 - invrgbsrc.rgb, _Inversion);
					return fixed4(invrgb, c.a);
				} else {
					// SDF
					float bandwidth = max(uvd.x, uvd.y) * _Smoothness;
					fixed sdfAlpha = saturate(((c.a - 0.5) / bandwidth) + 0.5);
					fixed3 invrgbsrc = input.colour.rgb;
					fixed3 invrgb = lerp(invrgbsrc.rgb, 1.0 - invrgbsrc.rgb, _Inversion);
					return fixed4(invrgb, sdfAlpha * input.colour.a);
				}
			}

			ENDCG
		}
	}
}
