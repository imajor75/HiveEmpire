using UnityEngine;
using UnityEngine.Rendering;

public class CommandBufferStencil : MonoBehaviour
{
    private CommandBuffer cmdBuffer;
    private CommandBuffer cmdBuffer2;
    public Material stencil_is_on_mat;
    
    int texID;

    void OnEnable()
    {

        //**********************************************************************************************
        //First CommandBuffer: Save earlier screen's rendering to _OlderTexture
        //**********************************************************************************************
        if (cmdBuffer == null)
        {
            cmdBuffer = new CommandBuffer();
            cmdBuffer.name = "cmdBuffer";
            texID = Shader.PropertyToID("_OlderTexture");
            cmdBuffer.GetTemporaryRT(texID, -1, -1, 0);
            cmdBuffer.Blit(BuiltinRenderTextureType.CameraTarget, texID);

            //NOTE NOTE NOTE: THe following three lines of code need to be DELETED
            //As soon as Unity fixes the bug that causes .blit() to flip the whole
            //picture upside down. I assume they will fix this soon. These next three
            //lines simply flip the image back right-side-up. But once they fix this
            //bug, then these lines will be flipping the image up-side-down again!
            //So when that happens, delete these three lines and problem is solved.
            cmdBuffer.Blit(texID, BuiltinRenderTextureType.CameraTarget);
            cmdBuffer.Blit(BuiltinRenderTextureType.CameraTarget, texID);
            cmdBuffer.Blit(texID, BuiltinRenderTextureType.CameraTarget);


            cmdBuffer.SetGlobalTexture("_OlderTexture", texID);
            Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cmdBuffer);

            //The above code will save a screenshot BEFORE transparent objects are rendered.
            //Transparent objects are designated in their shader by: tags{"Queue"="Transparent"}
            //The wall of text that says "stencil", and the bottom spinning cube, are both transparent.
            //The upper spinning cube is opaque, so it is a part of this earlier screenshot.
            //But since the lower spinning cube is using a transparent shader, it's NOT part of this screenshot.
            //The stencil wall is also transparent, so it's not part of this screenshot either.
            //The stencil wall is also writing to the stencil buffer.
        }


        //**********************************************************************************************
        //Second CommandBuffer: Do something with earlier save screen texture...
        //**********************************************************************************************
        if (cmdBuffer2 == null)
        {
            cmdBuffer2 = new CommandBuffer();
            cmdBuffer2.name = "cmdBuffer2";
            cmdBuffer2.Blit(BuiltinRenderTextureType.None, BuiltinRenderTextureType.None, stencil_is_on_mat);
            Camera.main.AddCommandBuffer(CameraEvent.AfterEverything, cmdBuffer2);

            //The above code will act upon the earlier screenshot.
            //What's happening is several things:
            //First, it runs the shader stencil_is_on_mat, which ONLY works on the stencil pixels.
            //The wall is writing to the stencil buffer, so it only works on pixels affected by the wall.
            //So on all the wall pixels, it draws the image taken in the earlier screenshot.
            //This means that on the WALL side of the screen, all transparent objects disappear, beacuse
            //they wren't part of the earlier screenshot.
            //But on the other side of the screen, everything is visible, since again, the effect is only
            //being applied to the stencil pixels, and those are being drawn by the wall.
        }

    }

    void OnDisable()
    {
        if (cmdBuffer != null)
            Camera.main.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, cmdBuffer);
        cmdBuffer = null;
        if (cmdBuffer2 != null)
            Camera.main.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, cmdBuffer2);
        cmdBuffer2 = null;
    }
}