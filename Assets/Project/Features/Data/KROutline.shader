Shader "KillRitual/Outline"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.5, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
        _OutlineEnabled ("Outline Enabled", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        // ── Pass 1: 테두리 패스 ───────────────────────────────────────
        // 오브젝트를 법선 방향으로 살짝 확대하고 단색으로 렌더링합니다.
        // 앞면을 컬링하여 원본 오브젝트 뒤로 삐져나온 부분만 보이게 합니다.
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _OutlineColor;
            float  _OutlineWidth;
            float  _OutlineEnabled;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                // OutlineEnabled가 0이면 버텍스를 카메라 뒤로 보내 렌더링을 숨깁니다.
                if (_OutlineEnabled < 0.5)
                {
                    o.pos = float4(0, 0, -2, 1);
                    return o;
                }
                float3 norm = normalize(v.normal);
                float4 pos  = v.vertex + float4(norm * _OutlineWidth, 0);
                o.pos = UnityObjectToClipPos(pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // ── Pass 2: 원본 오브젝트 패스 ───────────────────────────────
        // 기존 Standard 셰이더처럼 정상적으로 렌더링합니다.
        Pass
        {
            Name "BASE"
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }
}
