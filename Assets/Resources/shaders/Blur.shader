Shader "Unlit/Blur"
{
	Properties
	{
		_MainTex("Main Texture", 2D) = "white" {}
		_XStart("X Start", Float) = -0.005
		_YStart("Y Start", Float) = -0.0075
		_XMove("X Move", Float) = 0.002
		_YMove("Y Move", Float) = 0.003
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
            float _XStart;
            float _YStart;
            float _XMove;
            float _YMove;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
                return o;
            }

			fixed4 frag(v2f i) : SV_Target
			{
				float4 col = float4( 0, 0, 0, 0 );
                for ( int x = 0; x < 4; x++ )
                    for ( int y = 0; y < 4; y++ )
				        col += tex2D(_MainTex, i.uv + float2( _XStart + _XMove * x, _YStart + _YMove * y ) );
				return col / 16;
            }
            ENDCG
        }
    }
}
