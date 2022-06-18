Shader "Custom/Grass"
{
    Properties
    {
        _Mask ("Mask", 2D) = "white" {}
        _SideMove ("Side Move", 2D) = "white" {}
        _Color ("Color", 2D) = "white" {}
        _WorldScale ("World Scale", Float) = 32
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        CGPROGRAM
        #pragma surface surf Standard keepalpha
        #pragma target 3.0
        
        sampler2D _Mask;
        sampler2D _SideMove;
        sampler2D _Color;

        float _WorldScale;
        int _GameTime;

        struct Input
        {
            float4 weights : COLOR;
            float2 uv_Mask;
            float3 worldPos;
        };

        void surf( Input IN, inout SurfaceOutputStandard o )
        {
            float offset = unity_ObjectToWorld[1][3] * 6.66666;
            fixed2 uv = float2( IN.worldPos.x / _WorldScale * 2 + IN.worldPos.z / _WorldScale, IN.worldPos.z / _WorldScale * 2 );
            fixed swing = offset * offset * 0.08;
            fixed2 move = tex2D( _SideMove, uv + fixed2( _GameTime * 0.004, _GameTime * 0.0015 ) ) - fixed2( 0.5, 0.5 );
            fixed2 swinged = uv * _WorldScale / 4 + swing * move;
            float a = tex2D( _Mask, swinged ).r * IN.weights.r * IN.uv_Mask.r;
            a -= ( 1 - a ) * offset * 0.9;
            clip( a - 0.1 );
            o.Alpha = a;
            o.Albedo = tex2D( _Color, uv * _WorldScale / 4 ) * ( 0.0 + 0.7 * offset );
        }

        ENDCG
    }
}
