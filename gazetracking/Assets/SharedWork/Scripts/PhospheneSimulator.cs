using System;
using Unity.XR.OpenVR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

public class PhospheneSimulator : MonoBehaviour
{
    public Material focusDotMaterial;

    private bool cutOut = false;
    
    internal Camera targetCamera;

    private bool headsetInitialised = false;
    private Vector2Int screenResolution;
    private Vector2Int cutOutResolution;
    private Vector2Int simulationRenderResolution;
    private Vector2Int centerRectPos;
    private RenderTextureDescriptor cutOutTextureDescriptor, simulationRenderDescriptor;

    public RenderTexture cutOutRT, simulationRT, cameraRT;

    private void Awake()
    {
        targetCamera ??= GetComponent<Camera>();
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            cutOut = !cutOut;
        }

        if (!headsetInitialised)
        {
            var screenW = XRSettings.eyeTextureWidth;
            var screenH = XRSettings.eyeTextureHeight;
            if ((screenW != 0) && (screenH != 0))
            {
                screenResolution = new Vector2Int(screenW, screenH);
                cutOutResolution = screenResolution / 3;
                simulationRenderResolution = cutOutResolution / 4;
                centerRectPos = (screenResolution / 2) - (cutOutResolution / 2);

                var eyeDesc = XRSettings.eyeTextureDesc;
                cutOutTextureDescriptor = 
                    new RenderTextureDescriptor(cutOutResolution.x, cutOutResolution.y)
                {
                    depthBufferBits = eyeDesc.depthBufferBits,
                    volumeDepth = eyeDesc.volumeDepth,
                    colorFormat = eyeDesc.colorFormat,
                    graphicsFormat = eyeDesc.graphicsFormat,
                    vrUsage = eyeDesc.vrUsage,
                    enableRandomWrite = true
                };

                simulationRenderDescriptor = 
                    new RenderTextureDescriptor(simulationRenderResolution.x, simulationRenderResolution.y)
                {
                    depthBufferBits = eyeDesc.depthBufferBits,
                    volumeDepth = eyeDesc.volumeDepth,
                    colorFormat = eyeDesc.colorFormat,
                    graphicsFormat = eyeDesc.graphicsFormat,
                    vrUsage = eyeDesc.vrUsage,
                    enableRandomWrite = true
                };

                cutOutRT = new RenderTexture(cutOutTextureDescriptor)
                {
                    filterMode = FilterMode.Point
                };
                cutOutRT.Create();
                simulationRT = new RenderTexture(simulationRenderDescriptor)
                {
                    filterMode = FilterMode.Point
                };
                simulationRT.Create();
                
                headsetInitialised = true;
            }
        }
    }

    private void OnDestroy()
    {
        cutOutRT.Release();
        simulationRT.Release();
    }

    public void SetEyePosition(Vector2 eyeL, Vector2 eyeR, Vector2 center)
    {
        focusDotMaterial.SetVector("_LeftEyePos", eyeL);
        focusDotMaterial.SetVector("_RightEyePos", eyeR);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // skip frames in which headset is not initialised
        if (dst == null) return;
        
        if (cutOut)
        {
            var xPos = centerRectPos.x;
            var yPos = centerRectPos.y;
            var w = cutOutResolution.x;
            var h = cutOutResolution.y;

            var rt = RenderTexture.GetTemporary(cutOutTextureDescriptor);
            Graphics.CopyTexture(src, 0, 0, xPos, yPos, w, h,
                cutOutRT, 0, 0, 0, 0);
            Graphics.Blit(cutOutRT, simulationRT);
            Graphics.Blit(src, dst);
        }
        else
        {
            Graphics.Blit(src, dst, focusDotMaterial);
        }
    }
}
