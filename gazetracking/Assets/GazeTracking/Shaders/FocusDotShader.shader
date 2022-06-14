Shader "Xarphos/FocusDotShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FocusDotColor ("_FocusDotColor", COLOR) = (1., 0., 0., 0.)
        _LeftEyePos ("_LeftEyePos", Vector) = (.5, .5, 0, 0)
        _RightEyePos ("_RightEyePos", Vector) = (.5, .5, 0, 0)
    }
    
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID 
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            fixed2 _LeftEyePos;
            fixed2 _RightEyePos;
            fixed4 _FocusDotColor;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                // sample the texture
                const fixed4 col = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv);

                const fixed2 eye_pos = lerp(_LeftEyePos, _RightEyePos, unity_StereoEyeIndex);
                const int is_on_eye_pos = distance(i.uv, eye_pos.xy) < 0.002;
                
                return lerp(col, _FocusDotColor, is_on_eye_pos);
            }
            ENDCG
        }
    }
}
