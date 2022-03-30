Shader "Unlit/Progress"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 color : TEXCOORD1;
            };

            sampler2D _MainTex;
            float _Progress;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);

                float3 bad = float3( 1, 0.1, 0 );
                float3 middle = float3( 1, 0.9, 0.1 );
                float3 good = float3( 0, 1, 0 );

                if ( _Progress < 0.5 )
                    o.color = lerp( bad, middle, _Progress * 2 );
                else
                    o.color = lerp( middle, good, 2 * ( _Progress - 0.5 ) );

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                clip( _Progress - i.uv.x );
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                col.rgb *= i.color;
                return col;
            }
            ENDCG
        }
    }
}
