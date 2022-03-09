using UnityEngine;
using Rect = UnityEngine.Rect;
using OpenCVForUnity;
using System;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using UnityEditor;
using UnityEngine.InputSystem;
using ViveSR.anipal.Eye;
using EyeFramework = ViveSR.anipal.Eye.SRanipal_Eye_Framework;

namespace Xarphos.Scripts
{
    [RequireComponent(typeof(Renderer))]
    public class DSPV_SimulationController : MonoBehaviour {
        // The material texture will be used as phosphene activation mask
        protected Material Material;
        protected Texture2D input_texture;   //The input texture (webcam or virtual camera)
        protected Texture2D activation_mask; //The output texture after preprocessing

        // The shader is used for the phosphene simulation
        [SerializeField] protected Shader shader;
        private static readonly int _PhospheneActivationMask = Shader.PropertyToID("_ActivationMask"); // Accessible by shader

        [SerializeField] protected Texture2D phospheneMapping;
        private static readonly int _PhospheneMapping = Shader.PropertyToID("_PhospheneMapping");

        // In this example the input texture is grabbed from virtual camera
        protected Camera playerCam;
        protected RenderTexture renderTexture;
        //protected WebCamTexture webcamTexture; // can be used instead of virtual camera

        // OpenCVForUnity
        protected Mat in_mat;
        protected Mat out_mat;
        protected Mat dilateElement;

        // EyeTracking
        protected Vector2 eyePosition;
        protected int gazeLocking; // used as boolean (sent to shader)
        protected bool camLocking;

        protected int NumberOfPhosphenes;
        protected Vector4[] phospheneSpecs;


        // stimulation parameters
        protected float stim;
        protected float[] activation;
        protected float[] memoryTrace;
        [SerializeField] float input_effect = 0.7f;
        [SerializeField] float intensity_decay = 0.8f;
        [SerializeField] float trace_increase = 0.1f;
        [SerializeField] float trace_decay = 0.9f;

        protected bool cannyFiltering;
        
        // Added for Eye Tracking Implementation
        [SerializeField] private Camera simCam;
        private bool _eyeTrackingEnabled;

        private void Start()
        {
            _eyeTrackingEnabled = SRanipal_Eye_Framework.Instance.EnableEye;
        }

        protected void Awake()
        {
            // Initialize material, input texture and shader
            GetComponent<Renderer>().material = Material = new Material(shader);
            
            int w = 512, h = 512;
            // Grab current screen size
            // w = Screen.width;
            // h = Screen.height;

            // adjust Quad size
            // var fac = Gcf(w, h);
            // gameObject.transform.localScale = new Vector3(w/fac, h/fac, 1);
            
            input_texture = new Texture2D(w, h, TextureFormat.RGBA32, false);

            // Initialize virtual camera
            renderTexture = new RenderTexture(w, h, 24);
            playerCam = Camera.main ? Camera.main : GameObject.Find("PlayerCamera").GetComponent<Camera>();
            playerCam.targetTexture = renderTexture;

            // //TODO: SPECIFY PHOSPHENE LOCATIONS IN JSON FILE
            // int i = 0;
            // for (int x = 0; x < 25; x++)
            // {
            //     for (int y = 0; y < 25; y++)
            //     {
            //         x_positions[i] = x / 25f;
            //         y_positions[i++] = y / 25f;
            //     }
            // }


            // Read phosphene specifications from phospheneMapping texture
            NumberOfPhosphenes = phospheneMapping.width;
            phospheneSpecs = new Vector4[NumberOfPhosphenes];
            for (int idx = 0;  idx < NumberOfPhosphenes; idx++){
              phospheneSpecs[idx]= phospheneMapping.GetPixel(idx, 0); // Specifications: (x,y,sigma,[unused])
            }

            // Pass to shader
            Material.SetVectorArray("_pSpecs", phospheneSpecs);
            Material.SetInt("_nPhosphenes", NumberOfPhosphenes);

            // Initialize other simulation variables
            activation = new float[NumberOfPhosphenes];
            memoryTrace = new float[NumberOfPhosphenes];

            // OPENCV
            in_mat = new Mat(w, h, CvType.CV_8UC4, new Scalar(0, 0, 0, 255));
            out_mat = new Mat(w, h, CvType.CV_8UC4, new Scalar(0, 0, 0, 255));
            dilateElement = Imgproc.getStructuringElement(Imgproc.MORPH_ELLIPSE, new Size(9, 9));
        }


        protected void Update()
        {
            ProcessImageAndUpdatePhosphenes();
            ProcessKeyboardInput();
            if (_eyeTrackingEnabled)
                EyeTrackingStep();

            Material.SetVector("_EyePosition", eyePosition);
            Material.SetInt("_GazeLocked", gazeLocking);
        }

