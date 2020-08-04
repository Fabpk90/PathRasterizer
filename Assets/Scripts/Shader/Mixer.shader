Shader "Hidden/Mixer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PathTracedTexture ("Texture", 2D) = "white" {}
        _PathTracedDepth ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Builtin/BuiltinData.hlsl"

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

            struct appdata
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
               float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
                         
                return o;
            }
            
            TEXTURE2D(_PathTracedTexture);
            TEXTURE2D(_PathTracedDepth);
            TEXTURE2D(_MainTex);

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                //flipping Y
                i.texcoord.y = abs(i.texcoord.y - 1);
                    
                uint2 positionSS = i.texcoord * _ScreenSize.xy;
                float4 color = float4(SAMPLE_TEXTURE2D_X_LOD(_ColorPyramidTexture, s_trilinear_clamp_sampler, positionSS, 0).rgb, 1.0f);
                float depth = Linear01Depth(LoadCameraDepth(positionSS), _ZBufferParams);
                float depthPathTraced = LOAD_TEXTURE2D_X(_PathTracedDepth, positionSS);
                float cmp = depth < depthPathTraced;
     
                return LOAD_TEXTURE2D_X(_PathTracedTexture, positionSS);
                //return color * (cmp) + LOAD_TEXTURE2D_X(_PathTracedTexture, i.texcoord) *  (1 - cmp);
            }
            ENDHLSL
        }
    }
}