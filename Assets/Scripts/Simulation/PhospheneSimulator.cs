using System;
using System.IO;
using ExperimentControl;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Xarphos.Scripts;

namespace Simulation
{
    public class PhospheneSimulator : MonoBehaviour
    {
      [Header("Rendering & Eye Position Debugging")]
        public Camera targetCamera;
        public bool setManualEyePos;
        public Vector2 manualEyePos = new (.5f, .5f);
        
        // stimulation parameters
        [Header("Simulation Parameters")]
        [SerializeField]
        private float inputEffect = 0.7f;// The factor by which stimulation accumulates to phosphene activation
        [SerializeField]
        private float intensityDecay = 0.8f; // The factor by which previous activation still influences current activation
        [SerializeField]
        private float traceIncrease = 0.1f; // The habituation strength: the factor by which stimulation leads to buildup of memory trace
        [SerializeField]
        private float traceDecay = 0.9f; // The factor by which the stimulation memory trace decreases
        [SerializeField] 
        private float relativeRFSize = 4.0f; // the size of the 'receptive field' (sampling area) relative to the phosphene size 
        
        // Image processing settings
        private float runSimulation = 0;
        private bool runEdgeDetection = false;
        private int renderFocusDot = 0;
        
        // protected RenderTextureDescriptor ActvTexDesc;
        private RenderTexture actvTex, simRenderTex;
        [SerializeField] protected SurfaceReplacement.ReplacementModes surfaceReplacementMode;
        private readonly int nSurfaceModes = Enum.GetValues(typeof(SurfaceReplacement.ReplacementModes)).Length;

        // For reading phosphene configuration from JSON
        [Header("Phosphene Initialisation")]
        [SerializeField] private bool initialiseFromFile;
        [SerializeField] private string phospheneConfigFile;
        [Header("Dynamic Phosphene Generation")]
        [SerializeField] private int numberOfPhosphenes = 1000;
        [SerializeField] private float maxEccentricityRadius = .15f;
        [Header("Debug: (Save Layout as image)")]
        [SerializeField] private bool debugPhoshpenes;
        private PhospheneConfig phospheneConfig;
        private int nPhosphenes;
        private ComputeBuffer phospheneBuffer;

        // Shaders and materials
        private Material imageProcessingMaterial;
        private Material focusDotMaterial;
        [SerializeField] protected Shader edgeDetectionShader;
        [SerializeField] protected ComputeShader simulationComputeShader;
        
        // Eye tracking
        private EyeTracking.EyeTrackingConditions gazeCondition;
        [NonSerialized]
        public UnityEvent<EyeTracking.EyeTrackingConditions> onChangeGazeCondition;
        protected EyeTracking.EyeTrackingConditions EyeTrackingCondition
        {
            get => gazeCondition;
            set
            {
              if (value == gazeCondition) return;
              gazeCondition = value;
              onChangeGazeCondition?.Invoke(value);
            }
        }
        private readonly int nEyeTrackingModes = Enum.GetValues(typeof(EyeTracking.EyeTrackingConditions)).Length;
        private Vector2 eyePosLeft, eyePosRight, eyePosCentre;
        
        // simulation auxiliaries
        private int kernelActivations, kernelSpread, kernelClean;
        private int threadX, threadY, threadPhosphenes;
        private bool headsetInitialised = false;
        
        // experiment refs
        public CollisionHandler boxChecker;
        public CheckpointHandler checkpointChecker;
        
        #region Shader Properties Name-To-Int
        // Rendering related
        private static readonly int ShPrInputTex = Shader.PropertyToID("InputTexture");
        private static readonly int ShPrActivationTex = Shader.PropertyToID("ActivationTexture");
        private static readonly int ShPrSimRenderTex = Shader.PropertyToID("SimulationRenderTexture");
        private static readonly int ShPrScreenResolution = Shader.PropertyToID("ScreenResolution");
      
        private static readonly int ShPrPhospheneBuffer = Shader.PropertyToID("phosphenes");
        private static readonly int ShPrRenderFocusDotToggle = Shader.PropertyToID("_RenderPoint");

