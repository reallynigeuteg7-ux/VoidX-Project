Shader "VoidX/Emblem"
{
    Properties
    {
        [PerRendererData] _MainTex ("Logo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil comparison", Float) = 8
        _Stencil ("Stencil", Float) = 0
        _StencilOp ("Stencil operation", Float) = 0
        _StencilWriteMask ("Stencil write mask", Float) = 255
        _StencilReadMask ("Stencil read mask", Float) = 255
        _ColorMask ("Color mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct output { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex; float4 _Color;
            output vert(input v) { output o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.uv = v.uv; return o; }
            fixed4 frag(output i) : SV_Target { fixed4 c = tex2D(_MainTex, i.uv); float bright = max(c.r, max(c.g, c.b)); c.a *= smoothstep(.02, .18, bright); return c * i.color; }
            ENDCG
        }
    }
}
