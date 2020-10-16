Shader "Custom/Gound"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _GrassTex ("Grass", 2D) = "white" {}
        _RockyTex ("Rocky", 2D) = "white" {}
		_SnowyTex("Snowy", 2D) = "white" {}
		_HeightStripsTexture("Height Strips Texture", 2D) = "white" {}
		_HeightStrips ( "Height Strips", Int) = 0
		_HeightMin ("Height Min", Float) = 0
		_HeightMax ("Height Max", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf SimpleLambert fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _GrassTex;
        sampler2D _RockyTex;
        sampler2D _SnowyTex;
		sampler2D _HeightStripsTexture;

        struct Input
        {
            float2 uv_GrassTex;
			float4 weights : COLOR;
			float3 worldPos;
		};

		bool _HeightStrips;
		float _HeightMin;
		float _HeightMax;
        fixed4 _Color;

		half4 LightingSimpleLambert(SurfaceOutput s, half3 lightDir, half atten) 
		{
			half3 modNormal = s.Normal;
			modNormal.y *= 0.35;
			modNormal = normalize( modNormal );
			half NdotL = dot(modNormal, lightDir);
			half4 c;
			c.rgb = s.Albedo * _LightColor0.rgb * (NdotL * atten);
			c.a = s.Alpha;
			return c;
		}

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 rocky = tex2D (_RockyTex, IN.uv_GrassTex);
            fixed4 snowy = tex2D (_SnowyTex, IN.uv_GrassTex);
            fixed4 grass = tex2D (_GrassTex, IN.uv_GrassTex);
			float4 w = IN.weights;
            o.Albedo = snowy.rgb*w.b+rocky.rgb*w.g+grass.rgb*w.r;
			if ( _HeightStrips )
			{
				float height = (IN.worldPos.y - _HeightMin) / (_HeightMax - _HeightMin);
				fixed4 stripes = tex2D(_HeightStripsTexture, float2(0.4, height));
				o.Albedo = lerp(o.Albedo, stripes.rgb, stripes.a);
			}
			o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
