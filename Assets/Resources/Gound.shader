Shader "Custom/Gound"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _GrassTex ("Grass", 2D) = "white" {}
        _RockyTex ("Rocky", 2D) = "white" {}
        _SnowyTex ("Snowy", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _GrassTex;
        sampler2D _RockyTex;
        sampler2D _SnowyTex;

        struct Input
        {
            float2 uv_GrassTex;
			float4 weights : COLOR;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 rocky = tex2D (_RockyTex, IN.uv_GrassTex);
            fixed4 snowy = tex2D (_SnowyTex, IN.uv_GrassTex);
            fixed4 grass = tex2D (_GrassTex, IN.uv_GrassTex);
			float4 w = IN.weights;
            o.Albedo = snowy.rgb*w.b+rocky.rgb*w.g+grass.rgb*w.r;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
