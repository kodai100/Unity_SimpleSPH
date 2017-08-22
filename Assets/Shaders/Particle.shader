Shader "Custom/SPH2D" {
	Properties {
		_MainTex("Texture",         2D) = "black" {}
		_ParticleRadius("Particle Radius", Float) = 0.05
		_WaterColor("WaterColor", Color) = (1, 1, 1, 1)
	}

	CGINCLUDE
	#include "UnityCG.cginc"

	sampler2D _MainTex;
	float4 _MainTex_ST;
	fixed4 _WaterColor;

	float  _ParticleRadius;
	float4x4 _InvViewMatrix;

	struct v2g {
		float3 pos   : POSITION_SV;
		float4 color : COLOR;
	};

	struct g2f {
		float4 pos   : POSITION;
		float2 tex   : TEXCOORD0;
		float4 color : COLOR;
	};

	struct FluidParticle {
		float2 position;
		float2 velocity;
	};

	StructuredBuffer<FluidParticle> _ParticlesBuffer;

	// --------------------------------------------------------------------
	// Vertex Shader
	// --------------------------------------------------------------------
	v2g vert(uint id : SV_VertexID) {

		v2g o = (v2g)0;
		o.pos = float3(_ParticlesBuffer[id].position.xy, 0);
		o.color = float4(0, 0.1, 0.1, 1);
		return o;
	}

	// --------------------------------------------------------------------
	// Geometry Shader
	// --------------------------------------------------------------------
	static const float3 g_positions[4] = {
		float3(-1, 1, 0),
		float3(1, 1, 0),
		float3(-1,-1, 0),
		float3(1,-1, 0),
	};

	static const float2 g_texcoords[4] = {
		float2(0, 0),
		float2(1, 0),
		float2(0, 1),
		float2(1, 1),
	};
		
	[maxvertexcount(4)]
	void geom(point v2g In[1], inout TriangleStream<g2f> triStream) {
		g2f output = (g2f)0;
		[unroll]
		for (int i = 0; i < 4; i++) {
			float3 position = g_positions[i] * _ParticleRadius;
			position = mul(_InvViewMatrix, position) + In[0].pos;
			output.pos = UnityObjectToClipPos(float4(position, 1.0));

			output.color = In[0].color;
			output.tex = g_texcoords[i];
			triStream.Append(output);
		}
		triStream.RestartStrip();
	}

	// --------------------------------------------------------------------
	// Fragment Shader
	// --------------------------------------------------------------------
	fixed4 frag(g2f input) : SV_Target {
		return tex2D(_MainTex, input.tex)*_WaterColor;
	}

	ENDCG

	SubShader {
		Tags{ "RenderType" = "Transparent" "RenderType" = "Transparent" }
		LOD 300

		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass {
			CGPROGRAM
			#pragma target   5.0
			#pragma vertex   vert
			#pragma geometry geom
			#pragma fragment frag
			ENDCG
		}
	}
}