Shader "MyShader/JJURP/JJP3_9"
{
    Properties
    {
        [Foldout(1,2,0,1)]
        _BaseParameter("BaseParameter_Foldout",float) = 1
        _Color("Color",Color) = (1,1,1,1)
        _MainTex("MainTex",2D) = "white"{}
        _Sequence("Row(X) Column(Y) Speed(Z)",Vector) = (1,1,1,1)

        [Enum_Switch(ChannelAll, ChannelA)]_ChannelType("ChannelSelect", float) = 0.0


        [Foldout(1,2,0,1)]
        _OtherSetting("OtherSetting_Foldout",float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcFactor("SrcFactor",int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstFactor("DstFactor",int) = 10
    }
    SubShader
    {
        Tags
        {
            //告诉引擎，该Shader只用于 URP 渲染管线
            "RenderPipeline"="UniversalPipeline"
            //渲染类型
            "RenderType"="Transparent"
            //渲染队列
            "Queue"="Transparent"
        }
        Blend [_SrcFactor] [_DstFactor] 
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _ _CHANNELTYPE_CHANNELALL _CHANNELTYPE_CHANNELA

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attribute
            {
                float3 vertexOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varying
            {
                float4 vertexCS : SV_POSITION;
                float2 uv : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _MainTex_ST;
            half4 _Sequence;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varying vert(Attribute v)
            {
                Varying o;
                o.vertexCS = TransformObjectToHClip(v.vertexOS);
                o.uv = float2(v.uv.x / _Sequence.y, v.uv.y / _Sequence.x + (_Sequence.x - 1) / _Sequence.x);
               float frameIndex = frac(_Time.y * _Sequence.z); // 控制帧速率，帧的循环由 Speed 控制
                float currentFrameX = floor(frameIndex * _Sequence.y); // 当前列
                float currentFrameY = floor(frameIndex * _Sequence.x); // 当前行

                o.uv.x += currentFrameX / _Sequence.y;
                o.uv.y -= currentFrameY / _Sequence.x;

                //o.uv.x += floor(_Time.y);
                //o.uv = float2(v.uv.x/4,v.uv.y/4);
                //o.uv = TRANSFORM_TEX(v.uv,_MainTex);
                o.fogCoord = ComputeFogFactor(o.vertexCS.z);
                return o;
            }

            half4 frag(Varying i) : SV_Target
            {
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 finalCol = 0;
                #ifdef _CHANNELTYPE_CHANNELALL
                    finalCol = mainTex;
                #elif defined _CHANNELTYPE_CHANNELA
                    half a = mainTex.a;
                    finalCol = half4(a, a, a, a);
                #endif


                float4 col = finalCol * _Color;
                col.rgb = MixFog(col, i.fogCoord);
                // col.rgb = col.rgb * col.a;
                return half4(col.xyz,col.a);
            }
            ENDHLSL
        }
    }
    CustomEditor"Scarecrow.SimpleShaderGUI"
}