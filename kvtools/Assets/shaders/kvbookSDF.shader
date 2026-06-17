// SDF shader for kvbook.
// This supports a 'nearly bare minimum' arrangement for kvbook to work.
// Importantly, it actually uses derivatives so smoothness works consistently.

Shader "z 20kdc kvtools/kvbook SDF" {
	Properties {
		[NoScaleOffset] _MainTex("SDF Texture", 2D) = "white" {}
		[Toggle] _WebFixup ("Web Fixup (Invert V)", int) = 0
		[Toggle] _InversionSupport ("Inversion Support", int) = 0
		_Inversion("Inversion", Range(0, 1)) = 0.0
		_Bias("SDF Bias (default 0.5)", float) = 0.5
		_Smoothness("SDF Smoothness", Range(0, 256)) = 1.0
		[Toggle] _AlphaInRed ("Alpha In Red (Advanced use. Swizzle and compression must be adjusted.)", int) = 0
		[Toggle] _Truecolour ("Colour Image Support (disabled with Alpha In Red)", int) = 1
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

			#pragma shader_feature _ALPHAINRED_ON
			#pragma shader_feature _TRUECOLOUR_ON
			#pragma shader_feature _WEBFIXUP_ON
			#pragma shader_feature _INVERSIONSUPPORT_ON

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"

			UNITY_DECLARE_TEX2D(_MainTex);
			float4 _MainTex_TexelSize;

			uniform fixed _Inversion;
			uniform float _Smoothness;
			uniform float _Bias;

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
#if _WEBFIXUP_ON
				o.texcoord0 = float2(v.texcoord0.x, 1.0 - v.texcoord0.y);
#else
				o.texcoord0 = v.texcoord0;
#endif
				return o;
			}

			fixed4 kvb_decoder(float2 uvd, fixed4 c, fixed4 incol) {
#if _TRUECOLOUR_ON
				if (c.b > 0.0) {
					// truecolour
					return c * incol;
				} else {
#endif
					// SDF
					// smoothness should be in a texture-pixel-adjacent space
					uvd *= _MainTex_TexelSize.zw;
					float bandwidth = max(uvd.x, uvd.y) * _Smoothness / 16.0;
#if _ALPHAINRED_ON
					float sdfAlpha = c.r;
#else
					float sdfAlpha = c.a;
#endif
					sdfAlpha = saturate(((sdfAlpha - 0.5) / bandwidth) + _Bias);
					return fixed4(incol.rgb, sdfAlpha * incol.a);
#if _TRUECOLOUR_ON
				}
#endif
			}

			fixed4 frag(v2f input) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(input);
				fixed4 c = UNITY_SAMPLE_TEX2D(_MainTex, input.texcoord0);
				c = kvb_decoder(fwidth(input.texcoord0), c, input.colour);
#if _INVERSIONSUPPORT_ON
				return fixed4(lerp(c.rgb, 1.0 - c.rgb, _Inversion), c.a);
#else
				return c;
#endif
			}

			ENDCG
		}
	}
}
