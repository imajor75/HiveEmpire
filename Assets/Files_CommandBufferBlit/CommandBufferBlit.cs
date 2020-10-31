using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

public class CommandBufferBlit : MonoBehaviour
{
    private Material PostprocessMaterial;
    public Material stencil_is_on;

    private RenderTexture StencilTempTexture;
    private RenderTexture CameraRenderTexture;
    private RenderTexture Buffer;

    private RenderTargetIdentifier StencilTempTextureID;
    private RenderTargetIdentifier CameraRenderTextureID;
    private RenderTargetIdentifier BufferID;

    private CommandBuffer cmdBuffer;

    public void Start()
    {

        PostprocessMaterial = new Material(Shader.Find("StencilToBlackAndWhite"));
        
        CameraRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        StencilTempTexture = new RenderTexture(Screen.width, Screen.height, 24);
        Buffer = new RenderTexture(Screen.width, Screen.height, 24);

        CameraRenderTextureID = new RenderTargetIdentifier(CameraRenderTexture);
        StencilTempTextureID = new RenderTargetIdentifier(StencilTempTexture);
        BufferID = new RenderTargetIdentifier(Buffer);

        Camera.main.targetTexture = CameraRenderTexture;
    }

    //void OnPostRender()
    //{
    void OnEnable()
    {
        if (cmdBuffer == null)
        {
            cmdBuffer = new CommandBuffer();
            cmdBuffer.name = "cmdBuffer";

            cmdBuffer.SetRenderTarget(StencilTempTextureID); //    Graphics.SetRenderTarget(StencilTempTexture);
            cmdBuffer.ClearRenderTarget(true, true, Color.black); //    GL.Clear(true, true, Color.black);

            cmdBuffer.SetRenderTarget(StencilTempTextureID, CameraRenderTextureID); //    Graphics.SetRenderTarget(StencilTempTexture.colorBuffer, CameraRenderTexture.depthBuffer);
            cmdBuffer.Blit(CameraRenderTextureID, BuiltinRenderTextureType.None, PostprocessMaterial); //    Graphics.Blit(CameraRenderTexture, PostprocessMaterial);

            cmdBuffer.SetGlobalTexture("_StencilTempTexture", StencilTempTexture); //    stencil_is_on.SetTexture("_StencilTempTexture", StencilTempTexture);

            cmdBuffer.SetRenderTarget(BufferID, CameraRenderTextureID); //    Graphics.SetRenderTarget(Buffer.colorBuffer, CameraRenderTexture.depthBuffer);
            cmdBuffer.Blit(CameraRenderTextureID, BuiltinRenderTextureType.None, stencil_is_on); //    Graphics.Blit(CameraRenderTexture, stencil_is_on);

            GetComponent<Camera>().AddCommandBuffer(CameraEvent.BeforeImageEffects, cmdBuffer); 

        }
    }
    //}
}