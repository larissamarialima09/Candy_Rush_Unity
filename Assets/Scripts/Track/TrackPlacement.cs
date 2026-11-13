using System;
using UnityEngine;

namespace CandyRush.Track
{
    /// <summary>
    /// Posicionamento de objetos no referencial da pista (convertido de
    /// placeAlong, placementAt e headingOf em src/track/circuitScenery.ts).
    ///
    /// ESPELHAMENTO: como o mundo na Unity é o espelho do Three.js, ângulos
    /// que o código da web SOMA ao rumo da pista (ex.: heading + side * 0.62)
    /// entram aqui com o sinal trocado. Deslocamentos laterais não mudam:
    /// Left é a esquerda de verdade dos dois lados.
    /// </summary>
    public static class TrackPlacement
    {
        /// <summary>Percorre a pista chamando visit(amostra, índice) a cada spacing metros.</summary>
        public static void Along(CircuitPath path, float spacing, Action<PathSample, int> visit)
        {
            float nextDistance = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                PathSample sample = path.Samples[i];
                if (sample.Distance < nextDistance) continue;
                nextDistance = sample.Distance + spacing;
                visit(sample, i);
            }
        }

        /// <summary>
        /// Ponto no referencial da pista. A altura é medida a partir do TERRENO
        /// naquele deslocamento lateral (a pista sobe e desce).
        /// </summary>
        public static Vector3 At(CircuitPath path, int index, float lateral, float along, float height)
        {
            PathSample sample = path.Samples[index];
            Vector3 position = sample.Position + sample.Left * lateral + sample.Tangent * along;
            position.y = path.SurfaceHeight(index, lateral) + height;
            return position;
        }

        /// <summary>Rotação que alinha um objeto com o sentido de corrida naquela amostra.</summary>
        public static Quaternion Facing(CircuitPath path, int index) =>
            Quaternion.AngleAxis(path.HeadingAt(index) * Mathf.Rad2Deg, Vector3.up);
    }
}
