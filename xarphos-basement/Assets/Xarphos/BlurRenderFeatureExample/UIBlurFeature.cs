using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class UIBlurFeature : ScriptableRendererFeature
{
    public enum BlurType
    {
        Gaussian,
        Box,
    }

    const string TextureName = "_UIBlurTexture";
    static int DownscaleKernelID = -1;
    static int GaussianBlurKernelID = -1;
    static int BoxBlurKernelID = -1;

    [System.Serializable]
    public class Settings
    {
        [Range(1, 16)] public int DownscaleAmount = 2;

        public BlurType BlurType;
        [Range(0, 16)] public int BlurIterations = 4;

        public ComputeShader EffectCompute;
    }

    class BlurSceneColorPass : ScriptableRenderPass
    {
        RenderTargetHandle tempColorTarget;
        Settings settings;

        RenderTargetIdentifier cameraTarget;

        Vector2Int scale;
        Vector2Int groupSizes;

        public BlurSceneColorPass(Settings s)
        {
            settings = s;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            tempColorTarget.Init(TextureName);
        }

        public void Setup(RenderTargetIdentifier cameraTarget)
        {
            this.cameraTarget = cameraTarget;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var downscaleDesc = cameraTextureDescriptor;
            downscaleDesc.width = (int) ((float) downscaleDesc.width / (float) settings.DownscaleAmount);
            downscaleDesc.height = (int) ((float) downscaleDesc.height / (float) settings.DownscaleAmount);
            downscaleDesc.enableRandomWrite = true;

            scale = new Vector2Int(downscaleDesc.width, downscaleDesc.height);
            groupSizes = new Vector2Int(Mathf.CeilToInt(scale.x / 32f), Mathf.CeilToInt(scale.y / 32f));

            cmd.GetTemporaryRT(tempColorTarget.id, downscaleDesc);

            cmd.SetGlobalTexture(TextureName, tempColorTarget.Identifier());
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // Shared parameters between compute kernels
            cmd.SetComputeVectorParam(settings.EffectCompute, "_Size", new Vector2(scale.x, scale.y));
            cmd.SetComputeFloatParam(settings.EffectCompute, "_Amount", settings.DownscaleAmount);

            // Blit/Downscale scene color texture
            {
                cmd.SetComputeTextureParam(settings.EffectCompute, DownscaleKernelID, "_Source", cameraTarget);
                cmd.SetComputeTextureParam(settings.EffectCompute, DownscaleKernelID, "_Dest",
                    tempColorTarget.Identifier());
                cmd.DispatchCompute(settings.EffectCompute, DownscaleKernelID, groupSizes.x, groupSizes.y, 1);
            }

            // Apply iterative blur
            {
                int blurKernelID = settings.BlurType switch
                {
                    BlurType.Box => BoxBlurKernelID,
                    BlurType.Gaussian => GaussianBlurKernelID,
                };

                cmd.SetComputeTextureParam(settings.EffectCompute, blurKernelID, "_Dest", tempColorTarget.Identifier());
                cmd.SetComputeTextureParam(settings.EffectCompute, blurKernelID + 1, "_Dest", tempColorTarget.Identifier());
                
                for (int i = 0; i < settings.BlurIterations; i++)
                {
                    cmd.DispatchCompute(settings.EffectCompute, blurKernelID, groupSizes.x, groupSizes.y, 1);
                    if (settings.BlurType == BlurType.Gaussian)
                    {
                        cmd.DispatchCompute(settings.EffectCompute, blurKernelID + 1, groupSizes.x, groupSizes.y, 1);
                    }
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(tempColorTarget.id);
        }
    }

    BlurSceneColorPass blurSceneColorPass;
    [SerializeField] Settings settings;

    public override void Create()
    {
        blurSceneColorPass = new BlurSceneColorPass(settings);

        FindKernels();
    }

    void FindKernels()
    {
        if (settings.EffectCompute != null)
        {
            DownscaleKernelID = settings.EffectCompute.FindKernel("Downscale");
            GaussianBlurKernelID = settings.EffectCompute.FindKernel("GaussianBlurVertical");
            BoxBlurKernelID = settings.EffectCompute.FindKernel("BoxBlur");
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.EffectCompute == null)
        {
            return;
        }

        if (DownscaleKernelID == -1 || GaussianBlurKernelID == -1)
        {
            FindKernels();
        }

        blurSceneColorPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(blurSceneColorPass);
    }
}