using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using Xarphos.Scripts;

public class PhospheneSimulator : MonoBehaviour
{
    // Simulation Parameters
    [SerializeField]
    // The factor by which stimulation accumulates to phosphene activation
    private float inputEffect = 0.7f;
    [SerializeField]
    // The factor by which previous activation still influences current activation
    private float intensityDecay = 0.8f;
    [SerializeField]
    // The habituation strength: the factor by which stimulation leads to buildup of memory trace
    private float traceIncrease = 0.1f;
    [SerializeField]
    // The factor by which the stimulation memory trace decreases
    private float traceDecay = 0.9f;

    // Materials & Shaders
    public Material focusDotMaterial;
    public Material edgeDetectionMaterial;
    public ComputeShader simulationCS;
    // compute shader properties 
    private int kernelActivations, kernelSpread, kernelClean;
    private int threadX, threadY, nThreadPhospheneSim;
    [SerializeField] private SurfaceReplacement.ReplacementModes currentSurfaceReplacement;

    // Phosphene Configs
    [Header("Phosphene Config")]
    public bool initialiseFromFile;
    [SerializeField] private string phospheneConfigFile;
    private PhospheneConfig phospheneCfg;
    private int nPhosphenes;
    private ComputeBuffer phospheneBuffer;
    
    // Simulation
    private bool runEdgeDetection, runSimulation;
    private enum GazeTrackingCondition { GazeIgnored, GazeLocked, GazeAssisted }
    [SerializeField] private GazeTrackingCondition currentCondition = GazeTrackingCondition.GazeIgnored;
    private RenderTexture activationTex, simulationRenderTex;
    
    // necessary references
    internal Camera targetCamera;

    private bool headsetInitialised = false;
    
    // Shader Property Mapping
    private static readonly int ShPrScreenResolution = Shader.PropertyToID("resolution");
    private static readonly int ShPrInputTex = Shader.PropertyToID("InputTex");
    private static readonly int ShPrActivationTex = Shader.PropertyToID("ActivationTex");
    private static readonly int ShPrSimulationRenderTex = Shader.PropertyToID("SimulationTex");
    private static readonly int ShPropEyePosLeft = Shader.PropertyToID("_EyePositionLeft");
    private static readonly int ShPropEyePosRight = Shader.PropertyToID("_EyePositionRight");
    private static readonly int ShPrInputEffect = Shader.PropertyToID("input_effect");
    private static readonly int ShPrIntensityDecay = Shader.PropertyToID("intensity_decay");
    private static readonly int ShPrTraceIncrease = Shader.PropertyToID("trace_increase");
    private static readonly int ShPrTraceDecay = Shader.PropertyToID("trace_decay");
    private static readonly int ShPrPhospheneBuffer = Shader.PropertyToID("phosphenes");
    private static readonly int ShPrGazeLocked = Shader.PropertyToID("phosphenes");
    private static readonly int ShPrGazeAssisted = Shader.PropertyToID("phosphenes");
    private static readonly int ShPrDoRenderDot = Shader.PropertyToID("do_render_dot");


    private void Awake()
    {
        targetCamera ??= GetComponent<Camera>();
        
        // Extract kernel IDs from compute shader
        kernelActivations = simulationCS.FindKernel("CalculateActivations");
        kernelSpread = simulationCS.FindKernel("SpreadActivations");
        kernelClean = simulationCS.FindKernel("CleanActivations");
        
        // Initialize the array of phosphenes
        if (initialiseFromFile && phospheneConfigFile != null)
        {
            try
            {
                phospheneCfg = PhospheneConfig.InitPhosphenesFromJSON(phospheneConfigFile);
            } catch (FileNotFoundException){ }
        }
        // if boolean is false, the file path is not given or the initialising from file failed, initialise probabilistic
        phospheneCfg ??= PhospheneConfig.InitPhosphenesProbabilistically(1000, .3f, PhospheneConfig.Monopole);
        nPhosphenes = phospheneCfg.phosphenes.Length;
        phospheneBuffer = new ComputeBuffer(nPhosphenes, sizeof(float)*7);
        phospheneBuffer.SetData(phospheneCfg.phosphenes);
        simulationCS.SetBuffer(kernelActivations, ShPrPhospheneBuffer, phospheneBuffer);
        
        // Set the compute shader with the temporal dynamics variables
        simulationCS.SetFloat(ShPrInputEffect, inputEffect);
        simulationCS.SetFloat(ShPrIntensityDecay, intensityDecay);
        simulationCS.SetFloat(ShPrTraceIncrease, traceIncrease);
        simulationCS.SetFloat(ShPrTraceDecay, traceDecay);
        // Set the default EyeTrackingCondition (Ignore Gaze)
        simulationCS.SetInt(ShPrGazeLocked, 0);
        simulationCS.SetInt(ShPrGazeAssisted, 0);
        
        focusDotMaterial.SetInt(ShPrDoRenderDot, 1);
    }

