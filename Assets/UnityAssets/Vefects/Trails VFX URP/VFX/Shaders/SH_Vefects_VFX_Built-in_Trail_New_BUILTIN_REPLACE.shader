// Built-in Render Pipeline replacement for:
// Original: Vefects/SH_Vefects_VFX_URP_Trail_New
// Purpose: keep the same material property names so existing Vefects trail materials can reuse their values.
// Note: This is a practical Built-in approximation of the URP/Amplify unlit forward pass.
// It does not reproduce URP-only depth/normal/decal/rendering-layer passes.

Shader "Vefects/SH_Vefects_VFX_URP_Trail_New"
{
    Properties
    {
        [HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        _EmissiveIntensity("Emissive Intensity", Float) = 1
        _AlphaAffectsOpacity("Alpha Affects Opacity", Float) = 1
        _OverallSpeed("Overall Speed", Float) = 1

        [Space(33)][Header(Color)][Space(13)]
        _Color("Color", 2D) = "white" {}
        _ColorUVScale("Color UV Scale", Vector) = (1,1,0,0)
        _ColorPanSpeed("Color Pan Speed", Vector) = (0,0,0,0)
        _Color01("Color 01", Color) = (1,1,1,0)
        _Color02("Color 02", Color) = (1,1,1,0)
        _ColorSmoothstep("Color Smoothstep", Float) = 0
        _ColorSmoothstepSmoothness("Color Smoothstep Smoothness", Float) = 1

        [Space(33)][Header(Distortion)][Space(13)]
        _Distortion("Distortion", 2D) = "white" {}
        _DistortionUVScale("Distortion UV Scale", Vector) = (1,1,0,0)
        _DistortionPanSpeed("Distortion Pan Speed", Vector) = (0,0,0,0)
        _DistortionAmount("Distortion Amount", Float) = 0.1

        [Space(33)][Header(Erosion)][Space(13)]
        _Erosion("Erosion", 2D) = "white" {}
        _ErosionUVScale("Erosion UV Scale", Vector) = (1,1,0,0)
        _ErosionPanSpeed("Erosion Pan Speed", Vector) = (0,0,0,0)
        _ErosionSmoothstep("Erosion Smoothstep", Float) = 0
        _ErosionSmoothstepSmoothness("Erosion Smoothstep Smoothness", Float) = 1

        [Space(33)][Header(Mask)][Space(13)]
        _Mask("Mask", 2D) = "white" {}
        _MaskUVScale("Mask UV Scale", Vector) = (1,1,0,0)
        _MaskPanSpeed("Mask Pan Speed", Vector) = (0,0,0,0)
        _MaskDistortionIntensity("Mask Distortion Intensity", Float) = 1
        _MaskSmoothstep("Mask Smoothstep", Float) = 0
        _MaskSmoothstepSmoothness("Mask Smoothstep Smoothness", Float) = 1

        [Space(33)][Header(AR)][Space(13)]
        _Cull("Cull", Float) = 2
        _Src("Src", Float) = 5
        _Dst("Dst", Float) = 10
        _ZWrite("ZWrite", Float) = 0
        _ZTest("ZTest", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        LOD 100

        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Blend [_Src] [_Dst], One OneMinusSrcAlpha
        ColorMask RGBA
        Lighting Off

        Pass
        {
            Name "Forward"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            sampler2D _Color;
            sampler2D _Distortion;
            sampler2D _Erosion;
            sampler2D _Mask;

            float4 _Color01;
            float4 _Color02;

            float2 _ColorUVScale;
            float2 _ColorPanSpeed;
            float _ColorSmoothstep;
            float _ColorSmoothstepSmoothness;

            float2 _DistortionUVScale;
            float2 _DistortionPanSpeed;
            float _DistortionAmount;

            float2 _ErosionUVScale;
            float2 _ErosionPanSpeed;
            float _ErosionSmoothstep;
            float _ErosionSmoothstepSmoothness;

            float2 _MaskUVScale;
            float2 _MaskPanSpeed;
            float _MaskDistortionIntensity;
            float _MaskSmoothstep;
            float _MaskSmoothstepSmoothness;

            float _OverallSpeed;
            float _EmissiveIntensity;
            float _AlphaAffectsOpacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.uv;

                // URP ShaderGraph Time output is approximated with built-in _Time.y.
                // If the motion becomes much faster/slower than the original, replace _Time.y with _Time.x or _Time.y * 0.05.
                float t = _OverallSpeed * _Time.y;

                float2 distortionUV = uv * _DistortionUVScale + t * _DistortionPanSpeed;
                float distortion = tex2D(_Distortion, distortionUV).g * _DistortionAmount;

                float2 maskBaseUV = (uv + float2(0.28, 0.0)) * _MaskUVScale + t * _MaskPanSpeed;
                float4 maskTex = tex2D(_Mask, maskBaseUV + distortion * _MaskDistortionIntensity);
                float mask = smoothstep(_MaskSmoothstep, _MaskSmoothstep + _MaskSmoothstepSmoothness, maskTex.g);

                float2 erosionUV = uv * _ErosionUVScale + t * _ErosionPanSpeed;
                float erosion = smoothstep(
                    _ErosionSmoothstep,
                    _ErosionSmoothstep + _ErosionSmoothstepSmoothness,
                    tex2D(_Erosion, erosionUV + distortion).g
                );

                float visibility = saturate(saturate(mask) - saturate(erosion - i.color.a));

                float2 colorUV = uv * _ColorUVScale + t * _ColorPanSpeed;
                float colorMask = smoothstep(
                    _ColorSmoothstep,
                    _ColorSmoothstep + _ColorSmoothstepSmoothness,
                    tex2D(_Color, colorUV).g
                );

                float4 gradientColor = lerp(_Color01, _Color02, saturate(colorMask));

                float alphaByVertex = saturate(visibility * i.color.a);
                float alpha = lerp(visibility, alphaByVertex, saturate(_AlphaAffectsOpacity));

                float3 rgb = i.color.rgb * visibility * gradientColor.rgb * _EmissiveIntensity;

                fixed4 col = fixed4(rgb, alpha);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack "Particles/Standard Unlit"
}
