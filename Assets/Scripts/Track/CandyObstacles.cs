using System.Collections.Generic;
using CandyRush.Config;
using CandyRush.Rendering;
using CandyRush.Vehicle;
using UnityEngine;

namespace CandyRush.Track
{
    /// <summary>
    /// Pêndulos de pirulito: três discos gigantes balançando de um lado ao
    /// outro da pista, pendurados num pórtico de bengala (convertido de
    /// src/track/candyObstacles.ts). Bater num deles empurra o kart para longe,
    /// mais forte se o disco vinha na direção dele.
    /// </summary>
    public sealed class CandyObstacles
    {
        private sealed class Hazard
        {
            public Transform Bob;
            public Transform Rope;
            public Vector3 Base;
            /// <summary>Direção em que o disco balança: o X local do pórtico, no mundo.</summary>
            public Vector3 Swing;
            public Vector3 Left;
            public float PivotY;
            public float RopeLength;
            public float MaxAngle;
            public float Phase;
            public float Speed;
            public float Radius;
            public Vector3 Center;
            public Vector3 Velocity;
            public float PreviousX;
        }

        private readonly List<Hazard> _hazards = new List<Hazard>();
        private float _time;

        public CandyObstacles(CircuitPath path, Transform parent)
        {
            Transform root = SceneryBuilder.Group(parent, "Pêndulos de pirulito", Vector3.zero, Quaternion.identity);
            (float at, float phase, float maxAngle)[] specs =
            {
                (0.285f, 0f, 0.58f),
                (0.305f, Mathf.PI * 0.72f, 0.66f),
                (0.325f, Mathf.PI * 1.38f, 0.6f),
            };
            for (int i = 0; i < specs.Length; i++)
                _hazards.Add(CreatePendulum(path, root, path.IndexAtFraction(specs[i].at), specs[i].phase, specs[i].maxAngle, i));
            Update(0f);
        }

        public void Update(float dt)
        {
            _time += dt;
            foreach (Hazard hazard in _hazards) UpdateHazard(hazard, dt);
        }

        public void Collide(Kart kart)
        {
            Physics.KartRigidBody body = kart.Body;
            Vector3 kartPosition = body.ChassisPosition;

            foreach (Hazard hazard in _hazards)
            {
                Vector3 delta = kartPosition - hazard.Center;
                delta.y *= 0.35f;
                float contactRadius = hazard.Radius + TrackConfig.Barriers.KartRadius;
                float distance = delta.magnitude;
                if (distance >= contactRadius) continue;

                Vector3 normal = distance > 1e-4f ? delta / distance : hazard.Left;
                normal.y = Mathf.Clamp(normal.y, -0.25f, 0.25f);
                normal.Normalize();

                float penetration = contactRadius - distance;
                body.Position += normal * (penetration * 0.9f);

                float obstacleSpeed = Vector3.Dot(hazard.Velocity, normal);
                float kartSpeed = Vector3.Dot(body.Velocity, normal);
                float closingSpeed = Mathf.Max(0f, obstacleSpeed - kartSpeed);
                float push = 4.5f + closingSpeed * 1.1f;

                body.Velocity = body.Velocity * 0.82f + normal * push;
                Vector3 angular = body.AngularVelocity;
                angular.y += (normal.x * body.Velocity.z - normal.z * body.Velocity.x) * 0.035f;
                body.AngularVelocity = angular;
            }
        }

        private void UpdateHazard(Hazard hazard, float dt)
        {
            float angle = Mathf.Sin(_time * hazard.Speed + hazard.Phase) * hazard.MaxAngle;
            float localX = Mathf.Sin(angle) * hazard.RopeLength;
            float localY = hazard.PivotY - Mathf.Cos(angle) * hazard.RopeLength;

            var pivot = new Vector3(0f, hazard.PivotY, 0f);
            var bobCenter = new Vector3(localX, localY, 0f);
            hazard.Bob.localPosition = bobCenter;
            hazard.Bob.localRotation = Quaternion.AngleAxis(-angle * 0.45f * Mathf.Rad2Deg, Vector3.forward);

            // A corda vai do pivô até o centro do disco.
            Vector3 rope = bobCenter - pivot;
            hazard.Rope.localPosition = (pivot + bobCenter) * 0.5f;
            hazard.Rope.localRotation = Quaternion.FromToRotation(Vector3.up, rope.normalized);
            hazard.Rope.localScale = new Vector3(1f, rope.magnitude, 1f);

            // Centro no mundo para a colisão: o MESMO eixo em que o disco balança na tela.
            hazard.Center = hazard.Base + hazard.Swing * localX + Vector3.up * localY;
            hazard.Velocity = dt > 0f ? hazard.Swing * ((localX - hazard.PreviousX) / dt) : Vector3.zero;
            hazard.PreviousX = localX;
        }

