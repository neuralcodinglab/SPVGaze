using System;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using Rect = UnityEngine.Rect;

namespace Xarphos
{
    public class GraphicsTest : MonoBehaviour
    {
        // The material texture will be used as phosphene activation mask
        protected Material Material;

        // The shader is used for the phosphene simulation
        [SerializeField] protected Shader shader;
        private static readonly int _PhospheneActivationMask = Shader.PropertyToID("_ActivationMask"); // Accessible by shader

        [SerializeField] protected Texture2D phospheneMapping;
        private static readonly int _PhospheneMapping = Shader.PropertyToID("_PhospheneMapping");
        private static readonly int _Activation = Shader.PropertyToID("activation");

        // In this example the input texture is grabbed from virtual camera
        protected Camera playerCam;
        protected RenderTexture renderTexture;

        protected Texture2D activation_mask;
        //protected WebCamTexture webcamTexture; // can be used instead of virtual camera

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
        
        
        private void Awake()
        {
            Material = new Material(shader);
            playerCam = Camera.main ? Camera.main : GameObject.Find("PlayerCamera").GetComponent<Camera>();

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

            Material.SetTexture(_PhospheneActivationMask, renderTexture);
        }

        protected void Update()
        {
            ProcessKeyboardInput();
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

            if (Keyboard.current.escapeKey.isPressed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }


        private void OnPreRender()
        {
            playerCam.targetTexture = renderTexture;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            activation_mask ??= new Texture2D(src.width, src.height);
            RenderTexture.active = src;
            activation_mask.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            activation_mask.Apply();
            RenderTexture.active = null;
            
            // Update activation using activation mask and previous state
            for (int i = 0; i < NumberOfPhosphenes; i++){
                Vector2 pos = phospheneSpecs[i]; // x and y coordinates
            
                if (pos.x is > 0 and < 1 && pos.y is > 0 and < 1) {
                    stim = activation_mask.GetPixelBilinear(pos.x, pos.y).grayscale;}
                else {
                    stim = 0;
                }
                activation[i] *= intensity_decay;
                activation[i] += Math.Max(0,input_effect*(stim-memoryTrace[i]));
                memoryTrace[i] = memoryTrace[i] * trace_decay + trace_increase*stim;
            }
            Material.SetFloatArray(_Activation, activation);
            
            playerCam.targetTexture = null;
            Graphics.Blit(src, null as RenderTexture, Material);
        }
    }
}