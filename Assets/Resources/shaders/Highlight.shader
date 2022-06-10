Shader "Unlit/Highlight"
{
	Properties
	{
		_MainTex("Main Texture", 2D) = "white" {}
		_Mask("Mask", 2D) = "white" {}
		_Blur("Blur", 2D) = "white" {}
		_OffsetX("Offset X", Float) = 0.004
		_OffsetY("Offset Y", Float) = 0.006
		_MaskLimit("Mask Limit", Float) = 0.5
	}

	SubShader
	{
		LOD 100
		ZTest Always
		ZWrite Off
		Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag alpha

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

            struct v2f
            {
                float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler _MainTex;
			sampler _Mask;
			sampler _Blur;
            float _OffsetX;
            float _OffsetY;
			float _MaskLimit;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
                return o;
            }

			fixed4 frag(v2f i) : SV_Target
			{
				if ( tex2D(_Mask, i.uv).r < _MaskLimit )
				{
					float4 col = tex2D(_Blur, i.uv);
					col += tex2D(_Blur, i.uv + float2(_OffsetX * 2, -_OffsetY));
					col += tex2D(_Blur, i.uv + float2(_OffsetX, _OffsetY * 2));
					col += tex2D(_Blur, i.uv + float2(-_OffsetX * 2, _OffsetY));
					col += tex2D(_Blur, i.uv + float2(-_OffsetX, -_OffsetY * 2));
					return col * 0.15f;
				}
				return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
