// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Grass"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _SideMove ("Side Move", 2D) = "white" {}
        _Mask ("Mask", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Offset ("Offset", Range(-1,1)) = 0.0
        _TimeFraction ("Time Fraction", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        //LOD 200
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            // Physically based Standard lighting model, and enable shadows on all light types
            #pragma vertex vert
            #pragma fragment frag

            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _SideMove;
            sampler2D _Mask;

            // struct Input
            // {
            // 	float3 worldPos;
            // 	float4 weights : COLOR;
            // };

            // half _Glossiness;
            // half _Metallic;
            half _Offset;
            half _TimeFraction;
            // fixed4 _Color;

            struct v2f
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
            };

            v2f vert ( appdata_full v )
            {
                v2f o;
                v.vertex.y += _Offset * 0.06;
                o.position = UnityObjectToClipPos ( v.vertex );
                o.color.rgb = ShadeVertexLights ( v.vertex, v.normal );
                o.color.a = v.color.r;
                o.uv = v.vertex.xz / 2;
                return o;

    //            v.vertex.z += _Offset * 0.1;
            }

            fixed4 frag( v2f i ) : SV_Target 
            {
                fixed swing = _Offset * _Offset * 0.05;
                fixed2 move = tex2D( _SideMove, i.uv * 0.01 + fixed2( _TimeFraction, _TimeFraction * 0.31 ) ) - fixed2( 0.5, 0.5 );
                fixed2 swinged = i.uv + swing * move;
                float a = tex2D( _Mask, swinged ).r;
                a -= ( 1 - a ) * _Offset * 0.9;
                clip( a - 0.1 );
                fixed4 result = i.color;
                result.a *= a;
                result.rgb *= fixed3( 0.4, 0.6, 0.3 );
                return result;
            }

            // void frag (Input IN, inout SurfaceOutputStandard o)
            // {
            //     // // Albedo comes from a texture tinted by color
            //     // fixed4 c = tex2D (_MainTex, IN.worldPos.xz / 5 ) * _Color;
            //     // o.Albedo = c.rgb;
            //     // // Metallic and smoothness come from slider variables
            //     // o.Metallic = _Metallic;
            //     // o.Smoothness = _Glossiness;
            //     // o.Alpha = tex2D ( _Mask, IN.worldPos.xz / 5 ).a;
            //     // clip( c.a - 0.1 );
            // }
            ENDCG
        }
    }
//    FallBack "Diffuse"
}
