Shader "QuestPhoneStream/GaussianSplatPoc"
{
    Properties
    {
        _GlobalSize ("Global Size", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
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
                float2 uv2 : TEXCOORD1;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float _GlobalSize;

            v2f vert(appdata v)
            {
                v2f o;
                float3 center = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 right = float3(unity_CameraToWorld._m00, unity_CameraToWorld._m10, unity_CameraToWorld._m20);
                float3 up = float3(unity_CameraToWorld._m01, unity_CameraToWorld._m11, unity_CameraToWorld._m21);
                float2 corner = (v.uv - 0.5) * 2.0;
                float radius = max(0.0005, v.uv2.x * _GlobalSize);
                float3 world = center + right * corner.x * radius + up * corner.y * radius;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.uv = corner;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                if (r2 > 1.0) discard;
                float gaussian = exp(-3.5 * r2);
                fixed4 color = i.color;
                color.a *= gaussian;
                return color;
            }
            ENDCG
        }
    }
}
