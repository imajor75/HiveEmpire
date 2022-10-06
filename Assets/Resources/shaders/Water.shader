Shader "Unlit/Water"
{
    Properties
    {
        _MainTex ("Waves", 2D) = "white" {}
        _FoamTex ("Foam", 2D) = "white" {}
        _Offset0 ("Offset0", Float) = 0
        _Offset1 ("Offset1", Float) = 0
        _Iter ("Iter", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

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
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD4;
                float depth : TEXCOORD3;
                float4 vertex : SV_POSITION;
                float2 localPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _FoamTex;
            float Offset0, Offset1;
            float Iter;
            bool Waves;

            v2f vert (appdata v)
            {
                v2f o;
                o.localPos = v.vertex.xz;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.depth = v.uv.x;
                o.uv0 = TRANSFORM_TEX( ( ( v.vertex.xz + float2( Offset0, 0 ) ) / 20 ), _MainTex );
                o.uv1 = TRANSFORM_TEX( ( ( v.vertex.xz + float2( Offset1, Offset1 ) ) / 20 ), _MainTex );
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 col0 = tex2D( _MainTex, i.uv0 );
                fixed3 col1 = tex2D( _MainTex, i.uv1 );
                fixed innerAlpha = saturate( i.depth * 2 );
                if ( !Waves )
                    return fixed4( lerp( col0, col1, 0.5 ), 1 );
                fixed4 inner = fixed4( lerp( col0, col1, 0.5 ), innerAlpha );
                fixed4 outer = tex2D( _FoamTex, i.localPos / 20 );
                fixed foamWeight = frac( 1 - Iter - i.depth * 8 ) * saturate( 1 - i.depth * 8 );
                return lerp( inner, outer, foamWeight );
            }
            ENDCG
        }
    }
}
