using SmashFest.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SmashFest.Core.Effects
{
    public class BreakParticles : MonoBehaviour
    {
        [SerializeField] private ParticleSystem chunks;
        [SerializeField] private ParticleSystem puffs;
        [SerializeField] private ParticleSystem droplets;
        [SerializeField] private Material chunkMaterial;
        [SerializeField] private Material puffMaterial;
        [SerializeField] private Material dropletMaterial;
        [SerializeField] private Mesh chunkMesh;
        [SerializeField, Min(0f)] private float chunkScale = 1f;

        private void Awake()
        {
            ConfigureChunks();
            ConfigurePuffs();
            ConfigureDroplets();
        }

        public void PlayBreak(BlockMaterialType material, Bounds bounds, Color color, Vector3 push)
        {
            switch (material)
            {
                case BlockMaterialType.CeramicJar:
                    EmitSplash(bounds, color, push);
                    EmitShards(bounds, Color.Lerp(color, Color.white, 0.75f), push, 6);
                    EmitPuffs(bounds, Color.Lerp(color, Color.white, 0.4f), 4);
                    break;
                case BlockMaterialType.Glass:
                    EmitShards(bounds, Color.Lerp(color, Color.white, 0.35f), push, 18);
                    EmitPuffs(bounds, Color.Lerp(color, Color.white, 0.6f), 6);
                    break;
                default:
                    EmitChunks(bounds, color, push);
                    EmitPuffs(bounds, Color.Lerp(color, Color.white, 0.35f), 8);
                    break;
            }
        }

        private void EmitChunks(Bounds bounds, Color color, Vector3 push)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(bounds.size.magnitude * 30f), 10, 26);
            float size = Mathf.Clamp(bounds.extents.magnitude * 0.22f, 0.025f, 0.09f) * chunkScale;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = RandomPoint(bounds);
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = Burst(position, bounds.center, push, 1.6f, 3.4f),
                    startSize3D = new Vector3(Random.Range(0.6f, 1.3f), Random.Range(0.6f, 1.3f), Random.Range(0.6f, 1.3f)) * size,
                    rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                    startColor = Vary(color),
                    startLifetime = Random.Range(1.1f, 1.8f)
                };
                chunks.Emit(emit, 1);
            }
        }

        private void EmitShards(Bounds bounds, Color color, Vector3 push, int count)
        {
            float size = Mathf.Clamp(bounds.extents.magnitude * 0.3f, 0.03f, 0.1f) * chunkScale;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = RandomPoint(bounds);
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = Burst(position, bounds.center, push, 1.8f, 3.8f),
                    startSize3D = new Vector3(Random.Range(0.6f, 1.2f) * size, Random.Range(0.6f, 1.2f) * size, size * 0.18f),
                    rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                    startColor = Vary(color),
                    startLifetime = Random.Range(0.9f, 1.4f)
                };
                chunks.Emit(emit, 1);
            }
        }

        private void EmitSplash(Bounds bounds, Color color, Vector3 push)
        {
            Vector3 origin = bounds.center + Vector3.up * bounds.extents.y * 0.3f;
            for (int i = 0; i < 26; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 1.6f;
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = origin + Random.insideUnitSphere * bounds.extents.x * 0.5f,
                    velocity = new Vector3(spread.x, Random.Range(2.2f, 4.6f), spread.y) + push * Random.Range(1f, 2f),
                    startSize = Random.Range(0.045f, 0.1f),
                    startColor = Vary(color),
                    startLifetime = Random.Range(0.8f, 1.3f)
                };
                droplets.Emit(emit, 1);
            }
        }

        private void EmitPuffs(Bounds bounds, Color color, int count)
        {
            color.a = 0.6f;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = RandomPoint(bounds),
                    velocity = Random.insideUnitSphere * 0.5f + Vector3.up * 0.3f,
                    startSize = Random.Range(0.18f, 0.4f) * Mathf.Max(0.6f, bounds.extents.magnitude * 2f),
                    startColor = color,
                    startLifetime = Random.Range(0.7f, 1.1f)
                };
                puffs.Emit(emit, 1);
            }
        }

        private Vector3 Burst(Vector3 position, Vector3 center, Vector3 push, float minSpeed, float maxSpeed)
        {
            Vector3 outward = position - center;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
            return outward * Random.Range(minSpeed, maxSpeed)
                + Vector3.up * Random.Range(1f, 2.6f)
                + push * Random.Range(1f, 2.2f);
        }

        private Vector3 RandomPoint(Bounds bounds)
        {
            Vector3 extents = bounds.extents * 0.8f;
            return bounds.center + new Vector3(
                Random.Range(-extents.x, extents.x),
                Random.Range(-extents.y, extents.y),
                Random.Range(-extents.z, extents.z));
        }

        private Color32 Vary(Color color)
        {
            float shade = Random.Range(0.85f, 1.12f);
            return new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
        }

        private void ConfigureChunks()
        {
            ParticleSystem system = chunks;
            ConfigureBase(system, chunkMaterial, 1.7f, 600);
            ParticleSystem.MainModule main = system.main;
            main.startSize3D = true;
            main.startRotation3D = true;

            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rotation.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            ShrinkAtEnd(system, 0.75f);
            AddCollision(system, 0.25f, 0.35f, 0f);

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = chunkMesh;
            particleRenderer.alignment = ParticleSystemRenderSpace.World;
        }

        private void ConfigurePuffs()
        {
            ParticleSystem system = puffs;
            ConfigureBase(system, puffMaterial, -0.05f, 300);
            system.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f));

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
        }

        private void ConfigureDroplets()
        {
            ParticleSystem system = droplets;
            ConfigureBase(system, dropletMaterial, 2.2f, 400);
            ShrinkAtEnd(system, 0.8f);
            AddCollision(system, 0f, 0.1f, 1f);

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.velocityScale = 0.04f;
            particleRenderer.lengthScale = 1.3f;
        }

        private void ConfigureBase(ParticleSystem system, Material material, float gravity, int maxParticles)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = false;

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            system.Play();
        }

        private void ShrinkAtEnd(ParticleSystem system, float holdUntil)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(holdUntil, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private void AddCollision(ParticleSystem system, float bounce, float dampen, float lifetimeLoss)
        {
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.bounce = bounce;
            collision.dampen = dampen;
            collision.lifetimeLoss = lifetimeLoss;
            collision.enableDynamicColliders = false;
        }
    }
}
