// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "DisplayBlitResult2" {
    Properties {
		_MainTex("MainTex", 2D) = "white" {}
	_stencil_is_on("StencilIsOn", 2D) = "white" {}
	//_StencilTempTexture ("StencilTempTexture", 2D) = "white" {}
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "RenderType"="Opaque"
        }
        LOD 100
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            Cull Off
            ZTest Always
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDBASE
            #include "UnityCG.cginc"
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            uniform sampler2D _StencilTempTexture; uniform float4 _StencilTempTexture_ST;
			uniform sampler2D _stencil_is_on; uniform float4 _stencil_is_on_ST;
            struct VertexInput {
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.pos = UnityObjectToClipPos(v.vertex );
                return o;
            }
            float4 frag(VertexOutput i, float facing : VFACE) : COLOR {
                float isFrontFace = ( facing >= 0 ? 1 : 0 );
                float faceSign = ( facing >= 0 ? 1 : -1 );
////// Lighting:
				float4 _StencilTempTexture_var = tex2D(_StencilTempTexture, TRANSFORM_TEX(i.uv0, _StencilTempTexture));
				float4 _stencil_is_on_var = tex2D(_stencil_is_on, TRANSFORM_TEX(i.uv0, _stencil_is_on));

				float3 finalColor = (_StencilTempTexture_var.r > 0.99) ? _stencil_is_on_var.rgb : _StencilTempTexture_var.rgb;
                return fixed4(finalColor,1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
