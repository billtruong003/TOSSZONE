Shader "CleanRender/UI/ToonMirror"
{
    Properties
    {
        [Header(Reflection)]
        _MainTex       ("Mirror Texture", 2D) = "white" {}
        [MainColor]
        _Color         ("Color", Color)                   = (1, 1, 1, 1)
        _ColorStrength ("Color Strength (fade)", Range(0, 1)) = 0
        [Toggle] _FlipX ("Reverse (Flip Horizontal)", Float) = 0

        [Header(Toon Glass Lines)]
        _LineColor     ("Line Color", Color)              = (1, 1, 1, 1)
        _LineOffset    ("Line Offset", Float)             = 1.0
        _LineScale     ("Line Scale", Float)              = 0.3
        _LineCountA    ("Line Count A (exp)", Float)      = 3.0
        _LineCountB    ("Line Count B (linear)", Float)   = 1.0
        _LineWidth     ("Line Width", Range(0, 1))        = 0.5
        _LineStrength  ("Line Strength", Range(0, 1))     = 0.35

        [Header(Vignette)]
        _Vignette      ("Vignette Strength", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "MirrorSurface"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _ColorStrength;
                float  _FlipX;

                half4  _LineColor;
                float  _LineOffset;
                float  _LineScale;
                float  _LineCountA;
                float  _LineCountB;
                half   _LineWidth;
                half   _LineStrength;

                half   _Vignette;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 meshUV      : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                half3  tangentWS   : TEXCOORD2;
                half3  bitangentWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs posIn = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrmIn = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                o.positionCS  = posIn.positionCS;
                o.positionWS  = posIn.positionWS;
                o.meshUV      = TRANSFORM_TEX(input.uv, _MainTex);
                o.tangentWS   = (half3)nrmIn.tangentWS;
                o.bitangentWS = (half3)nrmIn.bitangentWS;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Early-exit when fully faded
                if (_ColorStrength >= 1.0h)
                    return half4(_Color.rgb, 1.0h);

                // Mesh UV — static sampling, no parallax when viewer rotates.
                // Mirror camera is placed statically in scene; RT maps 1:1 onto quad.
                float2 reflUV = input.meshUV;
                reflUV.x = lerp(reflUV.x, 1.0 - reflUV.x, _FlipX);
                half3 reflCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, reflUV).rgb;

                half3 col = lerp(reflCol, _Color.rgb, _ColorStrength);

                // ── Toon glass diagonal lines (slide with camera — stylistic) ──
                float3 camRel  = input.positionWS - _WorldSpaceCameraPos;
                float  u       = dot(camRel, input.tangentWS);
                float  v       = dot(camRel, input.bitangentWS);
                float  diag    = abs(u + v);
                float  shifted = saturate((diag - _LineOffset) * _LineScale);
                float  powered = pow(saturate(1.0 - shifted), _LineCountA);
                float  pattern = frac(powered * _LineCountB + 0.01);
                float  lines   = step(_LineWidth, pattern) * _LineStrength;

                col = lerp(col, _LineColor.rgb, lines);

                // Mesh-space vignette
                float2 centered = input.meshUV * 2.0 - 1.0;
                col *= 1.0 - dot(centered, centered) * _Vignette;

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}