Shader "Custom/Tree"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _NormalMap ("Normal", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _xFactor ("XFactor", Float) = 1.0
        _heightFactor ("Height XFactor", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma vertex vert
        #pragma surface surf Standard addshadow
        #include "wind.cginc"

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NormalMap;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _xFactor;
        float _heightFactor;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert( inout appdata_full v )
        {
            float3 worldPos = mul( unity_ObjectToWorld, v.vertex );
            float3 wind = calculateWindAt( worldPos );
            float3 localShift = float4( wind.x, 0, wind.y, 0 );
            float height = worldPos.y - unity_ObjectToWorld[1][3];
            float strength = height * height * _heightFactor;
            v.vertex += strength * mul( unity_WorldToObject, localShift );
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            float2 tc = IN.uv_MainTex;
            tc.x = tc.x * _xFactor;
            fixed4 c = tex2D (_MainTex, tc) * _Color;
            clip( c.a - 0.5 );
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Normal = UnpackNormal(tex2D (_NormalMap, tc));
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
