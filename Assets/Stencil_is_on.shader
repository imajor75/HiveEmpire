Shader "Unlit/Stencil_is_on"
{
	//Properties{ _StencilIsOn("Texture", 2D) = "white" {} }
	SubShader{
		Pass{
		ZTest Always Cull Off ZWrite Off

		Stencil {
			Ref 128
			Comp Equal
		}

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 2.0

		#include "UnityCG.cginc"
		
		sampler2D _OlderTexture;
		float4 _OlderTexture_ST;

		struct appdata_t {
			float4 vertex : POSITION;
			float2 texcoord : TEXCOORD0;
		};

		struct v2f {
			float4 vertex : SV_POSITION;
			float2 texcoord : TEXCOORD0;
		};

		v2f vert(appdata_t v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _OlderTexture);
			return o;
		}

		fixed4 frag(v2f i) : SV_Target
		{
			return tex2D(_OlderTexture, i.texcoord);
		}
			ENDCG

		}
	}
		Fallback Off
}