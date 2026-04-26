using UnityEngine;

namespace SolarSystemExplorer.Runtime.WebGL.CpuPlanetCompute
{
    // C# port of the HLSL noise helpers under
    // Assets/ThirdParty/CelestialBodySystem/Celestial Body/Scripts/Shaders/Includes/.
    // Kept numerically faithful to the GPU path so WebGL results match editor output.
    internal static class CpuNoise
    {
        internal readonly struct NoiseParams
        {
            public readonly Vector3 Offset;
            public readonly int NumLayers;
            public readonly float Persistence;
            public readonly float Lacunarity;
            public readonly float Scale;
            public readonly float Multiplier;
            public readonly float VerticalShift;

            public NoiseParams(Vector3 offset, int numLayers, float persistence, float lacunarity, float scale, float multiplier, float verticalShift)
            {
                Offset = offset;
                NumLayers = numLayers;
                Persistence = persistence;
                Lacunarity = lacunarity;
                Scale = scale;
                Multiplier = multiplier;
                VerticalShift = verticalShift;
            }
        }

        internal readonly struct RidgeNoiseParams
        {
            public readonly NoiseParams Base;
            public readonly float Power;
            public readonly float Gain;
            public readonly float PeakSmoothing;

            public RidgeNoiseParams(NoiseParams noiseParams, float power, float gain, float peakSmoothing)
            {
                Base = noiseParams;
                Power = power;
                Gain = gain;
                PeakSmoothing = peakSmoothing;
            }
        }

        // ---- Vector helpers (HLSL parity) --------------------------------

        static Vector3 Floor(Vector3 v) => new Vector3(Mathf.Floor(v.x), Mathf.Floor(v.y), Mathf.Floor(v.z));
        static Vector4 Floor(Vector4 v) => new Vector4(Mathf.Floor(v.x), Mathf.Floor(v.y), Mathf.Floor(v.z), Mathf.Floor(v.w));
        static Vector4 Abs(Vector4 v) => new Vector4(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z), Mathf.Abs(v.w));

        static Vector3 Mod289(Vector3 x) => x - Floor(x / 289f) * 289f;
        static Vector4 Mod289(Vector4 x) => x - Floor(x / 289f) * 289f;

        static Vector4 Permute(Vector4 x)
        {
            // (x * 34 + 1) * x, component-wise, then mod289
            var r = new Vector4(
                (x.x * 34f + 1f) * x.x,
                (x.y * 34f + 1f) * x.y,
                (x.z * 34f + 1f) * x.z,
                (x.w * 34f + 1f) * x.w);
            return Mod289(r);
        }

        static Vector4 TaylorInvSqrt(Vector4 r)
        {
            const float a = 1.79284291400159f;
            const float b = 0.85373472095314f;
            return new Vector4(a - r.x * b, a - r.y * b, a - r.z * b, a - r.w * b);
        }

        static Vector3 StepV3(Vector3 edge, Vector3 x) => new Vector3(
            x.x >= edge.x ? 1f : 0f,
            x.y >= edge.y ? 1f : 0f,
            x.z >= edge.z ? 1f : 0f);

        static Vector4 StepScalar(float edge, Vector4 x) => new Vector4(
            x.x >= edge ? 1f : 0f, x.y >= edge ? 1f : 0f, x.z >= edge ? 1f : 0f, x.w >= edge ? 1f : 0f);

