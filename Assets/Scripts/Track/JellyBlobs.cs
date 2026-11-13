using System.Collections.Generic;
using CandyRush.Rendering;
using CandyRush.Vehicle;
using UnityEngine;
using Jelly = CandyRush.Config.FeaturesConfig.JellyBlobs;

namespace CandyRush.Track
{
    /// <summary>
    /// Geleias com rostinho no trecho final da volta (convertido de src/track/jellyBlobs.ts).
    /// Encostar numa geleia segura o kart e o empurra para fora; ela balança mais forte.
    /// É regra de jogo, então roda no passo fixo junto com a física.
    /// </summary>
    public sealed class JellyBlobs
    {
        private sealed class Blob
        {
            public Transform Group;
            public Vector3 Center;
            public float Radius;
            public float Phase;
            public float Shake;
        }

        private readonly List<Blob> _blobs = new List<Blob>();
        private float _time;

        public JellyBlobs(CircuitPath path, Transform parent)
        {
            Transform root = SceneryBuilder.Group(parent, "Geleias", Vector3.zero, Quaternion.identity);
            for (int i = 0; i < Jelly.Spots.Length; i++) _blobs.Add(CreateBlob(path, root, Jelly.Spots[i], i));
        }

        /// <summary>Balanço contínuo, mais forte logo depois de uma batida.</summary>
        public void Update(float dt)
        {
            _time += dt;
            foreach (Blob blob in _blobs)
            {
                blob.Shake = Mathf.Max(0f, blob.Shake - dt / Jelly.HitDecay);
                float amplitude = Jelly.WobbleAmplitude + Jelly.HitAmplitude * blob.Shake;
                float swing = Mathf.Sin(_time * Jelly.WobbleSpeed + blob.Phase) * amplitude;
                blob.Group.localScale = new Vector3(1f - swing * 0.5f, 1f + swing, 1f - swing * 0.5f);
            }
        }

        public void Collide(Kart kart, float dt)
        {
            if (dt <= 0f) return;
            Physics.KartRigidBody body = kart.Body;
            Vector3 kartPosition = body.ChassisPosition;

            foreach (Blob blob in _blobs)
            {
                Vector3 delta = kartPosition - blob.Center;
                delta.y *= 0.4f;
                float contact = blob.Radius + 0.75f;
                float distance = delta.magnitude;
                if (distance >= contact) continue;

                blob.Shake = 1f;
                body.Velocity *= Mathf.Pow(Jelly.SlowPerSecond, dt);

                Vector3 normal = distance > 1e-4f ? delta / distance : Vector3.right;
                normal.y = 0f;
                body.Velocity += normal.normalized * Jelly.Push;
            }
        }

        private static Blob CreateBlob(CircuitPath path, Transform root, Jelly.Spot spot, int seed)
        {
            int index = path.IndexAtFraction(spot.At);
            PathSample sample = path.Samples[index];
            Color color = Jelly.Colors[spot.Color % Jelly.Colors.Length];
            float r = spot.Radius;

            Vector3 center = sample.Position + sample.Left * spot.Lateral;
            center.y = path.SurfaceHeight(index, spot.Lateral);
            Transform group = SceneryBuilder.Group(root, "Geleia", center, TrackPlacement.Facing(path, index));

            Material body = CandyMaterials.Glow(color, 0.18f);
            body.SetFloat("_Glossiness", 0.9f * 0.8f);
            Material ink = CandyMaterials.Candy(Jelly.Ink, 0.35f);
            Material cheek = CandyMaterials.Glow(Jelly.Cheek, 0.2f);
            Mesh sphere = ProceduralMeshes.Primitive(PrimitiveType.Sphere);

            SceneryBuilder.Part(group, "Cúpula", sphere, body, new Vector3(0f, r * 0.72f, 0f), Quaternion.identity, new Vector3(1f, 0.82f, 1f) * r * 2f);
            SceneryBuilder.Part(group, "Saia", sphere, body, new Vector3(0f, r * 0.26f, 0f), Quaternion.identity, new Vector3(1f, 0.3f, 1f) * r * 2.1f);

            // O rosto olha para -Z local: para quem vem pela pista.
            float faceZ = -r * 0.82f;
            float eye = Jelly.EyeRadius * r;
            foreach (int side in new[] { 1, -1 })
            {
                SceneryBuilder.Part(group, "Olho", sphere, ink, new Vector3(side * Jelly.EyeSpread * r, r * 0.78f, faceZ),
                    Quaternion.identity, new Vector3(eye * 2f, eye * 2f, eye * 2f * 0.45f), false);
                SceneryBuilder.Part(group, "Bochecha", sphere, cheek, new Vector3(side * Jelly.EyeSpread * r * 1.75f, r * 0.6f, faceZ * 0.92f),
                    Quaternion.identity, new Vector3(eye * 1.6f, eye * 1.6f, eye * 1.6f * 0.35f), false);
            }
            SceneryBuilder.Part(group, "Boca", ProceduralMeshes.Torus(Jelly.MouthRadius * r, Jelly.MouthRadius * r * 0.3f, 6, 14, Mathf.PI),
                ink, new Vector3(0f, r * 0.56f, faceZ), SceneryBuilder.Euler(0f, 0f, Mathf.PI), Vector3.one, false);

            return new Blob
            {
                Group = group,
                Center = center + Vector3.up * (r * 0.6f),
                Radius = r,
                Phase = seed * 1.7f,
            };
        }
    }
}
