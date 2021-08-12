Shader "Unlit/Highlight"
{
	Properties
	{
		_MainTex("Main Texture", 2D) = "white" {}
		_OffsetX("Offset X", Float) = 0.004
		_OffsetY("Offset Y", Float) = 0.006
		[IntRange] _StencilRef("Stencil Reference Value", Range(0,255)) = 0
	}

	SubShader
	{
		LOD 100
		ZTest Always
		ZWrite Off
		Cull Off

		Stencil
		{
			Ref [_StencilRef]
			Comp Equal
		}

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag alpha
            // make fog work
            #pragma multi_compile_fog

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
            float _OffsetX;
            float _OffsetY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
                return o;
            }

			fixed4 frag(v2f i) : SV_Target
			{
				float4 col = tex2D(_MainTex, i.uv);
				col += tex2D(_MainTex, i.uv + float2(_OffsetX, -_OffsetY));
				col += tex2D(_MainTex, i.uv + float2(_OffsetX, _OffsetY));
				col += tex2D(_MainTex, i.uv + float2(-_OffsetX, _OffsetY));
				col += tex2D(_MainTex, i.uv + float2(-_OffsetX, -_OffsetY));
				return col * 0.15f;
            }
            ENDCG
        }
    }
}