        // Gaze Tracking Related
        private static readonly int ShPrLeftEyePos = Shader.PropertyToID("_EyePositionLeft");
        private static readonly int ShPrRightEyePos = Shader.PropertyToID("_EyePositionRight");
        private static readonly int ShPrLeftEyeCenter = Shader.PropertyToID("_LeftEyeCenter");
        private static readonly int ShPrRightEyeCenter = Shader.PropertyToID("_RightEyeCenter");
        private static readonly int ShPrGazeAssistedToggle = Shader.PropertyToID("gazeAssisted");
        private static readonly int ShPrGazeLockedToggle = Shader.PropertyToID("gazeLocked");

        // Simulation Related
        private static readonly int ShPrInputEffect = Shader.PropertyToID("input_effect");
        private static readonly int ShPrIntensityDecay = Shader.PropertyToID("intensity_decay");
        private static readonly int ShPrTraceIncrease = Shader.PropertyToID("trace_increase");
        private static readonly int ShPrTraceDecay = Shader.PropertyToID("trace_decay");
        private static readonly int ShPrRelaltiveRFSize = Shader.PropertyToID("_RelativeRFSize");
      
        #endregion

        protected void Awake()
        {
          targetCamera ??= GetComponent<Camera>();

          // Initialize the array of phosphenes
          if (initialiseFromFile && phospheneConfigFile != null)
          {
            try
            {
              phospheneConfig = PhospheneConfig.InitPhosphenesFromJSON(phospheneConfigFile);
            } catch (FileNotFoundException){ }
          }
          // if boolean is false, the file path is not given or the initialising from file failed, initialise probabilistic
          phospheneConfig ??= PhospheneConfig.InitPhosphenesProbabilistically(
            numberOfPhosphenes, maxEccentricityRadius, PhospheneConfig.Monopole, debugPhoshpenes);
          
          nPhosphenes = phospheneConfig.phosphenes.Length;
          phospheneBuffer = new ComputeBuffer(nPhosphenes, sizeof(float)*7);
          phospheneBuffer.SetData(phospheneConfig.phosphenes);

          // Initialize materials with shaders
          imageProcessingMaterial = new Material(edgeDetectionShader);
          
          // Set the compute shader with the temporal dynamics variables
          simulationComputeShader.SetFloat(ShPrInputEffect, inputEffect);
          simulationComputeShader.SetFloat(ShPrIntensityDecay, intensityDecay);
          simulationComputeShader.SetFloat(ShPrTraceIncrease, traceIncrease);
          simulationComputeShader.SetFloat(ShPrTraceDecay, traceDecay);
          simulationComputeShader.SetFloat(ShPrRelaltiveRFSize, relativeRFSize);

          simulationComputeShader.SetBuffer(0, ShPrPhospheneBuffer, phospheneBuffer);
          // Set the default EyeTrackingCondition (Ignore Gaze)
          simulationComputeShader.SetInt(ShPrGazeLockedToggle, 0);
          simulationComputeShader.SetInt(ShPrGazeAssistedToggle, 0);

          // get kernel references
          kernelActivations = simulationComputeShader.FindKernel("CalculateActivations");
          kernelSpread = simulationComputeShader.FindKernel("SpreadActivations");
          kernelClean = simulationComputeShader.FindKernel("ClearActivations");

          // set up shader for focusdot
          focusDotMaterial = new Material(Shader.Find("Xarphos/FocusDot"));
          focusDotMaterial.SetInt(ShPrRenderFocusDotToggle, renderFocusDot);

          onChangeGazeCondition = new UnityEvent<EyeTracking.EyeTrackingConditions>();
          SenorSummarySingletons.RegisterType(this);
        }

        private void Start()
        {
          // replace surfaces with in editor selected variant
          SurfaceReplacement.ActivateReplacementShader(targetCamera, surfaceReplacementMode);
        }

