// The living background: a slowly drifting nebula and a field of twinkling stars. It warms from deep blue toward
// violet and gold as your biggest star grows, every merge sends a shockwave through space from where it
// happened, and a supernova floods it with light. Rendered into a small texture and stretched over the screen,
// which keeps it smooth on phones without looking any different.
Shader "Zenith/Nebula"
{
    Properties
    {
        _MainTex ("Unused", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            float2 _Res;
            float _T;
            float _Heat;
            float _Flash;
            float _Octaves;
            float4 _Rip[8];
            float4 _Tint[8];

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                float2x2 m = float2x2(1.6, -1.2, 1.2, 1.6);
                for (int i = 0; i < 5; i++)
                {
                    if (i >= _Octaves) break;
                    v += a * noise(p);
                    p = mul(m, p);
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float aspect = _Res.x / _Res.y;
                float2 p = uv - 0.5;
                p.x *= aspect;
                float2 warp = 0;
                float3 ringLight = 0;
                for (int k = 0; k < 8; k++)
                {
                    float4 r = _Rip[k];
                    float age = _T - r.z;
                    if (r.w <= 0.0 || age < 0.0 || age > 3.0) continue;
                    float2 c = r.xy - 0.5;
                    c.x *= aspect;
                    float2 d = p - c;
                    float dist = length(d);
                    float radius = age * (0.5 + 0.25 * r.w);
                    float x = (dist - radius) * (16.0 - 6.0 * min(r.w, 1.0));
                    float ring = exp(-x * x);
                    float fade = exp(-age * 1.35) * r.w;
                    warp += d / (dist + 0.001) * ring * fade * 0.045;
                    ringLight += _Tint[k].rgb * ring * fade * 0.55;
                }
                p -= warp;
                float t = _T * 0.018;
                float n = fbm(p * 1.8 + float2(t, -t * 0.6));
                float n2 = fbm(p * 3.4 - float2(t * 0.7, t) + n * 1.4);
                // cold palette (deep space) and hot palette (a heating sky), blended by how far the game has come
                float3 c1 = lerp(float3(0.020, 0.024, 0.075), float3(0.055, 0.018, 0.070), _Heat);
                float3 c2 = lerp(float3(0.090, 0.110, 0.300), float3(0.330, 0.090, 0.300), _Heat);
                float3 c3 = lerp(float3(0.150, 0.420, 0.620), float3(0.980, 0.560, 0.250), _Heat);
                float3 col = lerp(c1, c2, smoothstep(0.30, 0.85, n));
                col = lerp(col, c3, smoothstep(0.58, 1.05, n2) * (0.55 + 0.25 * _Heat));
                col *= 0.65 + 0.55 * n2;
                // stars, twinkling at their own pace
                float2 sp = uv * _Res / 2.2;
                float2 cell = floor(sp);
                float h = hash(cell);
                float2 f = frac(sp) - 0.5 + (float2(hash(cell + 3.1), hash(cell + 7.7)) - 0.5) * 0.6;
                float tw = 0.55 + 0.45 * sin(_T * (0.6 + h * 2.4) + h * 60.0);
                float st = step(0.982, h) * tw * smoothstep(0.24, 0.0, length(f));
                col += st * lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.9, 0.75), hash(cell + 1.3));
                col += ringLight;
                // vignette, then the supernova's flash
                float2 v = uv - 0.5;
                col *= 1.0 - 0.75 * dot(v, v);
                col = lerp(col, float3(1.0, 0.97, 0.92), _Flash);
                // designed in sRGB like the web version; stored as linear light
                #ifndef UNITY_COLORSPACE_GAMMA
                col = GammaToLinearSpace(max(col, 0));
                #endif
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
