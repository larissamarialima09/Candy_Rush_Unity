using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Track
{
    /// <summary>
    /// Curva Catmull-Rom FECHADA e centrípeta, reproduzindo exatamente o
    /// CatmullRomCurve3 do Three.js (closed = true, 'centripetal'), incluindo
    /// o getSpacedPoints com reparametrização por comprimento de arco.
    ///
    /// Reproduzir o algoritmo do Three.js (e não usar outra spline qualquer)
    /// garante que o traçado fique idêntico ao da versão web, metro a metro.
    /// Os cálculos são em double para não acumular erro na volta inteira.
    /// </summary>
    public sealed class CatmullRomCurve
    {
        /// <summary>Divisões usadas para medir o comprimento de arco (igual ao Three.js).</summary>
        private const int ArcLengthDivisions = 200;

        private readonly Vector3d[] _points;
        private double[] _arcLengths;

        public CatmullRomCurve(IReadOnlyList<Vector3> points)
        {
            _points = new Vector3d[points.Count];
            for (int i = 0; i < points.Count; i++) _points[i] = new Vector3d(points[i]);
        }

        /// <summary>Ponto da curva no parâmetro t (0..1), SEM correção de comprimento de arco.</summary>
        public Vector3d GetPoint(double t)
        {
            int l = _points.Length;
            double p = l * t;
            int intPoint = (int)Math.Floor(p);
            double weight = p - intPoint;

            if (intPoint <= 0) intPoint += ((int)Math.Floor(Math.Abs((double)intPoint) / l) + 1) * l;

            Vector3d p0 = _points[(intPoint - 1) % l];
            Vector3d p1 = _points[intPoint % l];
            Vector3d p2 = _points[(intPoint + 1) % l];
            Vector3d p3 = _points[(intPoint + 2) % l];

            // Centrípeta: distâncias elevadas a 0.25 (raiz quadrada da distância euclidiana).
            double dt0 = Math.Pow(p0.DistanceSquared(p1), 0.25);
            double dt1 = Math.Pow(p1.DistanceSquared(p2), 0.25);
            double dt2 = Math.Pow(p2.DistanceSquared(p3), 0.25);

            // Proteção contra pontos repetidos.
            if (dt1 < 1e-4) dt1 = 1.0;
            if (dt0 < 1e-4) dt0 = dt1;
            if (dt2 < 1e-4) dt2 = dt1;

            return new Vector3d(
                NonUniform(p0.X, p1.X, p2.X, p3.X, dt0, dt1, dt2, weight),
                NonUniform(p0.Y, p1.Y, p2.Y, p3.Y, dt0, dt1, dt2, weight),
                NonUniform(p0.Z, p1.Z, p2.Z, p3.Z, dt0, dt1, dt2, weight));
        }

        /// <summary>Ponto a uma fração u do COMPRIMENTO da curva (0..1).</summary>
        public Vector3d GetPointAt(double u) => GetPoint(ArcLengthToT(u));

        /// <summary>
        /// divisions + 1 pontos igualmente espaçados por comprimento de arco
        /// (o último coincide com o primeiro, pois a curva é fechada).
        /// </summary>
        public Vector3d[] GetSpacedPoints(int divisions)
        {
            var points = new Vector3d[divisions + 1];
            for (int d = 0; d <= divisions; d++) points[d] = GetPointAt((double)d / divisions);
            return points;
        }

        private double[] ArcLengths()
        {
            if (_arcLengths != null) return _arcLengths;

            _arcLengths = new double[ArcLengthDivisions + 1];
            Vector3d last = GetPoint(0);
            double sum = 0;
            for (int p = 1; p <= ArcLengthDivisions; p++)
            {
                Vector3d current = GetPoint((double)p / ArcLengthDivisions);
                sum += current.Distance(last);
                _arcLengths[p] = sum;
                last = current;
            }
            return _arcLengths;
        }

        /// <summary>Converte fração de comprimento (u) em parâmetro da curva (t), como o getUtoTmapping do Three.js.</summary>
        private double ArcLengthToT(double u)
        {
            double[] arcLengths = ArcLengths();
            int count = arcLengths.Length;
            double target = u * arcLengths[count - 1];

            // Busca binária pelo maior índice com comprimento <= alvo.
            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int i = low + (high - low) / 2;
                double comparison = arcLengths[i] - target;
                if (comparison < 0) low = i + 1;
                else if (comparison > 0) high = i - 1;
                else
                {
                    high = i;
                    break;
                }
            }

            int index = high;
            if (arcLengths[index] == target) return (double)index / (count - 1);

            double before = arcLengths[index];
            double after = arcLengths[index + 1];
            double fraction = (target - before) / (after - before);
            return (index + fraction) / (count - 1);
        }

        /// <summary>Polinômio cúbico da Catmull-Rom não uniforme em uma coordenada.</summary>
        private static double NonUniform(double x0, double x1, double x2, double x3,
            double dt0, double dt1, double dt2, double t)
        {
            double t1 = (x1 - x0) / dt0 - (x2 - x0) / (dt0 + dt1) + (x2 - x1) / dt1;
            double t2 = (x2 - x1) / dt1 - (x3 - x1) / (dt1 + dt2) + (x3 - x2) / dt2;
            t1 *= dt1;
            t2 *= dt1;

            double c0 = x1;
            double c1 = t1;
            double c2 = -3 * x1 + 3 * x2 - 2 * t1 - t2;
            double c3 = 2 * x1 - 2 * x2 + t1 + t2;
            return c0 + c1 * t + c2 * t * t + c3 * t * t * t;
        }
    }

    /// <summary>Vetor em double, só para a geração da pista.</summary>
    public readonly struct Vector3d
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3d(Vector3 v) : this(v.x, v.y, v.z) { }

        public double DistanceSquared(Vector3d other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public double Distance(Vector3d other) => Math.Sqrt(DistanceSquared(other));

        public Vector3 ToVector3() => new Vector3((float)X, (float)Y, (float)Z);
    }
}
