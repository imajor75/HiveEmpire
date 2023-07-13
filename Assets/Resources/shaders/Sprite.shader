Shader "Unlit/Sprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0,1)) = 1
        _Color ("Color", Color) = (1,1,1,1)
        _Slice ("Slice", Range(0,1)) = 1
        _Peek ("Peek", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Cull Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 screen : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _AlphaMask;
            float _Progress;
            float _AlphaMaskFactor;
            float2 _Mouse;
            float2 _MouseScale;
            float _Slice;
            float4 _MainTex_ST;
            float4 _Color;
            float _Peek;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed2 mouseUV = ( i.screen - _Mouse ) * _MouseScale + fixed2( 0.5, 0.5 );
                fixed alphaMask = tex2D(_AlphaMask, mouseUV);
                clip( _Progress - i.uv.x );
                if ( _Slice < i.uv.y )
                {
                    float lum = 0.1*col.r + 0.6*col.g + 0.3*col.b;
                    col.r = col.g = col.b = lum;
                }
                col.rgb *= _Color;
                col.a -= alphaMask * _AlphaMaskFactor * _Peek;
                if ( col.a < 0 )
                    col.a = 0;
                return col;
            }
            ENDCG
        }
    }
}
