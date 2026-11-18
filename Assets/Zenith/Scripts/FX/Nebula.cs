// Draws the nebula into a small texture a few dozen times a second. It warms with the game, ripples where stars
// merge and floods with light at a supernova.
using UnityEngine;

namespace Zenith
{
    public class Nebula : MonoBehaviour
    {
        Material mat;
        RenderTexture rt;
        float scale = 0.6f;
        float fps = 60;
        int octaves = 5;
        float last = -1;
        float t0;
        float heat, heatTarget, flash;
        readonly Vector4[] rip = new Vector4[8];
        readonly Vector4[] tint = new Vector4[8];
        int nextRip;

        public Texture Texture => rt ? rt : Texture2D.blackTexture;

        void Awake()
        {
            mat = new Material(Resources.Load<Material>("Materials/Nebula"));
            t0 = Time.unscaledTime;
        }

        public void SetQuality(Quality q, bool reduceMotion)
        {
            scale = q == Quality.High ? 0.6f : q == Quality.Balanced ? 0.42f : 0.3f;
            fps = q == Quality.High ? 60 : q == Quality.Balanced ? 40 : 12;
            if (reduceMotion) fps = Mathf.Min(fps, 20);
            octaves = q == Quality.High ? 5 : q == Quality.Balanced ? 4 : 3;
        }

        public void SetHeat(float h) => heatTarget = h;

        float Now => Time.unscaledTime - t0;

        /// <summary>A shockwave from a point on screen (0 to 1 across and up), as strong as `s`, tinted with a star's color.</summary>
        public void Ripple(Vector2 uv, float s, Color c)
        {
            rip[nextRip] = new Vector4(uv.x, uv.y, Now, s);
            tint[nextRip] = new Vector4(c.r, c.g, c.b, 1);
            nextRip = (nextRip + 1) % rip.Length;
        }

        public void Supernova() => flash = 1;

        void Update()
        {
            int w = Mathf.Max(8, Mathf.RoundToInt(Screen.width * scale));
            int h = Mathf.Max(8, Mathf.RoundToInt(Screen.height * scale));
            if (!rt || rt.width != w || rt.height != h)
            {
                if (rt) rt.Release();
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
                {
                    name = "Nebula",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                rt.Create();
                last = -1;
            }
            float now = Now;
            if (last >= 0 && now - last < 1f / fps - 0.002f) return;
            float dt = last < 0 ? 0 : now - last;
            last = now;
            heat += (heatTarget - heat) * (1 - Mathf.Pow(0.98f, dt * 60));
            flash *= Mathf.Pow(0.93f, dt * 60);
            mat.SetVector("_Res", new Vector4(w, h, 0, 0));
            mat.SetFloat("_T", now);
            mat.SetFloat("_Heat", heat);
            mat.SetFloat("_Flash", flash > 0.01f ? flash : 0);
            mat.SetFloat("_Octaves", octaves);
            for (int i = 0; i < rip.Length; i++)
                if (rip[i].w > 0 && now - rip[i].z > 3)
                    rip[i].w = 0;
            mat.SetVectorArray("_Rip", rip);
            mat.SetVectorArray("_Tint", tint);
            Graphics.Blit(null, rt, mat);
        }

        void OnDestroy()
        {
            if (rt) rt.Release();
        }
    }
}
