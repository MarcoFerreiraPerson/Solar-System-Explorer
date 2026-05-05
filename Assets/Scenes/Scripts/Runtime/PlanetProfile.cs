using System.Collections.Generic;
using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public readonly struct TerrainPalette
    {
        public readonly Color Low;
        public readonly Color Mid;
        public readonly Color High;
        public readonly Color Peak;

        public TerrainPalette(Color low, Color mid, Color high, Color peak)
        {
            Low = low;
            Mid = mid;
            High = high;
            Peak = peak;
        }
    }

    public readonly struct OceanPalette
    {
        public readonly Color Shallow;
        public readonly Color Deep;

        public OceanPalette(Color shallow, Color deep)
        {
            Shallow = shallow;
            Deep = deep;
        }

        public static OceanPalette Default => new OceanPalette(
            new Color(0.22f, 0.70f, 0.78f, 1f),
            new Color(0.05f, 0.18f, 0.35f, 1f));
    }

    public sealed class PlanetProfile
    {
        public string Name { get; }
        public float Diameter { get; }
        public float OrbitRadius { get; }
        public float InitialOrbitAngleDeg { get; }
        public float OrbitBand { get; }
        public float AxialSpinDegPerSec { get; }
        public int Seed { get; }
        public bool HasOcean { get; }
        public bool IsGasGiant { get; }
        public float TerrainSmoothness { get; }
        public float PerturbStrength { get; }
        public float MountainStrength { get; }
        public float OceanDepth { get; }
        public TerrainPalette TerrainPalette { get; }
        public OceanPalette OceanColors { get; }
        public float Radius => Diameter * 0.5f;
        public float ScaledDiameter => Diameter * PlanetCatalog.SystemScale;
        public float ScaledRadius => Radius * PlanetCatalog.SystemScale;
        public float ScaledOrbitRadius => OrbitRadius * PlanetCatalog.SystemScale;
        public float ScaledOrbitBand => OrbitBand * PlanetCatalog.SystemScale;

        public PlanetProfile(
            string name,
            float diameter,
            float orbitRadius,
            float initialOrbitAngleDeg,
            float orbitBand,
            float axialSpinDegPerSec,
            int seed,
            bool hasOcean,
            bool isGasGiant,
            float terrainSmoothness,
            float perturbStrength,
            float mountainStrength,
            float oceanDepth,
            TerrainPalette terrainPalette,
            OceanPalette oceanColors)
        {
            Name = name;
            Diameter = diameter;
            OrbitRadius = orbitRadius;
            InitialOrbitAngleDeg = initialOrbitAngleDeg;
            OrbitBand = orbitBand;
            AxialSpinDegPerSec = axialSpinDegPerSec;
            Seed = seed;
            HasOcean = hasOcean;
            IsGasGiant = isGasGiant;
            TerrainSmoothness = terrainSmoothness;
            PerturbStrength = perturbStrength;
            MountainStrength = mountainStrength;
            OceanDepth = oceanDepth;
            TerrainPalette = terrainPalette;
            OceanColors = oceanColors;
        }
    }

    public static class PlanetCatalog
    {
        public const float SystemScale = 5f;
        public const float AxialRotationScale = 0.25f;

        public static readonly IReadOnlyList<PlanetProfile> All = new[]
        {
            new PlanetProfile(
                "Mercury",
                180f,
                1400f,
                0f,
                60f,
                4.8f,
                101,
                false,
                false,
                0.08f,
                0.20f,
                0.28f,
                0.75f,
                new TerrainPalette(
                    new Color(0.20f, 0.20f, 0.22f, 1f),
                    new Color(0.34f, 0.31f, 0.29f, 1f),
                    new Color(0.56f, 0.43f, 0.33f, 1f),
                    new Color(0.74f, 0.66f, 0.58f, 1f)),
                OceanPalette.Default),
            new PlanetProfile(
                "Venus",
                360f,
                2800f,
                45f,
                90f,
                3.2f,
                202,
                false,
                false,
                0.20f,
                0.12f,
                0.16f,
                0.85f,
                new TerrainPalette(
                    new Color(0.63f, 0.48f, 0.20f, 1f),
                    new Color(0.78f, 0.61f, 0.24f, 1f),
                    new Color(0.90f, 0.74f, 0.36f, 1f),
                    new Color(0.99f, 0.89f, 0.61f, 1f)),
                OceanPalette.Default),
            new PlanetProfile(
                "Earth",
                300f,
                4800f,
                90f,
                120f,
                3.0f,
                303,
                true,
                false,
                0.15f,
                0.22f,
                0.55f,
                1.36f,
                new TerrainPalette(
                    new Color(0.80f, 0.75f, 0.55f, 1f),
                    new Color(0.25f, 0.48f, 0.19f, 1f),
                    new Color(0.45f, 0.39f, 0.28f, 1f),
                    new Color(0.92f, 0.94f, 0.96f, 1f)),
                new OceanPalette(
                    new Color(0.24f, 0.73f, 0.78f, 1f),
                    new Color(0.04f, 0.15f, 0.35f, 1f))),
            new PlanetProfile(
                "Mars",
                240f,
                7000f,
                135f,
                140f,
                2.8f,
                404,
                false,
                false,
                0.10f,
                0.18f,
                0.38f,
                0.70f,
                new TerrainPalette(
                    new Color(0.39f, 0.15f, 0.10f, 1f),
                    new Color(0.60f, 0.24f, 0.14f, 1f),
                    new Color(0.81f, 0.40f, 0.19f, 1f),
                    new Color(0.95f, 0.78f, 0.60f, 1f)),
                OceanPalette.Default),
            new PlanetProfile(
                "Jupiter",
                620f,
                9800f,
                180f,
                180f,
                1.8f,
                505,
                false,
                true,
                0.34f,
                0.08f,
                0.10f,
                0.60f,
                new TerrainPalette(
                    new Color(0.55f, 0.34f, 0.20f, 1f),
                    new Color(0.72f, 0.49f, 0.28f, 1f),
                    new Color(0.88f, 0.68f, 0.43f, 1f),
                    new Color(0.98f, 0.86f, 0.66f, 1f)),
                OceanPalette.Default),
            new PlanetProfile(
                "Saturn",
                520f,
                13200f,
                225f,
                220f,
                1.5f,
                606,
                false,
                true,
                0.40f,
                0.06f,
                0.07f,
                0.60f,
                new TerrainPalette(
                    new Color(0.58f, 0.46f, 0.25f, 1f),
                    new Color(0.76f, 0.63f, 0.34f, 1f),
                    new Color(0.90f, 0.79f, 0.52f, 1f),
                    new Color(0.98f, 0.92f, 0.74f, 1f)),
                OceanPalette.Default),
            new PlanetProfile(
                "Uranus",
                340f,
                17000f,
                270f,
                260f,
                1.2f,
                707,
                false,
                true,
                0.50f,
                0.06f,
                0.06f,
                0.55f,
                new TerrainPalette(
                    new Color(0.74f, 0.85f, 0.88f, 1f),
                    new Color(0.86f, 0.93f, 0.95f, 1f),
                    new Color(0.95f, 0.98f, 0.99f, 1f),
                    new Color(1.00f, 1.00f, 1.00f, 1f)),
                OceanPalette.Default),
            new PlanetProfile(
                "Neptune",
                420f,
                21000f,
                315f,
                320f,
                1.0f,
                808,
                true,
                true,
                0.52f,
                0.05f,
                0.05f,
                1.10f,
                new TerrainPalette(
                    new Color(0.08f, 0.16f, 0.24f, 1f),
                    new Color(0.13f, 0.27f, 0.38f, 1f),
                    new Color(0.22f, 0.43f, 0.58f, 1f),
                    new Color(0.62f, 0.82f, 0.92f, 1f)),
                new OceanPalette(
                    new Color(0.12f, 0.54f, 0.88f, 1f),
                    new Color(0.02f, 0.08f, 0.24f, 1f))),
        };

        public static PlanetProfile Earth
        {
            get
            {
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].Name == "Earth")
                    {
                        return All[i];
                    }
                }

                return All[0];
            }
        }
    }
}
