using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.XR;


namespace Xarphos.Simulation
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
        private bool runSimulation = false;
        private bool runEdgeDetection = false;
        private int renderFocusDot = 0;
        
        private RenderTexture activeTex, simRenderTex;
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
        [SerializeField] private bool debugPhosphenes;
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
        private static readonly int ShPrRelativeRFSize = Shader.PropertyToID("_RelativeRFSize");
      
        #endregion

        #region Start Up Procedures
        protected void Awake()
        {
          targetCamera = targetCamera != null ? targetCamera : GetComponent<Camera>();

          // Initialize the array of phosphenes
          InitPhospheneBuffer();
          // Initialize the shaders
          InitShaders();

          // Initialise event to update shader and simulation when condition changes
          onChangeGazeCondition = new UnityEvent<EyeTracking.EyeTrackingConditions>();
          // Register instance with the singleton-manager to allow for easy access from other scripts
          SingletonRegister.RegisterType(this);
        }

        /// <summary>
        /// Initializes the phosphene buffer.
        /// </summary>
        private void InitPhospheneBuffer()
        {
          if (initialiseFromFile && phospheneConfigFile != null)
          {
            try
            {
              phospheneConfig = PhospheneConfig.InitPhosphenesFromJSON(phospheneConfigFile);
            } catch (FileNotFoundException){
              Debug.Log("File not found, initialising probabilistically");
              initialiseFromFile = false;
            }
          }
          if (!initialiseFromFile)
          {
            // if boolean is false, the file path is not given or the initialising from file failed, initialise probabilistic
            phospheneConfig = PhospheneConfig.InitPhosphenesProbabilistically(numberOfPhosphenes, maxEccentricityRadius, PhospheneConfig.Monopole, debugPhosphenes);
          }

          nPhosphenes = phospheneConfig.phosphenes.Length;
          phospheneBuffer = new ComputeBuffer(nPhosphenes, sizeof(float)*7);
          phospheneBuffer.SetData(phospheneConfig.phosphenes);
        }

        
        /// <summary>
        /// Initializes the shaders and materials used for phosphene simulation.
        /// </summary>
        private void InitShaders()
        {
          // Initialize materials with shaders
          imageProcessingMaterial = new Material(edgeDetectionShader);
          
          // Set the compute shader with the temporal dynamics variables
          simulationComputeShader.SetFloat(ShPrInputEffect, inputEffect);
          simulationComputeShader.SetFloat(ShPrIntensityDecay, intensityDecay);
          simulationComputeShader.SetFloat(ShPrTraceIncrease, traceIncrease);
          simulationComputeShader.SetFloat(ShPrTraceDecay, traceDecay);
          simulationComputeShader.SetFloat(ShPrRelativeRFSize, relativeRFSize);

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
        }

        private void Start()
        {
          // replace surfaces with in editor selected variant
          SurfaceReplacement.ActivateReplacementShader(targetCamera, surfaceReplacementMode);
        }
        #endregion

        
        /// <summary>
        /// Unity function that is called right before an image is rendered to the camera.
        /// This is where the actual simulation happens and image processing is applied.
        /// </summary>
        /// <param name="src">The incoming source RenderTexture.</param>
        /// <param name="target">The target RenderTexture with the simulation applied.</param>
        private void OnRenderImage(RenderTexture src, RenderTexture target)
        {
          // set up textures, resolutions and parameters
          InitialiseTextures(src);

          // if headset is not yet available: skip
          // ToDo: Handle headset not being available for demo purposes
          if (target == null || !headsetInitialised) return;

          // in between texture to put processed image on before blitting from this to target
          // ? Should we utilise a low-res RenderTextures for processing? (thicker edges / need to handle upscaling downscale source, but less compute)
          // ? example with low-res one-sixteenth resolution:
          // var descriptor = target.descriptor;
          // descriptor.height = (int)descriptor.height / 4;
          // descriptor.width = (int)descriptor.height / 4;
          // var preTargetPing = RenderTexture.GetTemporary(descriptor);
          // Full resolution middle stage:
          var inBetweenRenderTexture = RenderTexture.GetTemporary(target.descriptor);
          
          // Run edge detection shader if toggled on
          if (runEdgeDetection)
            Graphics.Blit(src, inBetweenRenderTexture, imageProcessingMaterial);
          // otherwise just copy input to processing-texture
          else
            Graphics.Blit(src, inBetweenRenderTexture);

          // if phosphene simulation is turned on
          if (runSimulation)
          {
            // pass in-between texture as input to simulation compute shader
            simulationComputeShader.SetTexture(kernelActivations, ShPrInputTex, inBetweenRenderTexture);

            // calculate activations; since it's a 1d array only 1 thread for the other two dimensions
            simulationComputeShader.Dispatch(kernelActivations, threadPhosphenes, 1, 1);
            // render phosphene simulation; all channels of the pixels are handled simultaneously, so only 1 z dimension thread
            // simulation output is stored in `simRenderTex`
            simulationComputeShader.Dispatch(kernelSpread, threadX, threadY, 1);

            // release in-between texture & reinitialise as pre-out and copy simulation onto it
            RenderTexture.ReleaseTemporary(inBetweenRenderTexture);
            inBetweenRenderTexture = RenderTexture.GetTemporary(target.descriptor);
            Graphics.Blit(simRenderTex, inBetweenRenderTexture);//, new Vector2(1, -1), Vector2.zero); //? why is this flipped sometimes?
            // clean-up simulation texture since output is stored in `inBetweenRenderTexture`
            simulationComputeShader.Dispatch(kernelClean, threadX, threadY, 1);
          }

          // lastly render the focus dot on top and copy everything into the target texture to be shown
          Graphics.Blit(inBetweenRenderTexture, target, focusDotMaterial);
          // release in-between texture
          RenderTexture.ReleaseTemporary(inBetweenRenderTexture);
        }

        /// <summary>
        /// Sets up the parameters relating to image processing and simulation, like resolution, thread-count, etc.
        /// </summary>
        /// <param name="src">a render texture that is VR ready to get parameters from</param>
        private void InitialiseTextures(RenderTexture src)
        {
          // if this step has been performed already or the XRSettings haven't been initialised yet: skip
          if (headsetInitialised || XRSettings.eyeTextureWidth == 0) return;
            
          // set up resolution for edge detection shader
          var w = XRSettings.eyeTextureWidth;
          var h = XRSettings.eyeTextureHeight;
          imageProcessingMaterial.SetVector(ShPrScreenResolution, new Vector4(w, h, 0, 0));
          Debug.Log($"Set Res to: {w}, {h}");

          // set up input texture for simulation
          activeTex = new RenderTexture(src.descriptor)
          {
            width = w,
            height = h,
            graphicsFormat = GraphicsFormat.R32G32_SFloat, // we use a 2 channel texture to run the simulation on for memory efficiency
            depth = 0,
            enableRandomWrite = true
          };
          activeTex.Create();

          // pass texture reference to compute shader to be associated with all 3 programs
          simulationComputeShader.SetTexture(kernelActivations, ShPrActivationTex, activeTex);
          simulationComputeShader.SetTexture(kernelSpread, ShPrActivationTex, activeTex);
          simulationComputeShader.SetTexture(kernelClean, ShPrActivationTex, activeTex);
          

          // set up render texture that will hold the spread activation, thus the output of the simulation
          simRenderTex = new RenderTexture(src.descriptor)
          {
            enableRandomWrite = true // necessary for the compute shader to write to random places according to phosphene location
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
          // and the threads necessary to cover the entire phosphene array
          simulationComputeShader.GetKernelThreadGroupSizes(kernelActivations, out xGroup, out _, out _);
          threadPhosphenes = Mathf.CeilToInt(nPhosphenes / (float)xGroup);
          
          // calculate the center position on the texture for each eye, when corrected for projection
          var (lViewSpace, rViewSpace, cViewSpace) = EyePosFromScreenPoint(0.5f, 0.5f);
          // set centre of eyes as shader variables
          simulationComputeShader.SetVector(ShPrLeftEyeCenter, lViewSpace);
          simulationComputeShader.SetVector(ShPrRightEyeCenter, rViewSpace);
          // and set current position to centre
          SetEyePosition(lViewSpace, rViewSpace, cViewSpace);

          // Replace surfaces with the surface replacement shader
          SurfaceReplacement.ActivateReplacementShader(targetCamera, surfaceReplacementMode);          

          // mark initialisation as done
          headsetInitialised = true;
        }

        /// <summary>
        /// A convenience function for calculation eye position from a 2d point mostly for testing and initialisation
        /// Assumes a 10 unit distance from the camera
        /// </summary>
        /// <param name="x">x position on screen in 0..1, left is 0</param>
        /// <param name="y">y position on screen in 0..1, bottom is 0</param>
        /// <param name="z">distance from the camera to the point in world units</param>
        /// <returns>tuple of left, right and center screen position according to input. center should be roughly equal to input</returns>
        internal (Vector2, Vector2, Vector2) EyePosFromScreenPoint(float x, float y, float z=10f)
        {
          // Calculate a point in world space that is 10 units in front of the camera from where the eye tracking reports the eyes are
          var P = targetCamera.ViewportToWorldPoint(new Vector3(x, y, z)); 
          return EyePosFromFocusPoint(P);
        }
        
        /// <summary>
        /// calculate the screen position for each eye from a 2d point on the "center" view.
        /// This is necessary because the projection matrix is different for each eye and 
        /// basically is the inverse of the screen projection performed by the camera for each eye 
        /// </summary>
        /// <param name="point">Point to be projected in world space coordinates</param>
        /// <returns>tuple of left, right and center screen position according to input. center should be roughly equal to input</returns> 
        internal (Vector2, Vector2, Vector2) EyePosFromFocusPoint(Vector3 point)
        {
          // projection from local space to clip space
          var lMat = targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Left);
          var rMat = targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Right);
          var cMat = targetCamera.nonJitteredProjectionMatrix;
          // projection from world space into local space
          var world2cam = targetCamera.worldToCameraMatrix;
          // 4th dimension necessary in graphics to get scale
          var P4d = new Vector4(point.x, point.y, point.z, 1f); 
          // point in world space * world2cam -> local space point
          // local space point * projection matrix = clip space point
          var lProjection = lMat * world2cam * P4d;
          var rProjection = rMat * world2cam * P4d;
          var cProjection = cMat * world2cam * P4d;
          // scale and shift from clip space [-1,1] into view space [0,1]
          var lViewSpace = new Vector2(lProjection.x, lProjection.y) / lProjection.w * .5f + .5f * Vector2.one;
          var rViewSpace = new Vector2(rProjection.x, rProjection.y) / rProjection.w * .5f + .5f * Vector2.one;
          var cViewSpace = new Vector2(cProjection.x, cProjection.y) / cProjection.w * .5f + .5f * Vector2.one;
          
          return (lViewSpace, rViewSpace, cViewSpace);
        }

        private void OnDestroy(){
          phospheneBuffer.Release();
          if (activeTex != null) activeTex.Release();
          if (simRenderTex != null) simRenderTex.Release();
        }

        /// <summary>
        /// Update class variables and pass new positions to shaders
        /// </summary>
        /// <param name="leftViewport">left eye screen position in 0..1</param>
        /// <param name="rightViewport">right eye screen position in 0..1</param>
        /// <param name="centreViewport">centre screen position in 0..1</param>
        internal void SetEyePosition(Vector2 leftViewport, Vector2 rightViewport, Vector2 centreViewport)
        {
          eyePosLeft = leftViewport;
          eyePosRight = rightViewport;
          eyePosCentre = centreViewport;

          focusDotMaterial.SetVector(ShPrLeftEyePos, eyePosLeft);
          focusDotMaterial.SetVector(ShPrRightEyePos, eyePosRight);
          
          simulationComputeShader.SetVector(ShPrLeftEyePos, eyePosLeft);
          simulationComputeShader.SetVector(ShPrRightEyePos, eyePosRight);
        }
      
        
        /// <summary>
        /// Mostly for debugging to manually move the eye position around
        /// </summary>
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

