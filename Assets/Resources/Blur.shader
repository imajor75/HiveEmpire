Shader "Unlit/Blur"
{
	Properties
	{
		_MainTex("Main Texture", 2D) = "white" {}
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
			static float offset = 0.003f;

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
				col += tex2D(_MainTex, i.uv + float2(offset, -offset));
				col += tex2D(_MainTex, i.uv + float2(offset, offset));
				col += tex2D(_MainTex, i.uv + float2(-offset, offset));
				col += tex2D(_MainTex, i.uv + float2(-offset, -offset));
				return col * 0.2f;
            }
            ENDCG
        }
    }
}
