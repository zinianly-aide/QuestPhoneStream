Shader "QuestPhoneStream/UnlitVideo"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "black" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull [_Cull]
        ZWrite On
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return tex2D(_MainTex, input.uv);
            }
            ENDCG
        }
    }

    Fallback Off
}
