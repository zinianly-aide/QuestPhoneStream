Shader "QuestPhoneStream/VRMediaStereo"
{
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        _Fov ("Field of view", Float) = 360
        _Stereo ("SBS stereo", Float) = 0
        _EyeOrder ("Right eye first", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Front
        ZWrite Off
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Fov;
            float _Stereo;
            float _EyeOrder;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 dir = normalize(i.dir);
                if (_Fov < 270 && dir.z < 0) return fixed4(0, 0, 0, 1);
                float u = atan2(dir.x, dir.z) / (2.0 * UNITY_PI) + 0.5;
                float v = 0.5 - asin(clamp(dir.y, -1.0, 1.0)) / UNITY_PI;
                float2 uv = float2(frac(u), saturate(v));
                if (_Stereo > 0.5)
                {
                    bool rightEye = unity_StereoEyeIndex == 1;
                    bool sampleRight = _EyeOrder > 0.5 ? !rightEye : rightEye;
                    uv.x = uv.x * 0.5 + (sampleRight ? 0.5 : 0.0);
                }
                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