        private void ProcessKeyboardInput()
        {
            if (Keyboard.current.digit1Key.isPressed)
            {
                cannyFiltering = false;
                Material.SetFloat("_PhospheneFilter", 0f);
            }

            if (Keyboard.current.digit2Key.isPressed)
            {
                cannyFiltering = true;
                Material.SetFloat("_PhospheneFilter", 1f);
            }

            if (Keyboard.current.digit3Key.isPressed)
            {
                cannyFiltering = false;
                Material.SetFloat("_PhospheneFilter", 1f);
            }

            if (Keyboard.current.digit4Key.isPressed)
            {
                cannyFiltering = true;
                Material.SetFloat("_PhospheneFilter", 0f);
            }

            if (Keyboard.current.gKey.isPressed)
            {
                gazeLocking = 1-gazeLocking;
            }

            if (Keyboard.current.cKey.isPressed)
            {
                camLocking = !camLocking;
            }
            
            if (Keyboard.current.escapeKey.isPressed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            if (_eyeTrackingEnabled) return;

            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                if (Keyboard.current.upArrowKey.isPressed) {eyePosition.y = 0.8f;}
                if (Keyboard.current.downArrowKey.isPressed) {eyePosition.y = 0.2f;}
            }
            else
            {
                eyePosition.y = 0.5f;
            }

            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                if (Keyboard.current.leftArrowKey.isPressed) {eyePosition.x = 0.2f;}
                if (Keyboard.current.rightArrowKey.isPressed) {eyePosition.x = 0.8f;}
            }
            else
            {
                eyePosition.x = 0.5f;
            }
        }

        private void ProcessImageAndUpdatePhosphenes()
        {
            
            // IMAGE PREPROCESSING

            //1.Get texture (webcam, or in this case: virtual camera)
            playerCam.Render();
            RenderTexture.active = renderTexture;
            input_texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            input_texture.Apply();


            // 2. Filter image
            // todo: apply pre-processing (e.g. in Python)
            activation_mask = input_texture; // in this example, no pre-processing is applied

            // --- Pre-processing examples ---
            /*
            // Simple thresholding example
            var data = activation_mask.GetRawTextureData<Color32>();
            int index = 0;
            for (int y = 0; y < activation_mask.height; y++)
            {
                for (int x = 0; x < activation_mask.width; x++)
                {
                    data[index] = ((Vector4)(Color)data[index++]).magnitude < 1.8 ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, 255);
                }
            }
            activation_mask.Apply();
            */

            // Canny edge detection example (requires OpenCVForUnity package)
            if (cannyFiltering){
              Utils.texture2DToMat(input_texture,in_mat);
              Imgproc.Canny(in_mat, out_mat, 110, 220);
              Imgproc.dilate(out_mat, out_mat, dilateElement);
              Utils.matToTexture2D(out_mat, activation_mask);
            }
            // ---------------------------------

            // 3. Apply activation mask as texture (so it can be accessed by shader)
            Material.SetTexture(_PhospheneActivationMask, activation_mask);

            // Update activation using activation mask and previous state
            for (int i = 0; i < NumberOfPhosphenes; i++){
              Vector2 pos = phospheneSpecs[i]; // x and y coordinates
              if (camLocking) {
                pos += eyePosition- new Vector2(0.5f,0.5f);
              }

              if (pos.x > 0 && pos.x < 1 && pos.y > 0 && pos.y < 1) {
                stim = activation_mask.GetPixelBilinear(pos.x, pos.y).grayscale;}
              else {
                stim = 0;
              }
              activation[i] *= intensity_decay;
              activation[i] += Math.Max(0,input_effect*(stim-memoryTrace[i]));//);;
              memoryTrace[i] = memoryTrace[i] * trace_decay + trace_increase*stim;

            }

            Material.SetFloatArray("activation", activation);
            Material.SetVectorArray("_pSpecs", phospheneSpecs);
        }

        private void EyeTrackingStep()
        {
            if (CheckFrameworkStatusErrors())
            {
                return;
            }
            VerboseData vData;
            SRanipal_Eye_v2.GetVerboseData(out vData);
            var gazeDir = vData.combined.eye_data.gaze_direction_normalized;
            gazeDir.x *= -1; // ToDo: This should not be necessary? Why is ray mirrored?

            RaycastHit hit;
            if (Physics.Raycast(simCam.transform.position, gazeDir, out hit))
            {
                eyePosition = hit.textureCoord;
            }
        }
        
        private bool CheckFrameworkStatusErrors()
        {
            return EyeFramework.Status != EyeFramework.FrameworkStatus.WORKING &&
                   EyeFramework.Status != EyeFramework.FrameworkStatus.NOT_SUPPORT;
        }
        
        private int Gcf(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

    }
}
