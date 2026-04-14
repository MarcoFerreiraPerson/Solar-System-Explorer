using UnityEngine;

namespace SolarSystemExplorer.Runtime.WebGL.CpuPlanetCompute
{
    // CPU port of PerturbPoints.compute / PerturbPointsPlatelike.compute.
    // Applies the same offset formula as the GPU kernels and re-normalises to
    // the vertex's original radius, so sphere radius is preserved.
    internal static class CpuPerturbPoints
    {
        public static void PerturbStandard(Vector3[] points, float maxStrength)
        {
            if (points == null) return;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 pos = points[i];
                float height = pos.magnitude;
                Vector3 offset = PerturbStandard(pos);
                Vector3 newPos = pos + offset * maxStrength;
                points[i] = newPos.normalized * height;
            }
        }

        public static void PerturbPlatelike(Vector3[] points, float maxStrength)
        {
            if (points == null) return;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 pos = points[i];
                float height = pos.magnitude;
                Vector3 offset = PerturbPlatelike(pos);
                Vector3 newPos = pos + offset * maxStrength;
                points[i] = newPos.normalized * height;
            }
        }

        static Vector3 PerturbStandard(Vector3 pos)
        {
            const float scale = 50f;
            const int layers = 2;
            const float persistence = 0.5f;
            const float lacunarity = 2f;
            const float multiplier = 1f;
            float fx = CpuNoise.SimpleNoise(pos * 1f, layers, scale, persistence, lacunarity, multiplier);
            float fy = CpuNoise.SimpleNoise(pos * 2f, layers, scale, persistence, lacunarity, multiplier);
            float fz = CpuNoise.SimpleNoise(pos * 3f, layers, scale, persistence, lacunarity, multiplier);
            Vector3 offset = new Vector3(fx, fy, fz);
            return CpuNoise.Smoothstep(-1f, 1f, offset) * 2f - Vector3.one;
        }

        static Vector3 PerturbPlatelike(Vector3 pos)
        {
            Vector4 noise = CpuNoise.FractalNoiseGrad(pos, 4, 25f, 0.5f, 2f);
            Vector3 n = new Vector3(noise.x, noise.y, noise.z);
            return CpuNoise.Smoothstep(-1f, 1f, n) * 2f - Vector3.one;
        }
    }

    internal sealed class CpuPerturbStandardKernel : ICpuPerturbKernel
    {
        public void Perturb(Vector3[] vertices, float perturbStrength) => CpuPerturbPoints.PerturbStandard(vertices, perturbStrength);
    }

    internal sealed class CpuPerturbPlatelikeKernel : ICpuPerturbKernel
    {
        public void Perturb(Vector3[] vertices, float perturbStrength) => CpuPerturbPoints.PerturbPlatelike(vertices, perturbStrength);
    }
}
