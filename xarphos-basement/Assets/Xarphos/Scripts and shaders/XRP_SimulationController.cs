using UnityEngine;
using Rect = UnityEngine.Rect;

namespace Xarphos.Scripts
{
    [RequireComponent(typeof(Renderer))]

    public class XRP_SimulationController : MonoBehaviour
    {
        // The material texture will be used as phosphene activation mask
        protected Material Material;
        protected Texture2D input_texture;   //The input texture (webcam or virtual camera)
        protected Texture2D activation_mask; //The output texture after preprocessing

        // The shader is used for the phosphene simulation
        [SerializeField] protected Shader shader;
        private static readonly int _PhospheneActivationMask = Shader.PropertyToID("_MaskTex"); // Accessible by shader

        // In this example the input texture is grabbed from virtual camera 
        protected Camera playerCam;
        protected RenderTexture renderTexture;
        //protected WebCamTexture webcamTexture; // can be used instead of virtual camera

        protected void Awake()
        {
            // Initialize material, input texture and shader
            GetComponent<Renderer>().material = Material = new Material(shader);
            input_texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);

            // Initialize virtual camera
            renderTexture = new RenderTexture(512, 512, 24);
            playerCam = GameObject.Find("PlayerCamera").GetComponent<Camera>();
            playerCam.targetTexture = renderTexture;

        }


        protected void Update()
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

                /*
                // Canny edge detection example (requires OpenCVForUnity package)
                Utils.textureToMat(in_mat, input_texture);
                Imgproc.Canny(in_mat, out_mat, 100, 200);
                Utils.matToTexture2D(out_mat, activation_mask);
                */
            // ---------------------------------

            // 3. Apply activation mask as texture (so it can be accessed by shader)
            Material.SetTexture(_PhospheneActivationMask, activation_mask);


            // Escape
            if (Input.GetKey("escape"))
            {
                Application.Quit();
            }

        }
        
        

    }
}





