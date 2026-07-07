Shader "KillRitual/BuiltIn/BodyDisintegrate"
{
    Properties
    {
        _Color ("Color Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}

        _Alpha ("Alpha", Range(0,1)) = 1

        [Header(Noisy Erase)]
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EraseMinY ("Erase Min Y", Float) = -1
        _EraseMaxY ("Erase Max Y", Float) = 2
        _EraseDirection ("Erase Direction 0 BottomToTop 1 TopToBottom", Range(0,1)) = 1

        _NoiseScale ("Noise Scale", Float) = 18
        _NoiseStrength ("Noise Strength", Range(0,0.5)) = 0.12

        [Header(Standard)]
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        LOD 250
        Cull Back
        ZWrite On

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;

        fixed4 _Color;
        float _Alpha;

        float _DissolveAmount;
        float _EraseMinY;
        float _EraseMaxY;
        float _EraseDirection;

        float _NoiseScale;
        float _NoiseStrength;

        half _Metallic;
        half _Smoothness;

        struct Input
        {
            float2 uv_MainTex;
            float3 localPos;
            float3 worldPos;
        };

        float Hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);

            f = f * f * (3.0 - 2.0 * f);

            float n000 = Hash31(i + float3(0, 0, 0));
            float n100 = Hash31(i + float3(1, 0, 0));
            float n010 = Hash31(i + float3(0, 1, 0));
            float n110 = Hash31(i + float3(1, 1, 0));

            float n001 = Hash31(i + float3(0, 0, 1));
            float n101 = Hash31(i + float3(1, 0, 1));
            float n011 = Hash31(i + float3(0, 1, 1));
            float n111 = Hash31(i + float3(1, 1, 1));

            float nx00 = lerp(n000, n100, f.x);
            float nx10 = lerp(n010, n110, f.x);
            float nx01 = lerp(n001, n101, f.x);
            float nx11 = lerp(n011, n111, f.x);

            float nxy0 = lerp(nx00, nx10, f.y);
            float nxy1 = lerp(nx01, nx11, f.y);

            return lerp(nxy0, nxy1, f.z);
        }

        float GetDissolveActive()
        {
            // DissolveAmount가 0일 때는 노이즈가 삭제선에 영향을 주지 않게 한다.
            return smoothstep(0.01, 0.06, _DissolveAmount);
        }

        float GetEraseCoord(float localY)
        {
            float heightRange = max(_EraseMaxY - _EraseMinY, 0.0001);
            float height01 = saturate((localY - _EraseMinY) / heightRange);

            // 0 = 아래에서 위로 삭제
            // 1 = 위에서 아래로 삭제
            return lerp(height01, 1.0 - height01, _EraseDirection);
        }

        float GetNoisyThreshold(float3 worldPos)
        {
            float active = GetDissolveActive();

            float noise = ValueNoise(worldPos * _NoiseScale);
            float noiseOffset = (noise - 0.5) * _NoiseStrength * active;

            return saturate(_DissolveAmount + noiseOffset);
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            o.localPos = v.vertex.xyz;
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            float eraseCoord = GetEraseCoord(IN.localPos.y);
            float threshold = GetNoisyThreshold(IN.worldPos);

            // 핵심:
            // 높이 방향으로 사라지되, 삭제 경계만 노이즈로 흔들린다.
            // 색 변화, 발광, 경계선 효과 없음.
            clip(eraseCoord - threshold);

            float finalAlpha = saturate(albedo.a * _Alpha);
            clip(finalAlpha - 0.01);

            o.Albedo = albedo.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Emission = 0;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}