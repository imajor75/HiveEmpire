Shader "Unlit/Highlight"
{
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		LOD 100
		ZTest Always

		Stencil
		{
			Ref 0
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = float4( 1, 0, 0, 0.3 );
                return col;
            }
            ENDCG
        }
    }
}
