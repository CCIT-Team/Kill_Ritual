Shader "KillRitual/UI/DamageVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Tint ("Tint", Color) = (1, 0, 0, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0

        _EdgeStart ("Edge Start", Range(0, 1)) = 0.48
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.45
        _VerticalStretch ("Vertical Stretch", Range(0.1, 2)) = 0.85
        _AlphaPower ("Alpha Power", Range(0.2, 5)) = 1.35

        _CenterClearRadius ("Center Clear Radius", Range(0, 1)) = 0.22
        _CenterClearSoftness ("Center Clear Softness", Range(0.01, 1)) = 0.35

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "DamageVignette"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _MainTex_ST;
            float4 _ClipRect;

            fixed4 _Tint;
            float _Intensity;

            float _EdgeStart;
            float _EdgeSoftness;
            float _VerticalStretch;
            float _AlphaPower;

            float _CenterClearRadius;
            float _CenterClearSoftness;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 0~1 UV를 -1~1 화면 좌표로 변환.
                float2 p = uv * 2.0 - 1.0;

                // 세로 방향을 약간 눌러서 위/아래보다 좌우 테두리가 과하게 죽지 않게 함.
                p.y *= _VerticalStretch;

                float distanceFromCenter = length(p);

                // 화면 가장자리로 갈수록 강해지는 비네트.
                float edge = smoothstep(
                    _EdgeStart,
                    _EdgeStart + _EdgeSoftness,
                    distanceFromCenter
                );

                // 중앙 조준 영역 보호.
                float centerClear = smoothstep(
                    _CenterClearRadius,
                    _CenterClearRadius + _CenterClearSoftness,
                    distanceFromCenter
                );

                float alpha = edge * centerClear;
                alpha = pow(saturate(alpha), _AlphaPower);
                alpha *= saturate(_Intensity) * _Tint.a;

                fixed4 color = _Tint;
                color.a = alpha * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}