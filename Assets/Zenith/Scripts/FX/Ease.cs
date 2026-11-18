// The motion curves, the same cubic-bezier curves the web version's animations used.
using UnityEngine;

namespace Zenith
{
    public static class Ease
    {
        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) at progress t (0 to 1).</summary>
        public static float Bezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            // find the curve parameter whose x is t (Newton steps, then bisection if needed)
            float u = t;
            for (int i = 0; i < 6; i++)
            {
                float x = Sample(x1, x2, u) - t;
                float dx = Slope(x1, x2, u);
                if (Mathf.Abs(x) < 1e-5f) break;
                if (Mathf.Abs(dx) < 1e-6f) break;
                u -= x / dx;
            }
            u = Mathf.Clamp01(u);
            if (Mathf.Abs(Sample(x1, x2, u) - t) > 1e-3f)
            {
                float lo = 0, hi = 1;
                for (int i = 0; i < 20; i++)
                {
                    u = (lo + hi) * 0.5f;
                    if (Sample(x1, x2, u) < t) lo = u;
                    else hi = u;
                }
            }
            return Sample(y1, y2, u);
        }

        static float Sample(float a, float b, float u) => ((1 - 3 * b + 3 * a) * u + (3 * b - 6 * a)) * u * u + 3 * a * u;

        static float Slope(float a, float b, float u) => 3 * (1 - 3 * b + 3 * a) * u * u + 2 * (3 * b - 6 * a) * u + 3 * a;

        /// <summary>A quick start and a long, gentle landing.</summary>
        public static float Out(float t) => Bezier(0.2f, 0.8f, 0.25f, 1f, t);

        /// <summary>Overshoots a little and settles, like a spring.</summary>
        public static float Spring(float t) => Bezier(0.34f, 1.56f, 0.64f, 1f, t);

        public static float InOut(float t) => Bezier(0.42f, 0f, 0.58f, 1f, t);

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        /// <summary>Steps through keyframes (time 0..1, value) with linear blending between them.</summary>
        public static float Keys(float t, params Vector2[] keys)
        {
            if (t <= keys[0].x) return keys[0].y;
            for (int i = 1; i < keys.Length; i++)
                if (t <= keys[i].x)
                {
                    float k = (t - keys[i - 1].x) / Mathf.Max(1e-5f, keys[i].x - keys[i - 1].x);
                    return Mathf.Lerp(keys[i - 1].y, keys[i].y, k);
                }
            return keys[keys.Length - 1].y;
        }
    }
}
