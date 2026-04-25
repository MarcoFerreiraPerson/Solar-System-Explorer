using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    /// <summary>
    /// Adapter between our code-driven Planet setup and the imported asset-driven
    /// CelestialBodyGenerator. Creates an empty GameObject, attaches the generator,
    /// loads the Humble Abode (Earth) settings from Resources, and schedules a
    /// post-generation pass that swaps the Built-in-RP terrain material for a
    /// URP-compatible one and writes biome colors into the mesh.
    ///
    /// The generator runs its setup in Start(), so the mesh and collider do not
    /// exist on the frame Build() returns. Player/ship ground-finding raycasts
    /// handle the missing-collider case with a fallback.
    /// </summary>
    public static class CelestialBodyPlanetBuilder
    {
        public static GameObject Build(float radius, string settingsResourceName)
        {
            var settings = Resources.Load<CelestialBodySettings>(settingsResourceName);
            if (settings == null)
            {
                Debug.LogError($"CelestialBodyPlanetBuilder: could not load CelestialBodySettings from Resources/{settingsResourceName}");
                return null;
            }

            var planet = new GameObject("Planet");
            planet.transform.localScale = Vector3.one * radius;

            var generator = planet.AddComponent<CelestialBodyGenerator>();
            generator.body = settings;
            generator.resolutionSettings = new CelestialBodyGenerator.ResolutionSettings
            {
                lod0 = 300,
                lod1 = 100,
                lod2 = 50,
                collider = 100,
            };

            // URP fix: the imported Earth shader is Built-in RP and renders magenta under URP.
            // This helper waits for the generator's Start() to populate the "Terrain Mesh"
            // child, then swaps in a URP shader and paints vertex colors from the height data.
            planet.AddComponent<CelestialBodyTerrainFixup>();

            return planet;
        }
    }

    /// <summary>
    /// Runs after CelestialBodyGenerator.Start() has created the "Terrain Mesh"
    /// child. Replaces the Built-in-RP terrain material with a URP shader and
    /// writes per-vertex biome colors based on height above the reference sphere.
    /// </summary>
    public class CelestialBodyTerrainFixup : MonoBehaviour
    {
        private const string OceanObjectName = "Ocean Sphere";
        private bool applied;

        void LateUpdate()
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

            // Compute height range over the actual vertices so the color ramp is calibrated
            // even if the shape asset's heightMinMax is unavailable.
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
                float r = vertices[i].magnitude;

                // Biome ramp: deep blue → shallow blue → sand → green → brown → grey/white
                Color c;
                if (r < oceanLevel)
                {
                    // Underwater: deep blue to teal
                    float t = (r - minR) / Mathf.Max(1e-4f, oceanLevel - minR);
                    c = Color.Lerp(new Color(0.03f, 0.08f, 0.25f), new Color(0.12f, 0.35f, 0.55f), t);
                }
                else
                {
                    float tLand = (r - oceanLevel) / Mathf.Max(1e-4f, maxR - oceanLevel);
                    if (tLand < 0.05f)
                        c = new Color(0.80f, 0.75f, 0.55f); // beach
                    else if (tLand < 0.40f)
                        c = Color.Lerp(new Color(0.20f, 0.45f, 0.15f), new Color(0.35f, 0.50f, 0.18f), (tLand - 0.05f) / 0.35f);
                    else if (tLand < 0.75f)
                        c = Color.Lerp(new Color(0.35f, 0.30f, 0.18f), new Color(0.45f, 0.40f, 0.32f), (tLand - 0.40f) / 0.35f);
                    else
                        c = Color.Lerp(new Color(0.55f, 0.55f, 0.58f), Color.white, (tLand - 0.75f) / 0.25f);
                }

                colors[i] = c;
            }
            mesh.colors = colors;

            // Swap the material for a URP-compatible one
            var urpShader = Shader.Find("SolarSystemExplorer/PlanetTerrainURP");
            if (urpShader != null)
            {
                var mat = new Material(urpShader);
                mat.SetColor("_Tint", Color.white);
                mat.SetFloat("_Smoothness", 0.15f);
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
            OceanSettings oceanSettings = generator.body.shading.oceanSettings;
            Color deepColor = oceanSettings != null ? oceanSettings.colB : new Color(0.05f, 0.18f, 0.35f, 1f);
            Color shallowColor = oceanSettings != null ? oceanSettings.colA : new Color(0.22f, 0.7f, 0.78f, 1f);
            float smoothness = oceanSettings != null ? oceanSettings.smoothness : 0.9f;

            oceanMaterial.SetColor("_DeepColor", deepColor);
            oceanMaterial.SetColor("_ShallowColor", shallowColor);
            oceanMaterial.SetFloat("_Smoothness", smoothness);
            oceanMaterial.SetFloat("_Alpha", 0.75f);
            oceanRenderer.sharedMaterial = oceanMaterial;
            oceanRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            oceanRenderer.receiveShadows = false;
        }
    }
}