        private void OnRenderImage(RenderTexture src, RenderTexture target)
        {
          InitialiseTextures(src); // set up textures, resolutions and parameters
          // if headset is not yet available: skip
          if (target == null || !headsetInitialised) return;

          // in between texture to put processed image on before blitting from this to target
          var preTargetPing = RenderTexture.GetTemporary(target.descriptor);
          
          // TODO: support low-res rendertexture? (thicker edges and less compute...
          // var descriptor = target.descriptor;
          // descriptor.height = (int)descriptor.height / 4;
          // descriptor.width = (int)descriptor.height / 4;
          // var preTargetPing = RenderTexture.GetTemporary(descriptor);
          
          // Run edge detection shader if toggled on
          if (runEdgeDetection)
            Graphics.Blit(src, preTargetPing, imageProcessingMaterial);
          else
            Graphics.Blit(src, preTargetPing);

          // if phosphene simulation is turned on
          if ((int)runSimulation != 0)
          {
            simulationComputeShader.SetTexture(kernelActivations, ShPrInputTex, preTargetPing);

            // calculate activations
            simulationComputeShader.Dispatch(kernelActivations, threadPhosphenes, 1, 1);
            // render phosphene simulation
            simulationComputeShader.Dispatch(kernelSpread, threadX, threadY, 1);

            // reinit & copy simulation to pre-out
            RenderTexture.ReleaseTemporary(preTargetPing);
            preTargetPing = RenderTexture.GetTemporary(target.descriptor);
            Graphics.Blit(simRenderTex, preTargetPing);//, new Vector2(1, -1), Vector2.zero);
            simulationComputeShader.Dispatch(kernelClean, threadX, threadY, 1);
          }

          // lastly render the focus dot on top
          Graphics.Blit(preTargetPing, target, focusDotMaterial);
          RenderTexture.ReleaseTemporary(preTargetPing);
        }

        /// <summary>
        /// Sets up the parameters relating to image processing and simulation, like resolution
        /// </summary>
        /// <param name="src">a render texture that is VR ready to get parameters from</param>
        private void InitialiseTextures(RenderTexture src)
        {
          if (headsetInitialised || XRSettings.eyeTextureWidth == 0) return;
          
          headsetInitialised = true;
            
          // set up resolution
          var w = XRSettings.eyeTextureWidth;
          var h = XRSettings.eyeTextureHeight;
          imageProcessingMaterial.SetVector(ShPrScreenResolution, new Vector4(w, h, 0, 0));
          Debug.Log($"Set Res to: {w}, {h}");

          // set up input texture for simulation
          actvTex = new RenderTexture(src.descriptor)
          {
            width = w,
            height = h,
            graphicsFormat = GraphicsFormat.R32G32_SFloat,
            depth = 0,
            enableRandomWrite = true
          };
          actvTex.Create();
          // pass texture references to compute shader
          simulationComputeShader.SetTexture(kernelActivations, ShPrActivationTex, actvTex);
          simulationComputeShader.SetTexture(kernelSpread, ShPrActivationTex, actvTex);
          simulationComputeShader.SetTexture(kernelClean, ShPrActivationTex, actvTex);
          
          // set up render texture that will hold the spread activation, thus the output of the simulation
          simRenderTex = new RenderTexture(src.descriptor)
          {
            enableRandomWrite = true
          };
          simRenderTex.Create();
          // Initialize the render textures & Set the shaders with the shared render textures
          simulationComputeShader.SetInts(ShPrScreenResolution, w, h);
          simulationComputeShader.SetTexture(kernelSpread, ShPrSimRenderTex, simRenderTex);
          simulationComputeShader.SetTexture(kernelClean, ShPrSimRenderTex, simRenderTex);

          // calculate the thread count necessary to cover the entire texture
          simulationComputeShader.GetKernelThreadGroupSizes(kernelSpread, out var xGroup, out var yGroup, out _);
          threadX = Mathf.CeilToInt(w / (float)xGroup);
          threadY = Mathf.CeilToInt(h / (float)yGroup);
          simulationComputeShader.GetKernelThreadGroupSizes(kernelActivations, out xGroup, out _, out _);
          threadPhosphenes = Mathf.CeilToInt(nPhosphenes / (float)xGroup);
          
          // calculate the center position for each eye corrected for visual transform
          var (lViewSpace, rViewSpace, cViewSpace) = EyePosFromScreenPoint(0.5f, 0.5f);
          SetEyePosition(lViewSpace, rViewSpace, cViewSpace);
          simulationComputeShader.SetVector(ShPrLeftEyeCenter, lViewSpace);
          simulationComputeShader.SetVector(ShPrRightEyeCenter, rViewSpace);

          // Replace surfaces with the surface replacement shader
          SurfaceReplacement.ActivateReplacementShader(targetCamera, surfaceReplacementMode);
        }

