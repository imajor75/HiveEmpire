Shader "Custom/Ground"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
		_GrassTex("Grass", 2D) = "white" {}
		_DuffTex("Duff", 2D) = "white" {}
		_RockTex("Rock", 2D) = "white" {}
		_SnowTex("Snow", 2D) = "white" {}
		_GridTex("Grid", 2D) = "white" {}
		_HeightStripsTexture("Height Strips Texture", 2D) = "white" {}
		_HeightStrips ( "Height Strips", Int) = 0
		_HeightMin ("Height Min", Float) = 0
		_HeightMax ("Height Max", Float) = 10
		_GridStartX("Grid Start X", Float) = 0
		_GridStartZ("Grid Start Z", Float) = 0
		_GridFactorX("Grid Factor X", Float) = 1
		_GridFactorZ("Grid Factor Z", Float) = 0.5
		_GridMaskTex("Grid Mask", 2D) = "white" {}
		_GridMaskX("Grid Mask X", Float) = 0
		_GridMaskZ("Grid Mask Z", Float) = 0
		_GridMaskFactor("Grid Mask Factor", Float) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf SimpleLambert

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

		sampler2D _GrassTex;
		sampler2D _DuffTex;
		sampler2D _RockTex;
		sampler2D _SnowTex;
		sampler2D _GridTex;
		sampler2D _GridMaskTex;
		sampler2D _HeightStripsTexture;

        struct Input
        {
			float4 weights : COLOR;
			float3 worldPos;
		};

		bool _HeightStrips;
		float _HeightMin;
		float _HeightMax;
		float _GridStartX;
		float _GridStartZ;
		float _GridFactorX;
		float _GridFactorZ;
		float _GridMaskFactor;
		float _GridMaskX;
		float _GridMaskZ;
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
			fixed3 grass = tex2D(_GrassTex, IN.worldPos.xz / 3);
			fixed3 duff = tex2D(_DuffTex, IN.worldPos.xz);
			fixed3 rock = tex2D(_RockTex, IN.worldPos.xz);
			fixed3 snow = tex2D(_SnowTex, IN.worldPos.xz);
			float4 w = IN.weights;
			o.Albedo = snow * w.b + rock * w.g + grass * w.r + duff * w.a;
			if ( _HeightStrips )
			{
				float height = (IN.worldPos.y - _HeightMin) / (_HeightMax - _HeightMin);
				fixed4 stripes = tex2D(_HeightStripsTexture, float2(0.4, height));
				o.Albedo = lerp(o.Albedo, stripes.rgb, stripes.a);
			}

			fixed grid = tex2D(_GridTex, IN.worldPos.xz * fixed2(_GridFactorX, _GridFactorZ) + fixed2(_GridStartX, _GridStartZ));
			fixed gridMask = tex2D(_GridMaskTex, (IN.worldPos.xz - fixed2(_GridMaskX, _GridMaskZ)) * _GridMaskFactor + fixed2(0.5, 0.5));
			o.Albedo = lerp(o.Albedo, fixed3(1, 1, 1), grid*gridMask*0.5);

			o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
