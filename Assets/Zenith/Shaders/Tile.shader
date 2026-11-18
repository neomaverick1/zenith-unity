// One star tile, drawn entirely on the graphics chip: a rounded face with a top-to-bottom gradient, a glassy sheen,
// an inner rim of light, a soft glow in the star's color, a sweep of light across hot stars, and the corona,
// pulse and accretion disk of the objects beyond the goal. The quad is bigger than the tile to leave room for glow.
Shader "Zenith/Tile"
{
    Properties
    {
        _From ("Top", Color) = (1, 1, 1, 1)
        _To ("Bottom", Color) = (1, 1, 1, 1)
        _Glow ("Glow", Color) = (0, 0, 0, 0)
        _Shape ("Radius, pad, sheen delay, aura", Vector) = (0.16, 0.4, 0, 0)
        _Look ("Alpha, brightness, shine, mask", Vector) = (1, 1, 0, 0)
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

            // The colors were designed in sRGB, like a web page; the math runs in sRGB and converts once at the end.
            half3 toSrgb(half3 c)
            {
            #if UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return LinearToSRGB(c);
            #endif
            }
            half3 toOut(half3 c)
            {
            #if UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return SRGBToLinear(max(c, 0));
            #endif
            }

            CBUFFER_START(UnityPerMaterial)
                half4 _From;
                half4 _To;
                half4 _Glow;
                float4 _Shape;
                float4 _Look;
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

            // a conic rainbow for the supernova's corona, turning slowly
            half3 corona(float a)
            {
                half3 c0 = half3(1.0, 0.416, 0.835);  // #ff6ad5
                half3 c1 = half3(1.0, 0.827, 0.420);  // #ffd36b
                half3 c2 = half3(0.486, 0.949, 1.0);  // #7cf2ff
                half3 c3 = half3(0.663, 0.545, 1.0);  // #a98bff
                float k = frac(a) * 4.0;
                half3 c = k < 1.0 ? lerp(c0, c1, k) : k < 2.0 ? lerp(c1, c2, k - 1.0) : k < 3.0 ? lerp(c2, c3, k - 2.0) : lerp(c3, c0, k - 3.0);
                return c;
            }

            // the black hole's accretion disk: hot orange arcs with gaps, spinning fast
            half4 disk(float a)
            {
                float k = frac(a);
                half4 c = half4(0, 0, 0, 0);
                if (k < 0.4) {
                    float s = k / 0.4; // transparent -> #ffb15c -> #ff6a3d -> transparent
                    half3 col = s < 0.5 ? half3(1.0, 0.694, 0.361) : half3(1.0, 0.416, 0.239);
                    c = half4(col, sin(s * 3.14159));
                } else if (k < 0.7) {
                    float s = (k - 0.4) / 0.3;
                    c = half4(1.0, 0.824, 0.541, sin(s * 3.14159));
                }
                return c;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float radius = _Shape.x;
                float pad = _Shape.y;
                float delay = _Shape.z;
                float aura = _Shape.w;
                float alpha = _Look.x;
                float bright = _Look.y;
                float shine = _Look.z;
                float2 p = (i.uv - 0.5) * (1.0 + 2.0 * pad); // the face spans -0.5..0.5
                float d = roundRect(p, float2(0.5, 0.5), radius);
                float aa = max(fwidth(d), 1e-4);
                float face = 1.0 - smoothstep(-aa, aa, d);
                float t = _Time.y;

                // behind the face: glow, corona or accretion disk
                half4 back = half4(0, 0, 0, 0);
                if (_Glow.a > 0.001) {
                    // a soft box-shadow glow that fades out completely before the edge of the quad
                    float x = max(d, 0.0) / 0.17;
                    float g = exp(-x * x) * _Glow.a * 0.8 * saturate(1.0 - d / (pad * 0.92));
                    back.rgb += toSrgb(_Glow.rgb) * g;
                    back.a += g;
                }
                float r = length(p);
                float ang = atan2(p.y, p.x) / 6.28318;
                if (aura > 0.5 && aura < 1.5) {
                    float ring = smoothstep(0.354, 0.408, r) * (1.0 - smoothstep(0.408, 0.68, r)) * 0.85;
                    half3 c = corona(-ang + t / 5.0);
                    back.rgb = back.rgb * (1.0 - ring) + c * ring * 1.6;
                    back.a = back.a * (1.0 - ring) + ring;
                } else if (aura > 2.5) {
                    float ring = smoothstep(0.348, 0.396, r) * (1.0 - smoothstep(0.396, 0.552, r));
                    half4 c = disk(-ang + 0.25 + t / 2.6);
                    float a = ring * c.a;
                    back.rgb = back.rgb * (1.0 - a) + c.rgb * a * 1.8;
                    back.a = back.a * (1.0 - a) + a;
                }

                // the face itself
                float y = saturate(p.y + 0.5);
                half3 col = lerp(toSrgb(_To.rgb), toSrgb(_From.rgb), y);
                // a sheen across the top, like light on glass (160 degrees, white 18% fading out by 42%)
                float2 dir = float2(0.342, -0.940);
                float along = dot(p, dir) / 1.282 + 0.5;
                col += (1.0 - smoothstep(0.0, 0.42, along)) * 0.18;
                // a rim of light along the top edge and a little shade along the bottom
                float inner = saturate(-d / 0.035);
                col += (1.0 - inner) * smoothstep(0.1, 0.5, p.y) * 0.22;
                col *= 1.0 - (1.0 - saturate(-d / 0.06)) * smoothstep(0.1, -0.5, p.y) * 0.16;
                // a sweep of light across hot stars every few seconds
                if (shine > 0.5) {
                    float cyc = frac((t + delay) / 4.2);
                    if (cyc > 0.7) {
                        float k = (cyc - 0.7) / 0.3;
                        k = k * k * (3.0 - 2.0 * k);
                        float x = p.x + p.y * 0.2126; // skewed 12 degrees
                        float c = lerp(-0.95, 1.45, k);
                        float band = saturate(1.0 - abs(x - c) / 0.175);
                        col += band * band * 0.45;
                    }
                }
                // the neutron star breathes
                if (aura > 1.5 && aura < 2.5) col *= 1.0 + 0.25 * (0.5 - 0.5 * cos(t * 6.28318 / 1.6));
                col *= bright;

                half4 o;
                half3 backCol = back.a > 1e-4 ? back.rgb / back.a : half3(0, 0, 0);
                o.rgb = toOut(col) * face + toOut(backCol) * back.a * (1.0 - face);
                o.a = face + back.a * (1.0 - face);
                return o * alpha; // premultiplied
            }
            ENDHLSL
        }
    }
}
