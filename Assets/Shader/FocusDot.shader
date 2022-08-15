Shader "Xarphos/FocusDot"
{
    Properties
    {
        _EyePositionLeft("_EyePositionLeft", Vector) = (0.5, 0.5, 0., 0.)
        _EyePositionRight("_EyePositionRight", Vector) = (0.5, 0.5, 0., 0.)
        _MainTex ("_MainTex", 2D) = "black" {}
        _RenderPoint ("_RenderPoint", int) = 1
    }

    SubShader
    {
        Lighting Off
        Blend One Zero

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            float4 _EyePositionLeft;
            float4 _EyePositionRight;

            int _RenderPoint;
            
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            
            #include "UnityCG.cginc"


            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex: SV_POSITION;
                float2 uv: TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                return o;
            }
            
            fixed4 frag(v2f  i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                fixed4 col = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv);
                fixed4 fixedCol = fixed4(1,0,0,0);
                
                fixed4 eyepos = lerp(_EyePositionLeft, _EyePositionRight, unity_StereoEyeIndex);

                int lookingAtPixel = _RenderPoint * (distance(i.uv, eyepos.rg) < 0.002); 

                return lerp(col, fixedCol, lookingAtPixel);
            }
            ENDCG
        }
    }
}
