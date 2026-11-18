// The board: a smoked-glass panel with a soft shadow under it and a dimple for every cell, in one draw.
Shader "Zenith/Board"
{
    Properties
    {
        _Grid ("Cells, gap, pad, alpha", Vector) = (4, 0.032, 0.12, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One OneMinusSrcAlpha
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
                float4 _Grid;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float roundRect(float2 p, float2 half_, float r)
            {
                float2 q = abs(p) - half_ + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float n = _Grid.x;
                float gap = _Grid.y;
                float pad = _Grid.z;
                float alpha = _Grid.w;
                float2 b = (i.uv - 0.5) * (1.0 + 2.0 * pad) + 0.5; // the board spans 0..1
                float cell = (1.0 - gap * (n + 1.0)) / n;
                float2 p = b - 0.5;
                float d = roundRect(p, float2(0.5, 0.5), cell * 0.24);
                float aa = max(fwidth(d), 1e-4);
                float inside = 1.0 - smoothstep(-aa, aa, d);

                // the shadow it casts, offset downward
                float sd = roundRect(p + float2(0.0, 0.045), float2(0.46, 0.46), cell * 0.24);
                float shadow = (1.0 - smoothstep(-0.02, 0.16, sd)) * 0.55 * saturate(1.0 - sd / 0.17);

                // the panel: smoked glass with a hairline edge and a lit top rim
                half3 col = half3(0.055, 0.063, 0.188);
                float a = 0.55;
                float edge = 1.0 - smoothstep(0.0, aa * 1.5, abs(d + aa));
                col = lerp(col, half3(1, 1, 1), edge * 0.16);
                a = max(a, edge * 0.3);
                col += smoothstep(0.42, 0.5, p.y) * (1.0 - saturate(-d / 0.012)) * 0.08;

                // the cell under each star
                float2 g = (b - gap) / (cell + gap);
                float2 k = floor(g);
                float2 f = (g - k) * (cell + gap) - cell * 0.5;
                float inGrid = step(0.0, k.x) * step(k.x, n - 1.0) * step(0.0, k.y) * step(k.y, n - 1.0);
                float cd = roundRect(f, float2(cell * 0.5, cell * 0.5), cell * 0.16);
                float cellMask = (1.0 - smoothstep(-aa, aa, cd)) * inGrid;
                // inset shadow along the cell's top edge
                float inset = (1.0 - saturate(-cd / (cell * 0.12))) * smoothstep(-cell * 0.5, cell * 0.5, f.y);
                half3 cellCol = col + 0.035 - inset * 0.06;
                col = lerp(col, cellCol, cellMask);

                half4 o;
            #if !UNITY_COLORSPACE_GAMMA
                col = SRGBToLinear(max(col, 0));
            #endif
                o.rgb = col * a * inside;
                o.a = a * inside;
                // shadow only outside the panel
                o.a += shadow * (1.0 - inside) * 0.8;
                return o * alpha;
            }
            ENDHLSL
        }
    }
}
