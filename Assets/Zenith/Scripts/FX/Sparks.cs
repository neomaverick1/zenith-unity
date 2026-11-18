// Sparks: glowing particles that burst out of a merge in the star's own color, added to the light so overlapping
// sparks glow brighter. A third of them streak along their path for a sense of speed.
using UnityEngine;

namespace Zenith
{
    public class Sparks : MonoBehaviour
    {
        ParticleSystem round, streak;
        public float density = 1;

        void Awake()
        {
            var mat = Resources.Load<Material>("Materials/Spark");
            round = Make("Round", mat, false);
            streak = Make("Streak", mat, true);
        }

        ParticleSystem Make(string name, Material mat, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0;
            main.gravityModifier = 0;
            main.useUnscaledTime = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            // speed bleeds away quickly, like sparks in thick air
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000;
            limit.dampen = 0;
            limit.drag = 3.2f;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.4f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.375f), new GradientAlphaKey(0, 1) });
            color.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = 200;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch)
            {
                r.lengthScale = 2f;
                r.velocityScale = 0.15f;
            }
            r.minParticleSize = 0;
            r.maxParticleSize = 1;
            ps.Play();
            return ps;
        }

        /// <summary>A burst at a point on the board's plane, sized to the merge. `px` converts layout points to world units.</summary>
        public void Burst(Vector3 at, Color c, float power, float px)
        {
            if (density <= 0) return;
            int count = Mathf.RoundToInt((10 + power * 26) * density);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2;
                float v = (60 + Random.value * 260) * (0.6f + power * 0.7f) * px;
                float life = 0.35f + Random.value * 0.55f + power * 0.25f;
                float s = (6 + Random.value * 14 * (0.6f + power * 0.5f)) * px;
                bool isStreak = Random.value < 0.35f;
                p.position = at + new Vector3(0, 0, -0.05f);
                p.velocity = new Vector3(Mathf.Cos(a) * v, Mathf.Sin(a) * v, 0);
                p.startLifetime = life;
                p.startSize = isStreak ? s * 0.5f : s;
                p.startColor = Random.value < 0.18f ? Color.white : c;
                (isStreak ? streak : round).Emit(p, 1);
            }
        }
    }
}
