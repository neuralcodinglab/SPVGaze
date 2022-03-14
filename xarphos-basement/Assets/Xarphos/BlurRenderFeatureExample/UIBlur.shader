Shader "Unlit/UIBlur"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _Width ("Width of Blur", Range(0, 8)) = 4
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest On

        HLSLINCLUDE
        #include "UnityCG.cginc"

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
            float4 screenPos : TEXCOORD1;
        };

        float _Width;
        sampler2D _UIBlurTexture;
        float4 _UIBlurTexture_TexelSize;

        v2f vert(appdata v)
        {
            v2f o = (v2f)0;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;

            o.screenPos = ComputeScreenPos(o.vertex);
            return o;
        }

        // Note: This is a really inefficient way of blurring
        // We only use it to smooth out our downscaled texture
        float4 BoxFilter(float2 screenUV)
        {
            const int width = _Width;
            float4 result = 0;

            int count = 0;
            for (int x = -width; x <= width; x++)
            {
                for (int y = -width; y <= width; y++)
                {
                    result += tex2D(_UIBlurTexture, screenUV + float2(x, y) * _UIBlurTexture_TexelSize.xy);
                    count++;
                }
            }

            return result / count;
        }

        float4 frag(v2f i) : SV_Target
        {
            return BoxFilter(i.screenPos.xy / i.screenPos.w);
        }
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}