#region Public methods for activating/deactivating the simulation parts (img processing + phosphene simulation, focus dot, condition, ...)
        private bool _simulationActive;

        public void ActivateSimulation() => ActivateSimulation(gazeCondition);
        public void ActivateSimulation(EyeTracking.EyeTrackingConditions condition)
        {
          _simulationActive = true;

          SurfaceReplacement.ActivateReplacementShader(targetCamera, SurfaceReplacement.ReplacementModes.Normals);
          SetEdgeDetection(true);
          SetPhospheneSim(true);
          SetGazeTrackingCondition(condition);
        }

        public void DeactivateSimulation()
        {
          _simulationActive = false;

          SurfaceReplacement.DeactivateReplacementShader(targetCamera);
          SetFocusDot(0);
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
        
        /// <summary>
        /// Convenience function for activating edge detection on the default surface replacement shader (surface normals)
        /// </summary>
        public void ActivateImageProcessing()
        {
          SurfaceReplacement.ActivateReplacementShader(targetCamera, SurfaceReplacement.ReplacementModes.Normals);
          SetEdgeDetection(true);
          SetPhospheneSim(false);
        }

        /// <summary>
        /// Toggle the focus dot on and off
        /// </summary>
        public void ToggleFocusDot(){ SetFocusDot(1-renderFocusDot); }
        /// <summary>
        /// Convenience function to toggle the focus dot on and off with a boolean
        /// </summary>
        /// <param name="val">Whether the focus dot is rendered (true) or not (false)</param>  
        public void ToggleFocusDot(bool val){ SetFocusDot(val ? 1 : 0); }
        /// <summary>
        /// Sets the focus dot on or off
        /// </summary>
        /// <param name="val">Whether the focus dot is rendered (val != 0) or not (val = 0)</param>
        public void SetFocusDot(int val)
        {
          renderFocusDot = val;
          focusDotMaterial.SetInt(ShPrRenderFocusDotToggle, renderFocusDot);
        }
        /// <summary>
        /// Toggle the edge detection shader on and off
        /// </summary>
        private void ToggleEdgeDetection(){ SetEdgeDetection(!runEdgeDetection); }
        /// <summary>
        /// Sets the edge detection shader on or off
        /// </summary>
        /// <param name="val">Whether to do edge detection (true) or not (false)</param>
        public void SetEdgeDetection(bool val){ runEdgeDetection = val; }
        /// <summary>
        /// Toggle the simulation on and off
        /// </summary>
        public void TogglePhospheneSim(){ SetPhospheneSim(!runSimulation); }
        public void SetPhospheneSim(bool val){ runSimulation = val; }
        /// <summary>
        /// Cycles the eye tracking condition through the different modes (ignore gaze, fixed to gaze, gaze assisted sampling) 
        /// </summary>
        private void NextEyeTrackingCondition() { SetGazeTrackingCondition((EyeTracking.EyeTrackingConditions)((int)(EyeTrackingCondition + 1) % nEyeTrackingModes)); }
        /// <summary>
        /// Sets the eye tracking condition to the given value and updates the shader accordingly
        /// </summary>
        /// <param name="condition">The eye tracking condition the simulation should use</param>
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

        /// <summary>
        /// Cycles through the different surface replacement modes for the camera, used for edge detection
        /// </summary>
        private void NextSurfaceReplacementMode(){
          surfaceReplacementMode = (SurfaceReplacement.ReplacementModes)((int)(surfaceReplacementMode + 1) % nSurfaceModes);
          SurfaceReplacement.ActivateReplacementShader(targetCamera, surfaceReplacementMode);
        }
#endregion

#region Input Handling
        public void NextSurfaceReplacementMode(InputAction.CallbackContext _) => NextSurfaceReplacementMode();
        public void NextEyeTrackingCondition(InputAction.CallbackContext _) => NextEyeTrackingCondition();        
        public void ToggleEdgeDetection(InputAction.CallbackContext _) => ToggleEdgeDetection();
        public void TogglePhospheneSim(InputAction.CallbackContext _) => TogglePhospheneSim();
        public void ToggleFocusDot(InputAction.CallbackContext _) => ToggleFocusDot();
#endregion
    }
}