        /// <summary>
        /// calculate the screen position for each eye from a 2d point on the "center" view
        /// </summary>
        /// <param name="x">x position on screen in 0..1, left is 0</param>
        /// <param name="y">y position on screen in 0..1, bottom is 0</param>
        /// <returns>tuple of left, right and center screen position according to input. center should be roughly equal to input</returns>
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

        private void OnDestroy(){
          phospheneBuffer.Release();
          actvTex.Release();
          simRenderTex.Release();
        }

        #region Input Handling
        // cycle surface replacement
        public void NextSurfaceReplacementMode(InputAction.CallbackContext ctx) => NextSurfaceReplacementMode();
        private void NextSurfaceReplacementMode(){
          surfaceReplacementMode = (SurfaceReplacement.ReplacementModes)((int)(surfaceReplacementMode + 1) % nSurfaceModes);
          // replace surfaces with in editor selected variant
          SurfaceReplacement.ActivateReplacementShader(targetCamera, surfaceReplacementMode);
        }

        // cycle eye tracking conditions
        public void NextEyeTrackingCondition(InputAction.CallbackContext ctx) => NextEyeTrackingCondition();
        private void NextEyeTrackingCondition()
        {
          // SetGazeTrackingCondition((EyeTracking.EyeTrackingConditions)((int)(EyeTrackingCondition + 1) % nEyeTrackingModes));
          SetGazeTrackingCondition((EyeTracking.EyeTrackingConditions)((int)(EyeTrackingCondition + 2) % nEyeTrackingModes)); // JR: reversed order for easier explanation.
        }
        
        public void SetGazeTrackingCondition(EyeTracking.EyeTrackingConditions condition)
        {
          EyeTrackingCondition = condition;
          switch (EyeTrackingCondition)
          {
            // reset and don't use gaze info
            case EyeTracking.EyeTrackingConditions.GazeIgnored:
              simulationComputeShader.SetInt(ShPrGazeAssistedToggle, 0);
              simulationComputeShader.SetInt(ShPrGazeLockedToggle, 0);
              break;
            // add lock to gaze
            case EyeTracking.EyeTrackingConditions.SimulationFixedToGaze:
              simulationComputeShader.SetInt(ShPrGazeAssistedToggle, 0);
              simulationComputeShader.SetInt(ShPrGazeLockedToggle, 1);
              break;
            // add gaze assisted sampling on top
            case EyeTracking.EyeTrackingConditions.GazeAssistedSampling:
              simulationComputeShader.SetInt(ShPrGazeAssistedToggle, 1);
              simulationComputeShader.SetInt(ShPrGazeLockedToggle, 1);
              break;
          }
        }
        
        public void ToggleEdgeDetection(InputAction.CallbackContext ctx) => ToggleEdgeDetection();
        private void ToggleEdgeDetection()
        {
          SetEdgeDetection(!runEdgeDetection);
        }
        
        public void SetEdgeDetection(bool val)
        {
          runEdgeDetection = val;
        }
        
        public void TogglePhospheneSim(InputAction.CallbackContext ctx) => TogglePhospheneSim();
        public void TogglePhospheneSim()
        {
          SetPhospheneSim(1-runSimulation);
        }
        public void SetPhospheneSim(bool val)
        {
          SetPhospheneSim(val ? 1f : 0f);
        }

