Shader "Unlit/Highlight"
{
	Properties
	{
		_MainTex("Main Texture", 2D) = "white" {}
		_Mask("Mask", 2D) = "white" {}
		_Blur("Blur", 2D) = "white" {}
		_GlowColor("Glow Color", Color) = (1,1,1,1)
		_SmoothMask("Smooth Mask", 2D) = "white" {}
		_MaskValueOffset("Mask Value Offset", Float) = 0
        _Strength("Strength", Range(0,1)) = 1
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
			sampler _SmoothMask;
			sampler _Blur;
			float _MaskValueOffset;
			float4 _GlowColor;
			float _Strength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
                return o;
            }

			fixed4 frag(v2f i) : SV_Target
			{
				float4 base = tex2D(_MainTex, i.uv);
				if ( tex2D(_Mask, i.uv).r + _MaskValueOffset > 0.5 )
					return base;

				float4 col = tex2D(_Blur, i.uv);
				float4 highlight = col * 0.75f + _GlowColor * ( tex2D(_SmoothMask, i.uv).r + _MaskValueOffset );
				return lerp( base, highlight, _Strength );
            }
            ENDCG
        }
    }
}
