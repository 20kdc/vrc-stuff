// 'Emission only' shader.
// You can conceivably use this on any 'plain old light surface'.

Shader "z 20kdc kvtools/Fixed Colour" {
	Properties {
		_Color("Render Colour", Color) = (1,1,1,1)
		// so passing [HDR] here makes the emission colour darker
		// for compatibility with Standard Lite, this is how we have to do things
		_EmissionColor("Bake/Emission Colour", Color) = (1,1,1)
	}
	SubShader {

		Pass {
			Name "META"
			Tags {"LightMode"="Meta"}

			Cull Off

			CGPROGRAM
			#pragma vertex vert_meta
			#pragma fragment frag_meta2
			#pragma shader_feature _EMISSION

			#include "UnityStandardMeta.cginc"

			// float3 _EmissionColor; // already defined

			float4 frag_meta2 (v2f_meta i) : SV_Target {
				UnityMetaInput o;
				UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);
				o.Albedo = float3(0.0, 0.0, 0.0);
				o.SpecularColor = float3(1.0, 1.0, 1.0);
				o.Emission = _EmissionColor;
				return UnityMetaFragment(o);
			}
			ENDCG
		}

		Tags { "RenderType"="Opaque" "VRCFallback"="Lightmapped" }
		LOD 100

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#include "UnityCG.cginc"

			fixed3 _Color;

			struct appdata {
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(appdata v) {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(i);
				return fixed4(_Color, 1.0);
			}
			ENDCG
		}
	}

	FallBack "VRChat/Mobile/Lightmapped"
	CustomEditor "KDCVRCTools.KVToolsFixedColourShaderGUI"
}
