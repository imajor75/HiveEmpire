Shader "Unlit/HighloightVolume"
{
	SubShader
	{
		Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
		LOD 100
		ZWrite Off

		Pass
		{
			ColorMask Off
			Cull Back
			Stencil
			{
				Ref 1
				Comp Always
				Pass DecrWrap
			}
		}
		Pass
		{
			ColorMask Off
			Cull Front
			Stencil
			{
				Ref 1
				Comp Always
				Pass IncrWrap
			}
		}
	}
}
