Shader "Custom/Gound"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
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

		sampler2D _HeightStripsTexture;

        struct Input
        {
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
			//modNormal.y *= 0.35;
			//modNormal = normalize( modNormal );
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
			float4 w = IN.weights;
			o.Albedo = fixed3(1,1,1) * w.b + fixed3(0.5, 0.5, 0.45) * w.g + fixed3(0.26, 0.28, 0.17) * w.r + fixed3(0.35, 0.25, 0.15) * w.a;
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
