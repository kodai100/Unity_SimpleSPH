
Shader "Custom/RealFluid" {
	Properties{
		_MainTex("Source", 2D) = "white" {}
		_Background("Background", 2D) = "black" {}
		_LightVec("LightVec", Vector) = (-0.2, -0.5, 0.5)
		_ViewVec("ViewVec", Vector) = (0, 0, 1)
		_Refraction("Refraction", Range(0.0, 1.0)) = 0.5
		_Lightness("Lightness", Vector) = (0.3, 0.11, 0.59, 0.0)
	}
	SubShader{
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		LOD 200
		ZWrite Off
		Fog{ Mode Off }

		Pass{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert(appdata_img v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord.xy);
				return o;
			}

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			sampler2D _Background;

			float3 _LightVec;
			float3 _ViewVec;
			float _Refraction;
			float4 _Lightness;

			fixed4 frag(v2f i) : SV_TARGET{
				float4 base = tex2D(_MainTex, i.uv);
				float4 Back = base;
				Back.w *= 2;

				float offx = 2.0 / _MainTex_TexelSize.z;
				float offy = 2.0 / _MainTex_TexelSize.w;

				// TODO 灰とマップにする



				// Take all neighbor samples
				float s00 = tex2D(_MainTex, i.uv + float2(-offx, -offy)).w;
				float s01 = tex2D(_MainTex, i.uv + float2(0,   -offy)).w;
				float s02 = tex2D(_MainTex, i.uv + float2(offx, -offy)).w;

				float s10 = tex2D(_MainTex, i.uv + float2(-offx,  0)).w;
				float s12 = tex2D(_MainTex, i.uv + float2(offx,  0)).w;

				float s20 = tex2D(_MainTex, i.uv + float2(-offx,  offy)).w;
				float s21 = tex2D(_MainTex, i.uv + float2(0,    offy)).w;
				float s22 = tex2D(_MainTex, i.uv + float2(offx,  offy)).w;

				float4 sobelX = s00 + 2 * s10 + s20 - s02 - 2 * s12 - s22;
				float4 sobelY = s00 + 2 * s01 + s02 - s20 - 2 * s21 - s22;

				float sx = dot(sobelX, _Lightness);	// lightness
				float sy = dot(sobelY, _Lightness);

				float3 normal = normalize(float3(sx, sy, 1));
				float3 lVec = normalize(_LightVec);	// lightvec
				float diffuse = saturate(dot(lVec, normal));
				float specular = pow(saturate(dot(reflect(-_ViewVec, normal), lVec)), 16);	// viewVec

				float3 refr_v = refract(-_ViewVec, normal, _Refraction);

				float4 backColor = tex2D(_Background, i.uv + refr_v.xy*0.04);
				
				float4 outColor = (diffuse * Back * 0.5 + specular*0.95) + 0.25 * Back;

				//
				outColor.xyz = outColor.xyz * outColor.w + backColor*(1 - outColor.w);
				outColor.w = 1.0f;

				return outColor;
			}
			ENDCG
		}
	}
	FallBack Off
}