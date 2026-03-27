Shader "UI/TutorialHoleMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Overlay Color", Color) = (0, 0, 0, 0.7)
        _HoleCenter ("Hole Center (Screen UV)", Vector) = (0.5, 0.5, 0, 0)
        _HoleSize ("Hole Size (Screen UV)", Vector) = (0.1, 0.1, 0, 0)
        _HoleRadius ("Corner Radius", Float) = 0.02
        _EdgeSoftness ("Edge Softness", Float) = 0.005

        // UI 기본 속성
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _HoleCenter;
            float4 _HoleSize;
            float _HoleRadius;
            float _EdgeSoftness;

            // 둥근 사각형 SDF
            float roundedBoxSDF(float2 p, float2 halfSize, float radius)
            {
                float2 d = abs(p) - halfSize + radius;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // 구멍 영역 계산
                float2 diff = screenUV - _HoleCenter.xy;

                // 화면 비율 보정
                float aspect = _ScreenParams.x / _ScreenParams.y;
                diff.x *= aspect;
                float2 holeHalf = _HoleSize.xy * 0.5;
                holeHalf.x *= aspect;

                float dist = roundedBoxSDF(diff, holeHalf, _HoleRadius);

                // 구멍 바깥 = 어두운 오버레이, 안쪽 = 투명
                float alpha = smoothstep(-_EdgeSoftness, _EdgeSoftness, dist);

                fixed4 col = _Color;
                col.a *= alpha * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