    private void Update()
    {
        // cycle surface replacement on 1
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            var nMax = Enum.GetNames(typeof(SurfaceReplacement.ReplacementModes)).Length;
            currentSurfaceReplacement = (SurfaceReplacement.ReplacementModes)(((int)currentSurfaceReplacement + 1) % nMax);
            SurfaceReplacement.ActivateReplacementShader(targetCamera, currentSurfaceReplacement);
        }
        // toggle edge detection on 2
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            runEdgeDetection = !runEdgeDetection;
        }
        // toggle phosphene simulation on 3
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            runSimulation = !runSimulation;
        }
        // cycle experiment conditions on 4
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            var nMax = Enum.GetNames(typeof(GazeTrackingCondition)).Length;
            currentCondition = (GazeTrackingCondition)(((int)currentCondition + 1) % nMax);
            switch (currentCondition)
            {
                case GazeTrackingCondition.GazeIgnored:
                    simulationCS.SetInt(ShPrGazeLocked, 0);
                    simulationCS.SetInt(ShPrGazeAssisted, 0);
                    break;
                case GazeTrackingCondition.GazeLocked:
                    simulationCS.SetInt(ShPrGazeLocked, 1);
                    break;
                case GazeTrackingCondition.GazeAssisted:
                    simulationCS.SetInt(ShPrGazeAssisted, 1);
                    break;
            }
        }
    }

    public void SetEyePosition(Vector2 eyeL, Vector2 eyeR, Vector2 center)
    {
        focusDotMaterial.SetVector(ShPropEyePosLeft, eyeL);
        focusDotMaterial.SetVector(ShPropEyePosRight, eyeR);
          
        simulationCS.SetVector(ShPropEyePosLeft, eyeL);
        simulationCS.SetVector(ShPropEyePosRight, eyeR);
    }
    
    private void InitialiseTextures(RenderTexture src)
    {
      if (headsetInitialised || XRSettings.eyeTextureWidth == 0) return;
      
      headsetInitialised = true;
        
      var w = XRSettings.eyeTextureWidth;
      var h = XRSettings.eyeTextureHeight;
      edgeDetectionMaterial.SetVector(ShPrScreenResolution, new Vector4(w, h, 0, 0));
      Debug.Log($"Set Res to: {w}, {h}");

        
      // Initialize the render textures & Set the shaders with the shared render textures
      simulationCS.SetInts(ShPrScreenResolution, w, h);
      activationTex = new RenderTexture(src.descriptor)
      {
          depth = 0,
          enableRandomWrite = true
      };
      activationTex.Create();
      simulationCS.SetTexture(kernelActivations, ShPrActivationTex, activationTex);
      simulationCS.SetTexture(kernelSpread, ShPrActivationTex, activationTex);
      simulationCS.SetTexture(kernelClean, ShPrActivationTex, activationTex);
      
      simulationRenderTex = new RenderTexture(src.descriptor)
      {
          enableRandomWrite = true
      };
      activationTex.Create();
      simulationCS.SetTexture(kernelActivations, ShPrSimulationRenderTex, simulationRenderTex);
      simulationCS.SetTexture(kernelSpread, ShPrSimulationRenderTex, simulationRenderTex);
      simulationCS.SetTexture(kernelClean, ShPrSimulationRenderTex, simulationRenderTex);
      
      simulationCS.GetKernelThreadGroupSizes(kernelActivations, out var xGroup, out _, out _);
      nThreadPhospheneSim = Mathf.CeilToInt(nPhosphenes / xGroup);
      simulationCS.GetKernelThreadGroupSizes(kernelSpread, out xGroup, out var yGroup, out _);
      threadX = Mathf.CeilToInt(w / xGroup);
      threadY = Mathf.CeilToInt(h / yGroup);

      var (lViewSpace, rViewSpace, cViewSpace) = EyePosFromScreenPoint(0.5f, 0.5f);
      SetEyePosition(lViewSpace, rViewSpace, cViewSpace);
      simulationCS.SetVector("_LeftEyeCenter", lViewSpace);
      simulationCS.SetVector("_RightEyeCenter", rViewSpace);
    }
    
    private (Vector2, Vector2, Vector2) EyePosFromScreenPoint(float x, float y)
    {
        // set eye position to centre of screen and calculate correct offsets
        var P = targetCamera.ViewportToWorldPoint(
            new Vector3(x, y, 10f)); 
        // projection from local space to clip space
        var lMat = targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Left);
        var rMat = targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Right);
        var cMat = targetCamera.nonJitteredProjectionMatrix;
        // projection from world space into local space
        var world2cam = targetCamera.worldToCameraMatrix;
        // 4th dimension necessary in graphics to get scale
        var P4d = new Vector4(P.x, P.y, P.z, 1f); 
        // point in world space * world2cam -> local space point
        // local space point * projection matrix = clip space point
        var lProjection = lMat * world2cam * P4d;
        var rProjection = rMat * world2cam * P4d;
        var cProjection = cMat * world2cam * P4d;
        // scale and shift from clip space [-1,1] into view space [0,1]
        var lViewSpace = (new Vector2(lProjection.x, lProjection.y) / lProjection.w) * .5f + .5f * Vector2.one;
        var rViewSpace = (new Vector2(rProjection.x, rProjection.y) / rProjection.w) * .5f + .5f * Vector2.one;
        var cViewSpace = (new Vector2(cProjection.x, cProjection.y) / cProjection.w) * .5f + .5f * Vector2.one;
          
        return (lViewSpace, rViewSpace, cViewSpace);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    { 
        InitialiseTextures(src);
        if (dst == null || !headsetInitialised) return;

        // in between texture to put processed image on before blitting from this to target
        var preTargetPing = RenderTexture.GetTemporary(dst.descriptor);
        // if phosphene simulator is off, only need to run image through image processing for edge detection

        if (runEdgeDetection)
            Graphics.Blit(src, preTargetPing, edgeDetectionMaterial);
        // if edge detection is off, just blit without any processing
        else
            Graphics.Blit(src, preTargetPing);

        if (runSimulation)
        {
            simulationCS.SetTexture(kernelActivations,ShPrInputTex, preTargetPing);

            // var activationTex = RenderTexture.GetTemporary(dst.descriptor);
            // activationTex.depth = 0;
            // activationTex.enableRandomWrite = true;
            // simulationCS.SetTexture(kernelActivations, ShPrActivationTex, activationTex);
            // simulationCS.SetTexture(kernelSpread, ShPrActivationTex, activationTex);
            
            // var phospheneRenderTexture = RenderTexture.GetTemporary(dst.descriptor);
            // phospheneRenderTexture.enableRandomWrite = true;
            // simulationCS.SetTexture(kernelActivations, ShPrSimulationRenderTex, phospheneRenderTexture);
            // simulationCS.SetTexture(kernelSpread, ShPrSimulationRenderTex, phospheneRenderTexture);

            // calculate activations
            simulationCS.Dispatch(kernelActivations, nThreadPhospheneSim, 1, 1);
            // render phosphene simulation
            simulationCS.Dispatch(kernelSpread, threadX, threadY, 1);

            Graphics.Blit(simulationRenderTex, preTargetPing, new Vector2(1, -1), Vector2.zero);
            // RenderTexture.ReleaseTemporary(phospheneRenderTexture);
            simulationCS.Dispatch(kernelClean, threadX, threadY, 1);
        }

        // lastly render the focus dot on top
        Graphics.Blit(preTargetPing, dst, focusDotMaterial);

        RenderTexture.ReleaseTemporary(preTargetPing);
    }
    
    private void OnDestroy(){
        phospheneBuffer.Release();
    }
}