        private static Hazard CreatePendulum(CircuitPath path, Transform root, int index, float phase, float maxAngle, int variant)
        {
            PathSample sample = path.Samples[index];
            float halfSpan = sample.HalfWidth + 3.0f;
            Vector3 basePosition = sample.Position;
            basePosition.y = path.SurfaceHeight(index, 0f);
            Quaternion facing = TrackPlacement.Facing(path, index);
            Transform group = SceneryBuilder.Group(root, "Pêndulo", basePosition, facing);

            Color pinkColor = ProceduralTextures.Hex(0xff4f9a);
            Color softPinkColor = ProceduralTextures.Hex(0xff9fc4);
            Color yellowColor = ProceduralTextures.Hex(0xffc24d);
            Material pink = CandyMaterials.Glow(pinkColor, 0.06f);
            Material softPink = CandyMaterials.Glow(softPinkColor, 0.03f);
            Material yellow = CandyMaterials.Glow(yellowColor, 0.04f);
            Texture2D stripes = ProceduralTextures.Stripe(ProceduralTextures.Hex(0xff5c96), ProceduralTextures.Hex(0xfff2f7));
            Material post = CandyMaterials.Textured(stripes, 0.16f, new Vector2(1.35f, 3.8f));
            Material cane = CandyMaterials.Textured(stripes, 0.16f, new Vector2(1.0f, 4.6f));
            Material swirl = CandyMaterials.Textured(ProceduralTextures.Swirl(ProceduralTextures.Hex(0xff3f92), ProceduralTextures.Hex(0xfff0f7)), 0.12f);
            CandyMaterials.SetGlow(swirl, ProceduralTextures.Hex(0xffd6e8), 0.1f);

            // --- Pórtico: dois postes listrados, pés amarelos, travessa rosa ---
            foreach (float x in new[] { -halfSpan, halfSpan })
            {
                SceneryBuilder.Part(group, "Poste", ProceduralMeshes.Cylinder(0.42f, 0.42f, 7.0f, 18), post, new Vector3(x, 3.5f, 0f));
                SceneryBuilder.Part(group, "Pé", ProceduralMeshes.RoundedBox(new Vector3(1.7f, 0.74f, 1.7f), 0.28f), yellow, new Vector3(x, 0.35f, 0f));
                SceneryBuilder.Part(group, "Topo", ProceduralMeshes.Cylinder(0.6f, 0.6f, 0.72f, 18), softPink, new Vector3(x, 6.98f, 0f));
            }
            SceneryBuilder.Part(group, "Travessa", ProceduralMeshes.RoundedBox(new Vector3(halfSpan * 2f + 1.4f, 0.58f, 0.82f), 0.3f), pink, new Vector3(0f, 6.95f, 0f));
            foreach (float x in new[] { -halfSpan + 0.35f, halfSpan - 0.35f, 0f })
            {
                SceneryBuilder.Part(group, "Anel", ProceduralMeshes.Cylinder(0.54f, 0.54f, 0.8f, 18), yellow, new Vector3(x, 6.95f, 0f),
                    SceneryBuilder.Euler(0f, 0f, Mathf.PI / 2f), Vector3.one);
            }

            Transform rope = SceneryBuilder.Part(group, "Corda", ProceduralMeshes.Cylinder(0.16f, 0.16f, 1f, 14), cane, Vector3.zero);

            // --- O disco de pirulito ---
            Transform bob = SceneryBuilder.Group(group, "Pirulito", Vector3.zero, Quaternion.identity);
            float discRadius = 1.8f + (variant % 2) * 0.12f;
            const float discThickness = 0.56f;
            Quaternion faceZ = SceneryBuilder.Euler(Mathf.PI / 2f, 0f, 0f);

            SceneryBuilder.Part(bob, "Disco", ProceduralMeshes.Cylinder(discRadius, discRadius, discThickness, 40), softPink, Vector3.zero, faceZ, Vector3.one);
            Mesh rim = ProceduralMeshes.Torus(discRadius + 0.02f, 0.13f, 8, 40);
            SceneryBuilder.Part(bob, "Aro", rim, pink, new Vector3(0f, 0f, discThickness * 0.51f));
            SceneryBuilder.Part(bob, "Aro", rim, pink, new Vector3(0f, 0f, -discThickness * 0.51f));
            SceneryBuilder.Part(bob, "Faixa", ProceduralMeshes.Cylinder(discRadius + 0.03f, discRadius + 0.03f, 0.22f, 40), softPink, Vector3.zero, faceZ, Vector3.one);
            Mesh face = ProceduralMeshes.Cylinder(discRadius * 0.92f, discRadius * 0.92f, 0.06f, 40);
            SceneryBuilder.Part(bob, "Espiral", face, swirl, new Vector3(0f, 0f, discThickness * 0.56f), faceZ, Vector3.one);
            SceneryBuilder.Part(bob, "Espiral", face, swirl, new Vector3(0f, 0f, -discThickness * 0.56f), faceZ, Vector3.one);

            Mesh sprinkle = ProceduralMeshes.RoundedBox(new Vector3(0.34f, 0.1f, 0.09f), 0.04f, 2);
            for (int i = 0; i < 18; i++)
            {
                Color sprinkleColor = FeaturesConfig.Pastels[(i + variant) % FeaturesConfig.Pastels.Length];
                float angle = i / 18f * Mathf.PI * 2f + variant * 0.22f;
                float ring = i % 3 == 0 ? discRadius * 0.48f : discRadius * 0.78f;
                SceneryBuilder.Part(bob, "Granulado", sprinkle, CandyMaterials.Glow(sprinkleColor, 0.08f),
                    new Vector3(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring, discThickness * 0.68f),
                    SceneryBuilder.Euler(0f, 0f, angle + Mathf.PI / 2f), Vector3.one, false);
            }

            return new Hazard
            {
                Bob = bob,
                Rope = rope,
                Base = basePosition,
                Swing = facing * Vector3.right,
                Left = sample.Left,
                PivotY = 6.88f,
                RopeLength = 4.8f,
                MaxAngle = maxAngle,
                Phase = phase,
                Speed = 1.35f + variant * 0.16f,
                Radius = discRadius,
            };
        }
    }
}
