Shader "Custom/XRP_PhosShader"
{
    Properties
    {   
       _MaskTex ("MaskTex", 2D) = "black" { }
       _Size_0 ("Size_0", Range(0, 0.5)) = 0.2
       _Size_var("_Size_var",Range(0, 1)) = 1
       _Magnification ("Magnification", Range(0, 0.5)) = 0
       _Resolution ("Resolution", Vector) = (35, 35, 0, 0)
       _Jitter("Jitter",Range(0, 0.5)) = 0.5
       _Intensity_var("_Intensity_var",Range(0, 1)) = 1
       _Brightness ("Brightness", Range(0, 3)) = 0.2
       _Saturation ("Saturation", Range(0, 1)) = 0
       _Dropout("Dropout", Range(0.0,0.5)) = 0.1
    }

    SubShader
    {
        Lighting Off
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #include "xrp_phosphene_vision.cginc"


            #pragma vertex vertex_program 
            #pragma fragment frag
            #pragma target 3.0

            struct AppData
            {
                float2 uv: TEXCOORD0;
                float4 vertex: POSITION;
            };
            
            struct VertexData
            {
                float2 uv: TEXCOORD0;
                float4 vertex: SV_POSITION;
            };


            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            float4 _MaskTex_TexelSize;

            float2 _Resolution;
            float _Size_0;
            float _Magnification;
            float _Brightness;
            float _Saturation;
            float _Dropout;
            float _Intensity_var;
            float _Size_var;
            float _Jitter;

            VertexData vertex_program(AppData inputs)
            {
                VertexData outputs;
                outputs.uv = TRANSFORM_TEX(inputs.uv, _MaskTex);
                outputs.vertex = UnityObjectToClipPos(inputs.vertex);            
                return outputs;
            }
            
            float4 frag(VertexData inputs) : SV_Target
            {
                float2 position = inputs.uv;
                float2 intensity_params = float2(_Brightness,_Saturation); // Base brighness, color saturation
                float2 size_params = float2(_Size_0,_Magnification); // Base size, cortical magnification
                float4 noise_params = float4(_Jitter,_Intensity_var,_Size_var,_Dropout); //  positional jitter, intensity variation, size variation, dropout
                return xrp_phosphene_filter(_MaskTex, position, _Resolution,intensity_params,size_params,noise_params);
            }
            ENDCG
        }
    }
}