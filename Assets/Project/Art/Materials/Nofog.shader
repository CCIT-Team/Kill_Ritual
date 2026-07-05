Shader "KillRitual/VFX/Unlit Alpha No Fog"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 1, 1, 0.7)
        _Intensity ("Intensity", Range(0, 5)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _AlphaPower ("Alpha Power", Range(0.1, 5)) = 1
        _VerticalFade ("Vertical Fade", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off

            // 색 보존형 투명 합성
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _Intensity;
            float _Alpha;
            float _AlphaPower;
            float _VerticalFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                float vertical = saturate(1.0 - abs(i.uv.y - 0.5) * 2.0);
                vertical = pow(vertical, _VerticalFade);

                fixed4 col = tex * _TintColor * i.color;

                col.rgb *= _Intensity;
                col.a *= _Alpha * vertical;
                col.a = pow(col.a, _AlphaPower);

                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}