// A spark: a white-hot core that cools into the star's color at the edge, added to the light behind it so
// overlapping sparks glow brighter.
Shader "Zenith/Spark"
{
    Properties
    {
        _Boost ("Brightness", Float) = 1.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Boost;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                // white at the center, the star's color at 18%, a third as strong at 50%, gone at the edge
                half3 tint = i.color.rgb;
            #if !UNITY_COLORSPACE_GAMMA
                tint = SRGBToLinear(tint);
            #endif
                half3 c = lerp(half3(1, 1, 1), tint, smoothstep(0.0, 0.18, d));
                float a = d < 0.18 ? 1.0 : d < 0.5 ? lerp(1.0, 0.33, (d - 0.18) / 0.32) : lerp(0.33, 0.0, saturate((d - 0.5) / 0.5));
                return half4(c * a * i.color.a * _Boost, 0);
            }
            ENDHLSL
        }
    }
}
