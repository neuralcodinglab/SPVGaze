using UnityEngine;
using Rect = UnityEngine.Rect;
using System;
using Sirenix.OdinInspector;


namespace Xarphos.Scripts
{
    // [RequireComponent(typeof(Renderer))]

    public class DSPV_SimulationPostProcessor : MonoBehaviour
    {
        // // The material texture will be used as phosphene activation mask
        protected Material material;
        protected Texture2D input_texture;   //The input texture (from webcam or virtual camera)
        protected Texture2D activation_mask;  //The output texture after preprocessing

        // // The shader that is used for the phosphene simulation
        [SerializeField] protected Shader shader;


        // // TODO use separate image processing shader
        // protected Shader imgprocShader;
        // protected Material imgprocMaterial;

        // // TODO: image processing
        // // OpenCVForUnity
        // protected Mat in_mat;
        // protected Mat out_mat;
        // protected Mat dilateElement;
        // protected bool cannyFiltering;

        // EyeTracking
        protected Vector2 eyePosition;
        protected int gazeLocking; // used as boolean (sent to shader)
        protected bool camLocking;

        // For reading phosphene configuration from JSON
        [FilePath] [SerializeField]
        string phospheneConfigurationFile;
        PhospheneConfiguration phospheneConfiguration; // is loaded from the above JSON file

        // stimulation parameters
        protected float stim;
        protected float[] activation;
        protected float[] memoryTrace;
        [SerializeField] float input_effect = 0.7f;
        [SerializeField] float intensity_decay = 0.8f;
        [SerializeField] float trace_increase = 0.1f;
        [SerializeField] float trace_decay = 0.9f;

        protected void Awake()
        {
            // Initialize material, input texture and shader
            material = new Material(shader);
            // imgprocMaterial = new Material(imgprocShader); TODO

            input_texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);


            // Load phosphene configuration and pass to shader
            phospheneConfiguration = PhospheneConfiguration.load(phospheneConfigurationFile.ToString());
            material.SetVectorArray("_pSpecs", phospheneConfiguration.specifications);
            material.SetInt("_nPhosphenes", phospheneConfiguration.phospheneCount);

            // Initialize other simulation variables
            activation = new float[phospheneConfiguration.phospheneCount];
            memoryTrace = new float[phospheneConfiguration.phospheneCount];

            // // OPENCV TODO
            // in_mat = new Mat(512, 512, CvType.CV_8UC4, new Scalar(0, 0, 0, 255));
            // out_mat = new Mat(512, 512, CvType.CV_8UC4, new Scalar(0, 0, 0, 255));
            // dilateElement = Imgproc.getStructuringElement(Imgproc.MORPH_ELLIPSE, new Size(3, 3)); // was new Size(9, 9))
        }

        // Postprocess the image
        void OnRenderImage (RenderTexture source, RenderTexture destination)
        {
            //1.  Read the pixels from 'source' (which is the active rendertexture by default)
            input_texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            input_texture.Apply();

            //2. Sample a subsection of the input texture for the activation  mask
            // TODO

            //3. Apply image processing
            // TODO

            activation_mask = input_texture;

            //4. Calculate the phosphene activations
            for (int i = 0; i < phospheneConfiguration.phospheneCount; i++){
              Vector2 pos = phospheneConfiguration.specifications[i]; // x and y coordinates
              if (camLocking) {
                pos += eyePosition- new Vector2(0.5f,0.5f);}

              if (pos.x > 0 && pos.x < 1 && pos.y > 0 && pos.y < 1) {
                stim = activation_mask.GetPixelBilinear(pos.x, pos.y).grayscale;}
              else {
              stim = 0;}
              activation[i] *= intensity_decay;
              activation[i] += Math.Max(0,input_effect*(stim-memoryTrace[i]));//);;
              memoryTrace[i] = memoryTrace[i] * trace_decay + trace_increase*stim;

            }

            // 5. apply the phosphene shader
            material.SetTexture("_ActivationMask", activation_mask);
            material.SetFloatArray("activation", activation);
            material.SetVectorArray("_pSpecs", phospheneConfiguration.specifications);
            Graphics.Blit (source, destination, material);
        }

        protected void Update()
        {

            // // User input
            // if (Input.GetKeyDown(KeyCode.Alpha1))
            // {
            //     cannyFiltering = false;
            //     Material.SetFloat("_PhospheneFilter", 0f);
            // }
            //
            // if (Input.GetKeyDown(KeyCode.Alpha2))
            // {
            //     cannyFiltering = true;
            //     Material.SetFloat("_PhospheneFilter", 1f);
            // }
            //
            // if (Input.GetKeyDown(KeyCode.Alpha3))
            // {
            //     cannyFiltering = false;
            //     Material.SetFloat("_PhospheneFilter", 1f);
            // }
            //
            // if (Input.GetKeyDown(KeyCode.Alpha4))
            // {
            //     cannyFiltering = true;
            //     Material.SetFloat("_PhospheneFilter", 0f);
            // }

            if (Input.GetKey(KeyCode.U) || Input.GetKey(KeyCode.J))
            {
                if (Input.GetKey(KeyCode.U)) {eyePosition.y = 0.8f;}
                if (Input.GetKey(KeyCode.J)) {eyePosition.y = 0.2f;}
            }
            else
            {
              eyePosition.y = 0.5f;
            }

            if (Input.GetKey(KeyCode.H) || Input.GetKey(KeyCode.K))
            {
                if (Input.GetKey(KeyCode.H)) {eyePosition.x = 0.2f;}
                if (Input.GetKey(KeyCode.K)) {eyePosition.x = 0.8f;}
            }
            else
            {
              eyePosition.x = 0.5f;
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
              gazeLocking = 1-gazeLocking;
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
              camLocking = !camLocking;
            }


            if (Input.GetKey("escape"))
            {
                Application.Quit();
            }

            //
            material.SetVector("_EyePosition", eyePosition);
            material.SetInt("_GazeLocked", gazeLocking);
        }

        // public void OnSceneChange()
        // {
        //   // var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
        //   // var mpb = new MaterialPropertyBlock();
        //   // foreach (var r in renderers)
        //   // {
        //   //   var id = r.gameObject.GetInstanceID();
        //   //   var layer = r.gameObject.layer;
        //   //   var tag = r.gameObject.tag;
        //   //   if (r == GetComponent<Renderer>()) // the current phosphene simulator quad
        //   //   {
        //   //     Debug.Log(tag);
        //   //     Debug.Log(id);
        //   //     continue;
        //   //   }
        //   //   else{
        //   //     r.material = imgprocMaterial;
        //   //     var mycolor = ColorEncoding.EncodeIDAsColor(id);
        //   //     r.material.SetColor("_ObjectColor", mycolor);
        //   //   }
        //   //
        //
        //     // mpb.SetColor("_ObjectColor", mycolor);
        //     // mpb.SetColor("_CategoryColor", ColorEncoding.EncodeLayerAsColor(layer));
        //     // r.SetPropertyBlock(mpb);
        //   //}
        //   // GetComponent<Renderer>().material = Material = new Material(shader); // restore phosphene rendering quad
        // }

    }
}
