Shader "VoidX/Ink"
{
    Properties
    {
        _BaseColor ("Paper ink", Color) = (.004,.004,.004,1)
        _EdgeColor ("White contour", Color) = (1.5,1.5,1.5,1)
        _LineWidth ("Contour pixels", Float) = 1.1
        _HatchTex ("Etched strokes", 2D) = "black" {}
        _Hatch ("Stroke strength", Float) = 0
        _Rim ("Curved silhouette", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_HatchTex); SAMPLER(sampler_HatchTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _EdgeColor;
                float _LineWidth, _Hatch, _Rim;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float3 bary:TEXCOORD1; float3 edges:TEXCOORD2; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float3 bary:TEXCOORD3; float3 edges:TEXCOORD4; half fog:TEXCOORD5; };
            V vert(A v)
            {
                V o; o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; o.bary=v.bary; o.edges=v.edges; o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(V i):SV_Target
            {
                float3 width=max(fwidth(i.bary),.00001);
                float3 distanceToEdge=i.bary/width + (1-i.edges)*10000;
                float closest=min(distanceToEdge.x,min(distanceToEdge.y,distanceToEdge.z));
                half contour=1-smoothstep(_LineWidth-.45,_LineWidth+.45,closest);
                half facing=abs(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world)));
                half rim=(1-smoothstep(.05,.20,facing))*_Rim*saturate(length(fwidth(normalize(i.normal)))*150);
                half strokes=SAMPLE_TEXTURE2D(_HatchTex,sampler_HatchTex,i.uv).r*_Hatch;
                half3 color=lerp(_BaseColor.rgb,_EdgeColor.rgb,max(contour,rim));
                color+=strokes;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
