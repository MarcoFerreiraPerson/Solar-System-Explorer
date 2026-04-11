using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    internal sealed class RuntimeBodyAssets
    {
        public CelestialBodySettings Settings { get; private set; }
        public CelestialBodyShape Shape { get; private set; }
        public CelestialBodyShading Shading { get; private set; }
        public OceanSettings OceanSettings { get; private set; }

        public static RuntimeBodyAssets Create(CelestialBodySettings baseSettings, PlanetProfile profile)
        {
            if (baseSettings == null || baseSettings.shape == null || baseSettings.shading == null)
            {
                return null;
            }

            var settingsClone = Object.Instantiate(baseSettings);
            settingsClone.name = $"{profile.Name} Settings";

            var shapeClone = Object.Instantiate(baseSettings.shape);
            shapeClone.name = $"{profile.Name} Shape";

            var shadingClone = Object.Instantiate(baseSettings.shading);
            shadingClone.name = $"{profile.Name} Shading";

            OceanSettings oceanClone = null;
            if (shadingClone.oceanSettings != null)
            {
                oceanClone = Object.Instantiate(shadingClone.oceanSettings);
                oceanClone.name = $"{profile.Name} Ocean";
                shadingClone.oceanSettings = oceanClone;
            }

            settingsClone.shape = shapeClone;
            settingsClone.shading = shadingClone;

            ApplyProfile(shapeClone, shadingClone, oceanClone, profile);

            return new RuntimeBodyAssets
            {
                Settings = settingsClone,
                Shape = shapeClone,
                Shading = shadingClone,
                OceanSettings = oceanClone,
            };
        }

        public void Destroy()
        {
            DestroyObject(OceanSettings);
            DestroyObject(Shading);
            DestroyObject(Shape);
            DestroyObject(Settings);

            OceanSettings = null;
            Shading = null;
            Shape = null;
            Settings = null;
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        private static void ApplyProfile(
            CelestialBodyShape shape,
            CelestialBodyShading shading,
            OceanSettings oceanSettings,
            PlanetProfile profile)
        {
            shape.randomize = false;
            shape.seed = profile.Seed;
            shape.perturbVertices = profile.PerturbStrength > 0.001f;
            shape.perturbStrength = profile.IsGasGiant
                ? Mathf.Min(profile.PerturbStrength, 0.12f)
                : Mathf.Min(profile.PerturbStrength, 0.30f);

            if (shape is EarthShape earthShape)
            {
                float mountainStrength = profile.IsGasGiant
                    ? Mathf.Min(profile.MountainStrength, 0.12f)
                    : Mathf.Min(profile.MountainStrength, 0.65f);

                earthShape.oceanFloorDepth = profile.OceanDepth;
                earthShape.oceanDepthMultiplier = profile.HasOcean ? 5f : 2f;
                earthShape.oceanFloorSmoothing = profile.IsGasGiant ? 0.9f : 0.6f;
                earthShape.mountainBlend = Mathf.Lerp(1.45f, 0.95f, mountainStrength);
                earthShape.ridgeNoise.elevation = Mathf.Lerp(1.25f, 5.6f, mountainStrength);
                earthShape.ridgeNoise.power = Mathf.Lerp(1.2f, 2.0f, mountainStrength);
                earthShape.ridgeNoise.peakSmoothing = Mathf.Lerp(1.15f, 0.85f, mountainStrength);
                earthShape.ridgeNoise.scale = profile.IsGasGiant ? 0.85f : earthShape.ridgeNoise.scale;
            }

            shading.randomize = false;
            shading.seed = profile.Seed;
            shading.hasOcean = profile.HasOcean;
            shading.oceanLevel = profile.HasOcean ? 1f : 0f;

            if (shading is EarthShading earthShading)
            {
                earthShading.customizedCols = new EarthShading.EarthColours
                {
                    shoreColLow = profile.TerrainPalette.Low,
                    shoreColHigh = profile.TerrainPalette.Mid,
                    flatColLowA = profile.TerrainPalette.Low,
                    flatColHighA = profile.TerrainPalette.Mid,
                    flatColLowB = profile.TerrainPalette.Mid,
                    flatColHighB = profile.TerrainPalette.High,
                    steepLow = profile.TerrainPalette.High,
                    steepHigh = profile.TerrainPalette.Peak,
                };
            }

            if (oceanSettings != null)
            {
                oceanSettings.colA = profile.OceanColors.Shallow;
                oceanSettings.colB = profile.OceanColors.Deep;
                oceanSettings.smoothness = Mathf.Clamp01(profile.TerrainSmoothness + 0.35f);
            }
        }
    }

    public static class CelestialBodyPlanetBuilder
    {
        private const string EarthSettingsResource = "CelestialBodies/Earth/Earth";

        public static GameObject Build(PlanetProfile profile)
        {
            var baseSettings = Resources.Load<CelestialBodySettings>(EarthSettingsResource);
            if (baseSettings == null)
            {
                Debug.LogError($"CelestialBodyPlanetBuilder: could not load CelestialBodySettings from Resources/{EarthSettingsResource}");
                return null;
            }

            RuntimeBodyAssets runtimeAssets = RuntimeBodyAssets.Create(baseSettings, profile);
            if (runtimeAssets == null || runtimeAssets.Settings == null)
            {
                return null;
            }

            var planet = new GameObject(profile.Name);
            planet.transform.localScale = Vector3.one * profile.Radius;

            var generator = planet.AddComponent<CelestialBodyGenerator>();
            generator.body = runtimeAssets.Settings;
            generator.resolutionSettings = new CelestialBodyGenerator.ResolutionSettings
            {
                lod0 = 300,
                lod1 = 100,
                lod2 = 50,
                collider = 100,
            };

            var fixup = planet.AddComponent<CelestialBodyTerrainFixup>();
            fixup.Initialize(profile, runtimeAssets);

            return planet;
        }
    }

    public class CelestialBodyTerrainFixup : MonoBehaviour
    {
        private const string OceanObjectName = "Ocean Sphere";
        private PlanetProfile profile;
        private RuntimeBodyAssets runtimeAssets;
        private bool applied;

        internal void Initialize(PlanetProfile planetProfile, RuntimeBodyAssets assets)
        {
            profile = planetProfile;
            runtimeAssets = assets;
        }

        private void LateUpdate()
        {
            if (applied) return;

            var generator = GetComponent<CelestialBodyGenerator>();
            if (generator == null) return;

            var terrain = transform.Find("Terrain Mesh");
            if (terrain == null) return;

            var filter = terrain.GetComponent<MeshFilter>();
            var renderer = terrain.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null) return;

            var mesh = filter.sharedMesh;

            var vertices = mesh.vertices;
            float minR = float.PositiveInfinity;
            float maxR = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float r = vertices[i].magnitude;
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
            }

            float oceanLevel = generator.body != null && generator.body.shading != null && generator.body.shading.hasOcean
                ? generator.GetOceanRadius() / Mathf.Max(generator.BodyScale, 1e-4f)
                : minR - 1f;

            var colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                colors[i] = SampleTerrainColor(vertices[i].magnitude, minR, maxR, oceanLevel);
            }
            mesh.colors = colors;

            var urpShader = Shader.Find("SolarSystemExplorer/PlanetTerrainURP");
            if (urpShader != null)
            {
                var mat = new Material(urpShader);
                mat.SetColor("_Tint", Color.white);
                mat.SetFloat("_Smoothness", profile.TerrainSmoothness);
                mat.SetFloat("_Metallic", 0f);
                renderer.sharedMaterial = mat;
            }
            else
            {
                Debug.LogWarning("CelestialBodyTerrainFixup: PlanetTerrainURP shader not found; planet will render with the imported Built-in RP material (likely magenta under URP).");
            }

            EnsureOceanSphere(generator);
            applied = true;
        }

        private Color SampleTerrainColor(float radius, float minR, float maxR, float oceanLevel)
        {
            if (profile.HasOcean && radius < oceanLevel)
            {
                float oceanT = Mathf.InverseLerp(minR, oceanLevel, radius);
                return Color.Lerp(profile.OceanColors.Deep, profile.OceanColors.Shallow, oceanT);
            }

            float landStart = profile.HasOcean ? oceanLevel : minR;
            float landT = Mathf.InverseLerp(landStart, maxR, radius);
            if (landT < 0.33f)
            {
                return Color.Lerp(profile.TerrainPalette.Low, profile.TerrainPalette.Mid, landT / 0.33f);
            }

            if (landT < 0.75f)
            {
                return Color.Lerp(profile.TerrainPalette.Mid, profile.TerrainPalette.High, (landT - 0.33f) / 0.42f);
            }

            return Color.Lerp(profile.TerrainPalette.High, profile.TerrainPalette.Peak, (landT - 0.75f) / 0.25f);
        }

        private void EnsureOceanSphere(CelestialBodyGenerator generator)
        {
            if (generator.body == null || generator.body.shading == null || !generator.body.shading.hasOcean)
            {
                return;
            }

            float oceanRadius = generator.GetOceanRadius() / Mathf.Max(generator.BodyScale, 1e-4f);
            if (oceanRadius <= 0f)
            {
                return;
            }

            Transform oceanTransform = transform.Find(OceanObjectName);
            GameObject oceanObject;
            if (oceanTransform == null)
            {
                oceanObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                oceanObject.name = OceanObjectName;
                oceanObject.transform.SetParent(transform, false);
                oceanObject.layer = gameObject.layer;

                var collider = oceanObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }
            else
            {
                oceanObject = oceanTransform.gameObject;
            }

            oceanObject.transform.localPosition = Vector3.zero;
            oceanObject.transform.localRotation = Quaternion.identity;
            oceanObject.transform.localScale = Vector3.one * (oceanRadius * 2f);

            var oceanRenderer = oceanObject.GetComponent<MeshRenderer>();
            if (oceanRenderer == null)
            {
                oceanRenderer = oceanObject.AddComponent<MeshRenderer>();
            }

            Shader oceanShader = Shader.Find("SolarSystemExplorer/PlanetOceanURP");
            if (oceanShader == null)
            {
                Debug.LogWarning("CelestialBodyTerrainFixup: PlanetOceanURP shader not found; ocean fallback was not created.");
                return;
            }

            var oceanMaterial = new Material(oceanShader);
            oceanMaterial.SetColor("_DeepColor", profile.OceanColors.Deep);
            oceanMaterial.SetColor("_ShallowColor", profile.OceanColors.Shallow);
            oceanMaterial.SetFloat("_Smoothness", Mathf.Clamp01(profile.TerrainSmoothness + 0.35f));
            oceanMaterial.SetFloat("_Alpha", 0.75f);
            oceanRenderer.sharedMaterial = oceanMaterial;
            oceanRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            oceanRenderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            runtimeAssets?.Destroy();
            runtimeAssets = null;
        }
    }
}
