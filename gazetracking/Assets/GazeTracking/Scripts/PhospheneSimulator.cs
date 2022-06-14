using System;
using System.Collections.Generic;
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

    private RenderTexture simRT, cutOutRT;

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
                var eyeDesc = XRSettings.eyeTextureDesc;
                
                screenResolution = new Vector2Int(screenW, screenH);
                cutOutResolution = screenResolution / 3;
                simulationRenderResolution = cutOutResolution / 4;
                centerRectPos = (screenResolution / 2) - (cutOutResolution / 2);

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

                simRT = new RenderTexture(simulationRenderDescriptor);
                simRT.Create();
                cutOutRT = new RenderTexture(cutOutTextureDescriptor);
                cutOutRT.Create();
                
                headsetInitialised = true;
            }
        }
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

            Graphics.Blit(src, cutOutRT);
            Graphics.Blit(cutOutRT, simRT);
        }
        else
        {
            Graphics.Blit(src, dst, focusDotMaterial);
        }
    }

    private void OnPostRender()
    {
        Graphics.Blit(simRT, null as RenderTexture);
    }
}