        static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));

        // ---- 3D simplex noise (Ashima / webgl-noise) ---------------------
        // Direct translation of snoise(float3) in SimplexNoise.cginc.

        public static float Snoise(Vector3 v)
        {
            const float Cx = 1f / 6f;
            const float Cy = 1f / 3f;

            float dotCy = v.x * Cy + v.y * Cy + v.z * Cy;
            Vector3 i = Floor(new Vector3(v.x + dotCy, v.y + dotCy, v.z + dotCy));
            float dotCx = i.x * Cx + i.y * Cx + i.z * Cx;
            Vector3 x0 = new Vector3(v.x - i.x + dotCx, v.y - i.y + dotCx, v.z - i.z + dotCx);

            Vector3 x0yzx = new Vector3(x0.y, x0.z, x0.x);
            Vector3 g = StepV3(x0yzx, x0);
            Vector3 l = new Vector3(1f - g.x, 1f - g.y, 1f - g.z);
            Vector3 lzxy = new Vector3(l.z, l.x, l.y);
            Vector3 i1 = Min(g, lzxy);
            Vector3 i2 = Max(g, lzxy);

            Vector3 x1 = new Vector3(x0.x - i1.x + Cx, x0.y - i1.y + Cx, x0.z - i1.z + Cx);
            Vector3 x2 = new Vector3(x0.x - i2.x + Cy, x0.y - i2.y + Cy, x0.z - i2.z + Cy);
            Vector3 x3 = new Vector3(x0.x - 0.5f, x0.y - 0.5f, x0.z - 0.5f);

            i = Mod289(i);
            Vector4 p = Permute(Permute(Permute(
                new Vector4(i.z + 0f, i.z + i1.z, i.z + i2.z, i.z + 1f))
                + new Vector4(i.y + 0f, i.y + i1.y, i.y + i2.y, i.y + 1f))
                + new Vector4(i.x + 0f, i.x + i1.x, i.x + i2.x, i.x + 1f));

            Vector4 j = p - 49f * Floor(p / 49f);

            Vector4 x_ = Floor(j / 7f);
            Vector4 y_ = Floor(j - 7f * x_);

            Vector4 x = new Vector4((x_.x * 2f + 0.5f) / 7f - 1f, (x_.y * 2f + 0.5f) / 7f - 1f, (x_.z * 2f + 0.5f) / 7f - 1f, (x_.w * 2f + 0.5f) / 7f - 1f);
            Vector4 y = new Vector4((y_.x * 2f + 0.5f) / 7f - 1f, (y_.y * 2f + 0.5f) / 7f - 1f, (y_.z * 2f + 0.5f) / 7f - 1f, (y_.w * 2f + 0.5f) / 7f - 1f);

            Vector4 ax = Abs(x);
            Vector4 ay = Abs(y);
            Vector4 h = new Vector4(1f - ax.x - ay.x, 1f - ax.y - ay.y, 1f - ax.z - ay.z, 1f - ax.w - ay.w);

            Vector4 b0 = new Vector4(x.x, x.y, y.x, y.y);
            Vector4 b1 = new Vector4(x.z, x.w, y.z, y.w);

            Vector4 s0 = new Vector4(Mathf.Floor(b0.x) * 2f + 1f, Mathf.Floor(b0.y) * 2f + 1f, Mathf.Floor(b0.z) * 2f + 1f, Mathf.Floor(b0.w) * 2f + 1f);
            Vector4 s1 = new Vector4(Mathf.Floor(b1.x) * 2f + 1f, Mathf.Floor(b1.y) * 2f + 1f, Mathf.Floor(b1.z) * 2f + 1f, Mathf.Floor(b1.w) * 2f + 1f);
            Vector4 sh = -StepScalar(0f, h);

            Vector4 b0_xzyw = new Vector4(b0.x, b0.z, b0.y, b0.w);
            Vector4 b1_xzyw = new Vector4(b1.x, b1.z, b1.y, b1.w);
            Vector4 s0_xzyw = new Vector4(s0.x, s0.z, s0.y, s0.w);
            Vector4 s1_xzyw = new Vector4(s1.x, s1.z, s1.y, s1.w);
            Vector4 sh_xxyy = new Vector4(sh.x, sh.x, sh.y, sh.y);
            Vector4 sh_zzww = new Vector4(sh.z, sh.z, sh.w, sh.w);

            Vector4 a0 = new Vector4(
                b0_xzyw.x + s0_xzyw.x * sh_xxyy.x,
                b0_xzyw.y + s0_xzyw.y * sh_xxyy.y,
                b0_xzyw.z + s0_xzyw.z * sh_xxyy.z,
                b0_xzyw.w + s0_xzyw.w * sh_xxyy.w);
            Vector4 a1 = new Vector4(
                b1_xzyw.x + s1_xzyw.x * sh_zzww.x,
                b1_xzyw.y + s1_xzyw.y * sh_zzww.y,
                b1_xzyw.z + s1_xzyw.z * sh_zzww.z,
                b1_xzyw.w + s1_xzyw.w * sh_zzww.w);

            Vector3 g0 = new Vector3(a0.x, a0.y, h.x);
            Vector3 g1 = new Vector3(a0.z, a0.w, h.y);
            Vector3 g2 = new Vector3(a1.x, a1.y, h.z);
            Vector3 g3 = new Vector3(a1.z, a1.w, h.w);

            Vector4 norm = TaylorInvSqrt(new Vector4(
                Vector3.Dot(g0, g0), Vector3.Dot(g1, g1), Vector3.Dot(g2, g2), Vector3.Dot(g3, g3)));
            g0 *= norm.x; g1 *= norm.y; g2 *= norm.z; g3 *= norm.w;

            Vector4 mv = new Vector4(
                Mathf.Max(0.6f - Vector3.Dot(x0, x0), 0f),
                Mathf.Max(0.6f - Vector3.Dot(x1, x1), 0f),
                Mathf.Max(0.6f - Vector3.Dot(x2, x2), 0f),
                Mathf.Max(0.6f - Vector3.Dot(x3, x3), 0f));
            mv = new Vector4(mv.x * mv.x, mv.y * mv.y, mv.z * mv.z, mv.w * mv.w);
            mv = new Vector4(mv.x * mv.x, mv.y * mv.y, mv.z * mv.z, mv.w * mv.w);

            Vector4 px = new Vector4(
                Vector3.Dot(x0, g0), Vector3.Dot(x1, g1), Vector3.Dot(x2, g2), Vector3.Dot(x3, g3));
            return 42f * Vector4.Dot(mv, px);
        }

        // Returns (grad.xyz, noiseValue) — parity with snoise_grad(float3).
        public static Vector4 SnoiseGrad(Vector3 v)
        {
            const float Cx = 1f / 6f;
            const float Cy = 1f / 3f;

            float dotCy = v.x * Cy + v.y * Cy + v.z * Cy;
            Vector3 i = Floor(new Vector3(v.x + dotCy, v.y + dotCy, v.z + dotCy));
            float dotCx = i.x * Cx + i.y * Cx + i.z * Cx;
            Vector3 x0 = new Vector3(v.x - i.x + dotCx, v.y - i.y + dotCx, v.z - i.z + dotCx);

            Vector3 x0yzx = new Vector3(x0.y, x0.z, x0.x);
            Vector3 g = StepV3(x0yzx, x0);
            Vector3 l = new Vector3(1f - g.x, 1f - g.y, 1f - g.z);
            Vector3 lzxy = new Vector3(l.z, l.x, l.y);
            Vector3 i1 = Min(g, lzxy);
            Vector3 i2 = Max(g, lzxy);

            Vector3 x1 = new Vector3(x0.x - i1.x + Cx, x0.y - i1.y + Cx, x0.z - i1.z + Cx);
            Vector3 x2 = new Vector3(x0.x - i2.x + Cy, x0.y - i2.y + Cy, x0.z - i2.z + Cy);
            Vector3 x3 = new Vector3(x0.x - 0.5f, x0.y - 0.5f, x0.z - 0.5f);

            i = Mod289(i);
            Vector4 p = Permute(Permute(Permute(
                new Vector4(i.z + 0f, i.z + i1.z, i.z + i2.z, i.z + 1f))
                + new Vector4(i.y + 0f, i.y + i1.y, i.y + i2.y, i.y + 1f))
                + new Vector4(i.x + 0f, i.x + i1.x, i.x + i2.x, i.x + 1f));

            Vector4 j = p - 49f * Floor(p / 49f);

            Vector4 x_ = Floor(j / 7f);
            Vector4 y_ = Floor(j - 7f * x_);

            Vector4 x = new Vector4((x_.x * 2f + 0.5f) / 7f - 1f, (x_.y * 2f + 0.5f) / 7f - 1f, (x_.z * 2f + 0.5f) / 7f - 1f, (x_.w * 2f + 0.5f) / 7f - 1f);
            Vector4 y = new Vector4((y_.x * 2f + 0.5f) / 7f - 1f, (y_.y * 2f + 0.5f) / 7f - 1f, (y_.z * 2f + 0.5f) / 7f - 1f, (y_.w * 2f + 0.5f) / 7f - 1f);

            Vector4 ax = Abs(x);
            Vector4 ay = Abs(y);
            Vector4 h = new Vector4(1f - ax.x - ay.x, 1f - ax.y - ay.y, 1f - ax.z - ay.z, 1f - ax.w - ay.w);

            Vector4 b0 = new Vector4(x.x, x.y, y.x, y.y);
            Vector4 b1 = new Vector4(x.z, x.w, y.z, y.w);
            Vector4 s0 = new Vector4(Mathf.Floor(b0.x) * 2f + 1f, Mathf.Floor(b0.y) * 2f + 1f, Mathf.Floor(b0.z) * 2f + 1f, Mathf.Floor(b0.w) * 2f + 1f);
            Vector4 s1 = new Vector4(Mathf.Floor(b1.x) * 2f + 1f, Mathf.Floor(b1.y) * 2f + 1f, Mathf.Floor(b1.z) * 2f + 1f, Mathf.Floor(b1.w) * 2f + 1f);
            Vector4 sh = -StepScalar(0f, h);

            Vector4 b0_xzyw = new Vector4(b0.x, b0.z, b0.y, b0.w);
            Vector4 b1_xzyw = new Vector4(b1.x, b1.z, b1.y, b1.w);
            Vector4 s0_xzyw = new Vector4(s0.x, s0.z, s0.y, s0.w);
            Vector4 s1_xzyw = new Vector4(s1.x, s1.z, s1.y, s1.w);
            Vector4 sh_xxyy = new Vector4(sh.x, sh.x, sh.y, sh.y);
            Vector4 sh_zzww = new Vector4(sh.z, sh.z, sh.w, sh.w);

            Vector4 a0 = new Vector4(
                b0_xzyw.x + s0_xzyw.x * sh_xxyy.x,
                b0_xzyw.y + s0_xzyw.y * sh_xxyy.y,
                b0_xzyw.z + s0_xzyw.z * sh_xxyy.z,
                b0_xzyw.w + s0_xzyw.w * sh_xxyy.w);
            Vector4 a1 = new Vector4(
                b1_xzyw.x + s1_xzyw.x * sh_zzww.x,
                b1_xzyw.y + s1_xzyw.y * sh_zzww.y,
                b1_xzyw.z + s1_xzyw.z * sh_zzww.z,
                b1_xzyw.w + s1_xzyw.w * sh_zzww.w);

            Vector3 g0 = new Vector3(a0.x, a0.y, h.x);
            Vector3 g1 = new Vector3(a0.z, a0.w, h.y);
            Vector3 g2 = new Vector3(a1.x, a1.y, h.z);
            Vector3 g3 = new Vector3(a1.z, a1.w, h.w);

            Vector4 norm = TaylorInvSqrt(new Vector4(
                Vector3.Dot(g0, g0), Vector3.Dot(g1, g1), Vector3.Dot(g2, g2), Vector3.Dot(g3, g3)));
            g0 *= norm.x; g1 *= norm.y; g2 *= norm.z; g3 *= norm.w;

            Vector4 mv = new Vector4(
                Mathf.Max(0.6f - Vector3.Dot(x0, x0), 0f),
                Mathf.Max(0.6f - Vector3.Dot(x1, x1), 0f),
                Mathf.Max(0.6f - Vector3.Dot(x2, x2), 0f),
                Mathf.Max(0.6f - Vector3.Dot(x3, x3), 0f));
            Vector4 m2 = new Vector4(mv.x * mv.x, mv.y * mv.y, mv.z * mv.z, mv.w * mv.w);
            Vector4 m3 = new Vector4(m2.x * mv.x, m2.y * mv.y, m2.z * mv.z, m2.w * mv.w);
            Vector4 m4 = new Vector4(m2.x * m2.x, m2.y * m2.y, m2.z * m2.z, m2.w * m2.w);

            float d0 = Vector3.Dot(x0, g0);
            float d1 = Vector3.Dot(x1, g1);
            float d2 = Vector3.Dot(x2, g2);
            float d3 = Vector3.Dot(x3, g3);

            Vector3 grad =
                -6f * m3.x * x0 * d0 + m4.x * g0 +
                -6f * m3.y * x1 * d1 + m4.y * g1 +
                -6f * m3.z * x2 * d2 + m4.z * g2 +
                -6f * m3.w * x3 * d3 + m4.w * g3;

            Vector4 px = new Vector4(d0, d1, d2, d3);
            float val = Vector4.Dot(m4, px);
            return 42f * new Vector4(grad.x, grad.y, grad.z, val);
        }

        // ---- Fractal noise helpers (FractalNoise.cginc parity) -----------

        public static float SimpleNoise(Vector3 pos, int numLayers, float scale, float persistence, float lacunarity, float multiplier)
        {
            float noiseSum = 0f;
            float amplitude = 1f;
            float frequency = scale;
            for (int i = 0; i < numLayers; i++)
            {
                noiseSum += Snoise(pos * frequency) * amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            return noiseSum * multiplier;
        }

        public static Vector4 FractalNoiseGrad(Vector3 pos, int numLayers, float scale, float persistence, float lacunarity)
        {
            Vector4 noise = Vector4.zero;
            float amplitude = 1f;
            float frequency = scale;
            for (int i = 0; i < numLayers; i++)
            {
                noise += SnoiseGrad(pos * frequency) * amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            return noise;
        }

        public static float SimpleNoise(Vector3 pos, NoiseParams noiseParams)
        {
            float noiseSum = 0f;
            float amplitude = 1f;
            float frequency = noiseParams.Scale;
            for (int i = 0; i < noiseParams.NumLayers; i++)
            {
                noiseSum += Snoise(pos * frequency + noiseParams.Offset) * amplitude;
                amplitude *= noiseParams.Persistence;
                frequency *= noiseParams.Lacunarity;
            }

            return noiseSum * noiseParams.Multiplier + noiseParams.VerticalShift;
        }

        public static float RidgedNoise(Vector3 pos, RidgeNoiseParams noiseParams)
        {
            float noiseSum = 0f;
            float amplitude = 1f;
            float frequency = noiseParams.Base.Scale;
            float ridgeWeight = 1f;

            for (int i = 0; i < noiseParams.Base.NumLayers; i++)
            {
                float noiseValue = 1f - Mathf.Abs(Snoise(pos * frequency + noiseParams.Base.Offset));
                noiseValue = Mathf.Pow(Mathf.Abs(noiseValue), noiseParams.Power);
                noiseValue *= ridgeWeight;
                ridgeWeight = Mathf.Clamp01(noiseValue * noiseParams.Gain);

                noiseSum += noiseValue * amplitude;
                amplitude *= noiseParams.Base.Persistence;
                frequency *= noiseParams.Base.Lacunarity;
            }

            return noiseSum * noiseParams.Base.Multiplier + noiseParams.Base.VerticalShift;
        }

        public static float SmoothedRidgedNoise(Vector3 pos, RidgeNoiseParams noiseParams)
        {
            Vector3 sphereNormal = pos.normalized;
            Vector3 axisA = Vector3.Cross(sphereNormal, Vector3.up);
            if (axisA.sqrMagnitude < 1e-6f)
            {
                axisA = Vector3.Cross(sphereNormal, Vector3.right);
            }
            axisA.Normalize();
            Vector3 axisB = Vector3.Cross(sphereNormal, axisA).normalized;

            float offsetDistance = noiseParams.PeakSmoothing * 0.01f;
            float sample0 = RidgedNoise(pos, noiseParams);
            float sample1 = RidgedNoise(pos - axisA * offsetDistance, noiseParams);
            float sample2 = RidgedNoise(pos + axisA * offsetDistance, noiseParams);
            float sample3 = RidgedNoise(pos - axisB * offsetDistance, noiseParams);
            float sample4 = RidgedNoise(pos + axisB * offsetDistance, noiseParams);
            return (sample0 + sample1 + sample2 + sample3 + sample4) / 5f;
        }

        public static float Blend(float startHeight, float blendDistance, float height)
        {
            return Smoothstep(startHeight - blendDistance / 2f, startHeight + blendDistance / 2f, height);
        }

        public static float SmoothMax(float a, float b, float k)
        {
            k = Mathf.Min(0f, -k);
            if (Mathf.Abs(k) < 1e-6f)
            {
                return Mathf.Max(a, b);
            }

            float h = Mathf.Clamp01((b - a + k) / (2f * k));
            return a * h + b * (1f - h) - k * h * (1f - h);
        }

        // HLSL smoothstep, component-wise Vector3.
        public static Vector3 Smoothstep(float edge0, float edge1, Vector3 x)
        {
            return new Vector3(Smoothstep(edge0, edge1, x.x), Smoothstep(edge0, edge1, x.y), Smoothstep(edge0, edge1, x.z));
        }

        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }

    internal static class CpuPlanetKernelFactory
    {
        public static ICpuPerturbKernel CreatePerturbKernel(ComputeShader perturbCompute)
        {
            if (perturbCompute == null)
            {
                return null;
            }

            string shaderName = perturbCompute.name ?? string.Empty;
            if (shaderName.Contains("Platelike"))
            {
                return new CpuPerturbPlatelikeKernel();
            }

            return new CpuPerturbStandardKernel();
        }
    }

    internal sealed class CpuEarthHeightKernel : ICpuHeightKernel
    {
        private readonly CpuNoise.NoiseParams continents;
        private readonly CpuNoise.NoiseParams mask;
        private readonly CpuNoise.RidgeNoiseParams mountains;
        private readonly float oceanDepthMultiplier;
        private readonly float oceanFloorDepth;
        private readonly float oceanFloorSmoothing;
        private readonly float mountainBlend;

        public CpuEarthHeightKernel(EarthShape shape, int seed)
        {
            var prng = new PRNG(seed);
            continents = CreateNoiseParams(shape.continentNoise, prng);
            mountains = CreateRidgeNoiseParams(shape.ridgeNoise, prng);
            mask = CreateNoiseParams(shape.maskNoise, prng);

            oceanDepthMultiplier = shape.oceanDepthMultiplier;
            oceanFloorDepth = shape.oceanFloorDepth;
            oceanFloorSmoothing = shape.oceanFloorSmoothing;
            mountainBlend = shape.mountainBlend;
        }

        public void CalculateHeights(Vector3[] vertices, float[] heights)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 pos = vertices[i];

                float continentShape = CpuNoise.SimpleNoise(pos, continents);
                continentShape = CpuNoise.SmoothMax(continentShape, -oceanFloorDepth, oceanFloorSmoothing);
                if (continentShape < 0f)
                {
                    continentShape *= 1f + oceanDepthMultiplier;
                }

                float ridgeNoise = CpuNoise.SmoothedRidgedNoise(pos, mountains);
                float maskValue = CpuNoise.Blend(0f, mountainBlend, CpuNoise.SimpleNoise(pos, mask));
                heights[i] = 1f + continentShape * 0.01f + ridgeNoise * 0.01f * maskValue;
            }
        }

        private static CpuNoise.NoiseParams CreateNoiseParams(SimpleNoiseSettings settings, PRNG prng)
        {
            Vector3 seededOffset = new Vector3(prng.Value(), prng.Value(), prng.Value()) * prng.Value() * 10000f;
            return new CpuNoise.NoiseParams(
                seededOffset + settings.offset,
                settings.numLayers,
                settings.persistence,
                settings.lacunarity,
                settings.scale,
                settings.elevation,
                settings.verticalShift);
        }

        private static CpuNoise.RidgeNoiseParams CreateRidgeNoiseParams(RidgeNoiseSettings settings, PRNG prng)
        {
            Vector3 seededOffset = new Vector3(prng.Value(), prng.Value(), prng.Value()) * prng.Value() * 10000f;
            return new CpuNoise.RidgeNoiseParams(
                new CpuNoise.NoiseParams(
                    seededOffset + settings.offset,
                    settings.numLayers,
                    settings.persistence,
                    settings.lacunarity,
                    settings.scale,
                    settings.elevation,
                    settings.verticalShift),
                settings.power,
                settings.gain,
                settings.peakSmoothing);
        }
    }

    internal sealed class CpuEarthShadingKernel : ICpuShadingDataKernel
    {
        private readonly CpuNoise.NoiseParams detailWarp;
        private readonly CpuNoise.NoiseParams detail;
        private readonly CpuNoise.NoiseParams large;
        private readonly CpuNoise.NoiseParams small;

        public CpuEarthShadingKernel(EarthShading shading, int seed)
        {
            var prng = new PRNG(seed);
            detail = CreateNoiseParams(shading.detailNoise, prng);
            detailWarp = CreateNoiseParams(shading.detailWarpNoise, prng);
            large = CreateNoiseParams(shading.largeNoise, prng);
            small = CreateNoiseParams(shading.smallNoise, prng);
        }

        public void CalculateShadingData(Vector3[] vertices, Vector4[] shadingData)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 pos = vertices[i];
                float largeNoise = CpuNoise.SimpleNoise(pos, large);
                float smallNoise = CpuNoise.SimpleNoise(pos, small);
                float detailWarpNoise = CpuNoise.SimpleNoise(pos, detailWarp);
                float detailNoise = CpuNoise.SimpleNoise(pos + Vector3.one * (detailWarpNoise * 0.1f), detail);

                shadingData[i] = new Vector4(largeNoise, detailNoise, smallNoise, 0f);
            }
        }

        private static CpuNoise.NoiseParams CreateNoiseParams(SimpleNoiseSettings settings, PRNG prng)
        {
            Vector3 seededOffset = new Vector3(prng.Value(), prng.Value(), prng.Value()) * prng.Value() * 10000f;
            return new CpuNoise.NoiseParams(
                seededOffset + settings.offset,
                settings.numLayers,
                settings.persistence,
                settings.lacunarity,
                settings.scale,
                settings.elevation,
                settings.verticalShift);
        }
    }
}
