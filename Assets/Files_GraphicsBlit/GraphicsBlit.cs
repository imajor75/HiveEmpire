using UnityEngine;
using System.Collections;

public class GraphicsBlit : MonoBehaviour
{
    private Material PostprocessMaterial;
    public Material stencil_is_on;

    private RenderTexture StencilTempTexture;
    private RenderTexture CameraRenderTexture;
    private RenderTexture Buffer;

    public void Start()
    {

        PostprocessMaterial = new Material(Shader.Find("StencilToBlackAndWhite"));
        
        CameraRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        StencilTempTexture = new RenderTexture(Screen.width, Screen.height, 24);
        Buffer = new RenderTexture(Screen.width, Screen.height, 24);

        Camera.main.targetTexture = CameraRenderTexture;
    }

    void OnPostRender()
    {

        Graphics.SetRenderTarget(StencilTempTexture);
        GL.Clear(true, true, Color.black);

        Graphics.SetRenderTarget(StencilTempTexture.colorBuffer, CameraRenderTexture.depthBuffer);
        Graphics.Blit(CameraRenderTexture, PostprocessMaterial);

        stencil_is_on.SetTexture("_StencilTempTexture", StencilTempTexture);

        Graphics.SetRenderTarget(Buffer.colorBuffer, CameraRenderTexture.depthBuffer);
        RenderTexture.active = null;
        Graphics.Blit(CameraRenderTexture, stencil_is_on);


    }
}