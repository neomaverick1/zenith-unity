// The camera, the sky behind everything and the screen-wide effects: bloom so bright stars glow, and a lens punch
// (a ripple of chromatic aberration and distortion) when something huge happens. The board lives on the plane
// z = 0 and the interface tells the stage where on screen each part should go.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zenith
{
    public class Stage : MonoBehaviour
    {
        public static Stage I { get; private set; }

        public Camera Cam { get; private set; }
        const float Distance = 10f;
        const float Fov = 30f;
        Transform backdrop;
        Material backdropMat;
        Volume volume;
        Bloom bloom;
        ChromaticAberration aberration;
        LensDistortion lens;
        float punch, punchStart = -10, punchDur = 0.6f;
        float bloomIntensity = 0.75f;
        public Nebula Sky { get; private set; }
        public Sparks Sparks { get; private set; }
        static Mesh quad;

        public static void Create()
        {
            if (I) return;
            var go = new GameObject("Stage");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Stage>();
        }

        /// <summary>A unit quad centered on its origin, shared by every tile and panel.</summary>
        public static Mesh Quad
        {
            get
            {
                if (quad) return quad;
                quad = new Mesh { name = "Quad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quad.RecalculateBounds();
                quad.bounds = new Bounds(Vector3.zero, new Vector3(1, 1, 0.1f));
                return quad;
            }
        }

        void Awake()
        {
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            camGo.tag = "MainCamera";
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Stars.Hex("#07081c");
            Cam.fieldOfView = Fov;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 200f;
            Cam.transform.position = new Vector3(0, 0, -Distance);
            Cam.allowHDR = true;
            Cam.allowMSAA = false;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<AudioListener>();

            // the sky: a big quad far behind the board showing the nebula
            var sky = new GameObject("Sky");
            sky.transform.SetParent(transform, false);
            backdrop = sky.transform;
            backdrop.position = new Vector3(0, 0, 60);
            sky.AddComponent<MeshFilter>().sharedMesh = Quad;
            var mr = sky.AddComponent<MeshRenderer>();
            backdropMat = new Material(Resources.Load<Material>("Materials/Backdrop"));
            mr.sharedMaterial = backdropMat;
            mr.sortingOrder = -1000;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Sky = sky.AddComponent<Nebula>();

            var sparks = new GameObject("Sparks");
            sparks.transform.SetParent(transform, false);
            Sparks = sparks.AddComponent<Sparks>();

            // screen effects
            var volGo = new GameObject("Effects");
            volGo.transform.SetParent(transform, false);
            volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(0.68f);
            bloom.highQualityFiltering.Override(false);
            bloom.tint.Override(new Color(0.92f, 0.95f, 1f));
            aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0);
            lens = profile.Add<LensDistortion>(true);
            lens.intensity.Override(0);
            lens.scale.Override(1);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.None);
            volume.sharedProfile = profile;
        }

        public void SetQuality(Quality q, bool reduceMotion)
        {
            bloom.active = q != Quality.Saver;
            bloomIntensity = q == Quality.High ? 0.75f : 0.55f;
            bloom.intensity.Override(bloomIntensity);
            bloom.highQualityFiltering.Override(false);
            Sky.SetQuality(q, reduceMotion);
            Sparks.density = reduceMotion ? 0.25f : q == Quality.High ? 1f : q == Quality.Balanced ? 0.65f : 0.35f;
        }

        /// <summary>A lens punch: the picture bends and splits into color for a moment.</summary>
        public void Punch(float amount, float dur = 0.6f)
        {
            if (Store.settings.reduceMotion) return;
            punch = amount;
            punchStart = Time.unscaledTime;
            punchDur = dur;
        }

        /// <summary>A brief swell of bloom, as if the whole picture caught the light.</summary>
        public void Flare(float amount)
        {
            flare = Mathf.Max(flare, amount);
        }

        float flare;

        void LateUpdate()
        {
            // keep the sky filling the view
            float h = 2 * (backdrop.position.z + Distance) * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * 1.04f;
            backdrop.localScale = new Vector3(h * Cam.aspect, h, 1);
            backdropMat.mainTexture = Sky.Texture;

            float k = Mathf.Clamp01((Time.unscaledTime - punchStart) / punchDur);
            float env = k >= 1 ? 0 : Mathf.Sin(k * Mathf.PI) * (1 - k);
            aberration.intensity.Override(Mathf.Clamp01(env * punch * 2.2f));
            lens.intensity.Override(-env * punch * 0.35f);
            flare *= Mathf.Exp(-Time.unscaledDeltaTime / 0.35f);
            bloom.intensity.Override(bloomIntensity + flare * 2.5f);
        }

        /// <summary>World units per screen pixel on the board's plane.</summary>
        public float WorldPerPixel => 2 * Distance * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);

        /// <summary>The point on the board's plane under a screen pixel (origin bottom left).</summary>
        public Vector3 ScreenToWorld(Vector2 px)
        {
            var ray = Cam.ScreenPointToRay(new Vector3(px.x, px.y, 0));
            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        public Vector2 WorldToScreen(Vector3 world)
        {
            var p = Cam.WorldToScreenPoint(world);
            return new Vector2(p.x, p.y);
        }
    }
}
