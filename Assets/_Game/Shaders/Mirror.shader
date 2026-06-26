Shader "VirtualBrightPlayz/Mirror"
{
    Properties
    {
        // The mirror reflection texture (in mono rendering).
        // Or the mirror reflection texture for the left eye (in stereo rendering).
        _MainTex ("Texture", 2D) = "white" {}

        // The mirror reflection texture for the right eye (only used in stereo rendering).
        _AltTex ("Texture", 2D) = "white" {}

        // When ColorStrength is > 0, the reflection color is mixed with this color.
        // Allows for smoothly activating/deactivating the mirror.
        [MainColor]
        _Color ("Color", Color) = (1, 1, 1, 1)

        // Lerps the reflection color towards Color.
        _ColorStrength ("ColorStrength", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "UnityShaderVariables.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                // UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _AltTex;

            float4 _Color;
            float _ColorStrength;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                // UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // _ColorStrength is at max so no reflection will be visible. Early exit:
                if (_ColorStrength >= 1.0)
                {
                    return _Color;
                }

                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Flip X to account for the default facing direction of the mirror quad
                screenUV.x = 1 - screenUV.x;

                fixed4 col = float4(0, 0, 0, 1);
                if (unity_StereoEyeIndex == 1)
                {
                    col = tex2D(_AltTex, screenUV);
                }
                else
                {
                    col = tex2D(_MainTex, screenUV);
                }

                // Mix with _Color based on _ColorStrength
                return lerp(col, _Color, _ColorStrength);
            }
            ENDCG
        }
    }
}
