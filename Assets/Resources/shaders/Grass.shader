Shader "Custom/Grass"
{
    Properties
    {
        _SideMove ("Side Move", 2D) = "white" {}
        _Mask ("Mask", 2D) = "white" {}
        _Color ("Color", 2D) = "white" {}
        _Offset ("Offset", Range(-1,1)) = 0.0
        _TimeFraction ("Time Fraction", Range(0,1)) = 0.0
    }
    SubShader
    {
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        CGPROGRAM
        #pragma vertex vert
        #pragma surface surf Standard keepalpha
        #pragma target 3.0
        
        sampler2D _SideMove;
        sampler2D _Mask;
        sampler2D _Color;

        half _Offset;
        float _TimeFraction;

        struct Input
        {
            float4 weights : COLOR;
            float3 worldPos;
        };

        struct v2f
        {
            float4 position : POSITION;
            float4 color : COLOR;
        };

        v2f vert ( inout appdata_full v )
        {
            v2f o;
            v.vertex.y += _Offset * 0.1;
            o.position = UnityObjectToClipPos ( v.vertex );
            o.color = fixed4( 1, 1, 1, v.color.r );
            return o;
        }

        void surf( Input IN, inout SurfaceOutputStandard o )
        {
            fixed2 uv = IN.worldPos.xz / 2;
            fixed swing = _Offset * _Offset * 0.08;
            fixed2 move = tex2D( _SideMove, uv * 0.1 + fixed2( _TimeFraction, _TimeFraction * 0.31 ) ) - fixed2( 0.5, 0.5 );
            fixed2 swinged = uv + swing * move;
            float a = tex2D( _Mask, swinged ).r * IN.weights.r;
            a -= ( 1 - a ) * _Offset * 0.9;
            clip( a - 0.1 );
            o.Alpha = a;
            o.Albedo = tex2D( _Color, uv ) * 0.6;
        }

        ENDCG
    }
}
