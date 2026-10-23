Shader "QuestPhoneStream/BirthdayDecor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Metallic ("Gold Metallic Accent", Range(0,1)) = 0
        _Smoothness ("Surface Gloss", Range(0,1)) = 0.35
        _EmissionColor ("Warm LED Emission", Color) = (0, 0, 0, 0)
        _Unlit ("Unlit Photo Surface", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off
        ZWrite On
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
            fixed4 _Color;
            half _Metallic;
            half _Smoothness;
            half4 _EmissionColor;
            half _Unlit;
            struct Input
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Output
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Output Vert(Input input)
            {
                Output output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }
            fixed4 Frag(Output input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 albedo = tex2D(_MainTex, input.uv) * _Color;
                // One analytic soft key light: no extra real-time per-object lights or shadows.
                // The actual CAD house is rendered separately using its bundled materials.
                half3 normalWorld = normalize(input.worldNormal);
                half3 key = normalize(half3(0.38,0.82,-0.41));
                half diffuse = saturate(dot(normalWorld,key));
                half bounce = saturate(normalWorld.y*0.5+0.5);
                half illumination = min(1.0h,0.62h + 0.28h*diffuse + 0.10h*bounce);
                half3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPosition);
                half3 halfDir = normalize(key + viewDir);
                half gloss = pow(saturate(dot(normalWorld,halfDir)),lerp(10.0h,56.0h,_Smoothness));
                half specIntensity = (0.04h + 0.27h*_Metallic) * gloss;
                half3 lighted = albedo.rgb * illumination + specIntensity *
                    lerp(half3(1,1,1),albedo.rgb,_Metallic) + _EmissionColor.rgb;
                return half4(lerp(lighted,albedo.rgb,_Unlit),albedo.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
