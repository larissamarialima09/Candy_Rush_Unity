using CandyRush.Rendering;
using CandyRush.Vehicle;
using UnityEngine;
using Ramp = CandyRush.Config.FeaturesConfig.SpeedRamp;

namespace CandyRush.Track
{
    /// <summary>
    /// Rampa de aceleração: fita de bala com setas de neon, logo depois do
    /// Túnel de Donut. Passar por cima entrega turbo (convertido de src/track/speedRamp.ts).
    ///
    /// A fita é COLADA na pista, construída sobre as amostras (acompanha subida
    /// e inclinação). O turbo não é reimplementado: a rampa chama
    /// Drift.TriggerBoost, o mesmo caminho da saída de drift.
    /// </summary>
    public sealed class SpeedRamp
    {
        private readonly CircuitPath _path;
        /// <summary>Distância acumulada, em metros, do centro da rampa na volta.</summary>
        private readonly float _centerDistance;

        public SpeedRamp(CircuitPath path, Transform parent)
        {
            _path = path;
            int centerIndex = path.IndexAtFraction(Ramp.At);
            _centerDistance = path.Samples[centerIndex].Distance;
            float spacing = path.TotalLength / path.Count;
            int halfSpan = Mathf.Max(1, Mathf.RoundToInt(Ramp.Length / 2f / spacing));

            Transform root = SceneryBuilder.Group(parent, "Rampa de turbo", Vector3.zero, Quaternion.identity);
            BuildDecal(root, centerIndex, halfSpan);
            BuildArrows(root, centerIndex, halfSpan);
            BuildPosts(root, centerIndex, halfSpan);
        }

        /// <summary>Dispara o turbo se o kart estiver em cima do tapete (renovado a cada passo da travessia).</summary>
        public void Collide(Kart kart)
        {
            Vector3 position = kart.Body.ChassisPosition;
            int index = _path.NearestSampleIndex(position.x, position.z);
            float lateral = _path.LateralOffset(index, position.x, position.z);
            if (Mathf.Abs(lateral - Ramp.Lateral) > Ramp.HalfWidth) return;

            float along = _path.Samples[index].Distance - _centerDistance;
            if (along > _path.TotalLength / 2f) along -= _path.TotalLength;
            if (along < -_path.TotalLength / 2f) along += _path.TotalLength;
            if (Mathf.Abs(along) > Ramp.Length / 2f) return;

            kart.Drift.TriggerBoost(Ramp.BoostLevel);
        }

        /// <summary>O tapete e as duas bordas listradas, seguindo o traçado.</summary>
        private void BuildDecal(Transform root, int centerIndex, int halfSpan)
        {
            float left = Ramp.Lateral + Ramp.HalfWidth;
            float right = Ramp.Lateral - Ramp.HalfWidth;
            const float rail = 0.42f;

            Material pad = CandyMaterials.Ribbon(null, 0.2f, -4f);
            pad.SetColor("_Color", Ramp.PadLight);
            CandyMaterials.SetGlow(pad, Ramp.PadLight, 0.08f);
            Material stripe = CandyMaterials.Ribbon(null, 0.18f, -5f);
            stripe.SetColor("_Color", Ramp.PadStripe);
            CandyMaterials.SetGlow(stripe, Ramp.PadStripe, 0.16f);

            AddRibbon(root, "Tapete", pad, centerIndex, halfSpan, left - rail, right + rail);
            AddRibbon(root, "Borda", stripe, centerIndex, halfSpan, left, left - rail);
            AddRibbon(root, "Borda", stripe, centerIndex, halfSpan, right + rail, right);
        }

        private void AddRibbon(Transform root, string name, Material material, int centerIndex, int halfSpan, float leftLateral, float rightLateral)
        {
            var builder = new MeshBuilder();
            int count = _path.Count;
            for (int step = -halfSpan; step < halfSpan; step++)
            {
                int index = (centerIndex + step + count * 2) % count;
                int next = (index + 1) % count;
                builder.Quad(
                    _path.PointAt(index, leftLateral, Ramp.Height),
                    _path.PointAt(index, rightLateral, Ramp.Height),
                    _path.PointAt(next, leftLateral, Ramp.Height),
                    _path.PointAt(next, rightLateral, Ramp.Height),
                    Color.white);
            }
            SceneryBuilder.Part(root, name, builder.Build(name), material, Vector3.zero, false);
        }

        /// <summary>Setas de neon (divisas) apontando no sentido de corrida.</summary>
        private void BuildArrows(Transform root, int centerIndex, int halfSpan)
        {
            Material arrow = CandyMaterials.Glow(Ramp.Arrow, 1.6f);
            Mesh bar = ProceduralMeshes.RoundedBox(new Vector3(Ramp.HalfWidth * 1.25f, 0.05f, 0.4f), 0.05f, 2);
            int count = _path.Count;

            for (int i = 0; i < Ramp.Chevrons; i++)
            {
                int offset = Mathf.RoundToInt(-halfSpan + 2f * halfSpan * (i + 0.7f) / (Ramp.Chevrons + 0.4f));
                int index = (centerIndex + offset + count * 2) % count;
                float heading = _path.HeadingAt(index);
                foreach (int side in new[] { 1, -1 })
                {
                    Vector3 position = _path.PointAt(index, Ramp.Lateral + side * Ramp.HalfWidth * 0.42f, Ramp.ArrowHeight);
                    // Espelhado em relação à web (heading + side * 0.62): cada barra inclina para dentro, formando "^".
                    SceneryBuilder.Part(root, "Seta", bar, arrow, position, SceneryBuilder.Yaw(heading - side * 0.62f), Vector3.one, false);
                }
            }
        }

        /// <summary>Postes de bala marcando a entrada e a saída, FORA do asfalto.</summary>
        private void BuildPosts(Transform root, int centerIndex, int halfSpan)
        {
            Material post = CandyMaterials.Candy(Ramp.Post, 0.22f);
            Material top = CandyMaterials.Glow(Ramp.PostTop, 0.15f);
            Mesh mast = ProceduralMeshes.Cylinder(0.16f, 0.18f, 1.5f, 12);
            Mesh ball = ProceduralMeshes.Primitive(PrimitiveType.Sphere);
            int count = _path.Count;

            foreach (int step in new[] { -halfSpan, halfSpan })
            {
                int index = (centerIndex + step + count * 2) % count;
                float edge = _path.Samples[index].HalfWidth + 0.9f;
                foreach (int side in new[] { 1, -1 })
                {
                    Vector3 ground = _path.PointAt(index, side * edge, 0f);
                    SceneryBuilder.Part(root, "Poste", mast, post, ground + Vector3.up * 0.75f);
                    SceneryBuilder.Part(root, "Bola", ball, top, ground + Vector3.up * 1.62f, Quaternion.identity, Vector3.one * 0.6f);
                }
            }
        }
    }
}
