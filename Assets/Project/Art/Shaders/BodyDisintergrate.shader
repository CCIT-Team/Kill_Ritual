Shader "KillRitual/BuiltIn/BodyDisintegrate"
{
    Properties
    {
        _Color ("Color Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}

        _Alpha ("Alpha", Range(0,1)) = 1

        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _NoiseScale ("Noise Scale", Float) = 18
        _EdgeWidth ("Edge Width", Range(0.001, 0.25)) = 0.06

        _EdgeColor ("Edge Color", Color) = (1.0, 0.65, 0.25, 1.0)
        _EdgeEmission ("Edge Emission", Float) = 2.5

        _NormalPush ("Normal Push", Float) = 0.04
        _UpPush ("Up Push", Float) = 0.08

        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 250
        Cull Back

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade addshadow vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;

        fixed4 _Color;
        float _Alpha;

        half _Metallic;
        half _Smoothness;

        float _DissolveAmount;
        float _NoiseScale;
        float _EdgeWidth;

        fixed4 _EdgeColor;
        float _EdgeEmission;

        float _NormalPush;
        float _UpPush;

        struct Input
        {
            float2 uv_MainTex;
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
            // DissolveAmount가 0일 때 edge emission / vertex push가 보이지 않게 막는다.
            // 0.01 이전까지는 사실상 꺼지고, 이후부터 서서히 살아난다.
            return smoothstep(0.01, 0.06, _DissolveAmount);
        }

        void vert(inout appdata_full v)
        {
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            float noise = ValueNoise(worldPos * _NoiseScale);

            float dissolveActive = GetDissolveActive();

            float edge = 1.0 - saturate(abs(noise - _DissolveAmount) / max(_EdgeWidth, 0.0001));
            edge *= dissolveActive;

            float motion = saturate(_DissolveAmount * 1.35) * dissolveActive;

            v.vertex.xyz += v.normal * edge * _NormalPush * motion;
            v.vertex.y += edge * _UpPush * motion;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float noise = ValueNoise(IN.worldPos * _NoiseScale);

            // DissolveAmount가 0일 때는 절대 표면을 자르지 않는다.
            // 0보다 커진 뒤부터만 clip을 적용한다.
            float dissolveCut = max(_DissolveAmount, -0.01);
            clip(noise - dissolveCut);

            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            float dissolveActive = GetDissolveActive();

            float edge = 1.0 - saturate(abs(noise - _DissolveAmount) / max(_EdgeWidth, 0.0001));
            edge *= dissolveActive;

            o.Albedo = albedo.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;

            // DissolveAmount가 0이면 발광도 0.
            o.Emission = _EdgeColor.rgb * edge * _EdgeEmission;

            o.Alpha = saturate(albedo.a * _Alpha);
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Diffuse"
}