        public void SetPhospheneSim(float val)
        {
          runSimulation = val > 0 ? 1f : 0f;
        }
        
        public void ToggleFocusDot(InputAction.CallbackContext ctx) => ToggleFocusDot();
        public void ToggleFocusDot()
        {
          SetFocusDot(1-renderFocusDot);
        }
        public void ToggleFocusDot(bool val)
        {
          SetFocusDot(val ? 1 : 0);
        }

        public void SetFocusDot(int val)
        {
          renderFocusDot = val;
          focusDotMaterial.SetInt(ShPrRenderFocusDotToggle, renderFocusDot);
        }
        #endregion

        /// <summary>
        /// Update class variables and pass new positions to shaders
        /// </summary>
        /// <param name="leftViewport">left eye screen position in 0..1</param>
        /// <param name="rightViewport">right eye screen position in 0..1</param>
        /// <param name="centreViewport">centre screen position in 0..1</param>
        public void SetEyePosition(Vector2 leftViewport, Vector2 rightViewport, Vector2 centreViewport)
        {
          eyePosLeft = leftViewport;
          eyePosRight = rightViewport;
          eyePosCentre = centreViewport;

          focusDotMaterial.SetVector(ShPrLeftEyePos, eyePosLeft);
          focusDotMaterial.SetVector(ShPrRightEyePos, eyePosRight);
          
          simulationComputeShader.SetVector(ShPrLeftEyePos, eyePosLeft);
          simulationComputeShader.SetVector(ShPrRightEyePos, eyePosRight);
        }
      
        protected void Update()
        {
          if (setManualEyePos)
          {
            if (Keyboard.current[Key.J].isPressed) manualEyePos.y -= .05f * Time.deltaTime;
            else if (Keyboard.current[Key.U].isPressed) manualEyePos.y += .05f * Time.deltaTime;

            if (Keyboard.current[Key.K].isPressed) manualEyePos.x += .05f * Time.deltaTime;
            else if (Keyboard.current[Key.H].isPressed) manualEyePos.x -= .05f * Time.deltaTime;
            
            var (lViewSpace, rViewSpace, cViewSpace) = EyePosFromScreenPoint(manualEyePos.x, manualEyePos.y);
            
            SetEyePosition(lViewSpace, rViewSpace, cViewSpace);
          }
        }

        // Public methods for activating/deactivating the full simulation (img processing + phosphene simulation)
        
        private bool _simulationActive;

        public void ActivateSimulation() => ActivateSimulation(gazeCondition);
        public void ActivateSimulation(EyeTracking.EyeTrackingConditions condition)
        {
          _simulationActive = true;
          // boxChecker.gameObject.SetActive(true);
          // checkpointChecker.gameObject.SetActive(true);
          //
          SetEdgeDetection(true);
          SetPhospheneSim(true);
          SetGazeTrackingCondition(condition);
          SurfaceReplacement.ActivateReplacementShader(targetCamera, SurfaceReplacement.ReplacementModes.Normals);
        }

        public void DeactivateSimulation()
        {
          _simulationActive = false;
          // boxChecker.gameObject.SetActive(false);
          // checkpointChecker.gameObject.SetActive(false);
          //
          SurfaceReplacement.DeactivateReplacementShader(targetCamera);
          SetEdgeDetection(false);
          SetPhospheneSim(false);
        }

        public void ToggleSimulationActive() => ToggleSimulationActive(gazeCondition);
        public void ToggleSimulationActive(EyeTracking.EyeTrackingConditions condition)
        {
          if (_simulationActive)
            DeactivateSimulation();
          else
            ActivateSimulation(condition);
        }
        
        public void ActivateImageProcessing()
        {
          SurfaceReplacement.ActivateReplacementShader(targetCamera, SurfaceReplacement.ReplacementModes.Normals);
          SetEdgeDetection(true);
          SetPhospheneSim(false);
        }
        
        
        
    }
}
