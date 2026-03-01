using UnityEngine;
using UnityEngine.Rendering;

namespace SolarSystemExplorer.Runtime
{
    public class PlanetAppearanceController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Renderer planetRenderer;
        [SerializeField] private Transform layerAnchor;

        [Header("Surface Generation")]
        [SerializeField] private int textureWidth = 2048;
        [SerializeField] private int textureHeight = 1024;
        [SerializeField] private float continentScale = 2.8f;
        [SerializeField] private float detailScale = 14f;
        [SerializeField] private float ridgeScale = 32f;
        [SerializeField] private float continentThreshold = 0.44f;
        [SerializeField] private float continentBlend = 0.075f;
        [SerializeField] private float detailStrength = 0.18f;
        [SerializeField] private float ridgeStrength = 0.06f;
        [SerializeField] private float normalStrength = 1.6f;

        [Header("Land Relief")]
        [SerializeField] private float landBaseLift = 0.004f;
        [SerializeField] private float landReliefAmplitude = 0.012f;
        [SerializeField] private float landUndersideDepth = 0.006f;
        [SerializeField] private float landLayerScale = 1.0025f;
        [SerializeField] [Range(0f, 1f)] private float landMaskClipStart = 0.42f;
        [SerializeField] [Range(0f, 1f)] private float landMaskClipEnd = 0.56f;

        [Header("Biomes")]
        [SerializeField] private float mountainStart = 0.7f;
        [SerializeField] private float snowLatitudeStart = 0.78f;
        [SerializeField] private Color oceanFloorDeep = new Color(0.02f, 0.09f, 0.2f);
        [SerializeField] private Color oceanFloorShallow = new Color(0.08f, 0.18f, 0.28f);
        [SerializeField] private Color beachColor = new Color(0.67f, 0.59f, 0.43f);
        [SerializeField] private Color grassColor = new Color(0.18f, 0.66f, 0.21f);
        [SerializeField] private Color forestColor = new Color(0.08f, 0.44f, 0.16f);
        [SerializeField] private Color dryLandColor = new Color(0.34f, 0.48f, 0.22f);
        [SerializeField] private Color mountainColor = new Color(0.54f, 0.52f, 0.48f);
        [SerializeField] private Color snowColor = new Color(0.93f, 0.96f, 1f);

        [Header("Surface Material")]
        [SerializeField] private float landSmoothness = 0.3f;
        [SerializeField] private float seabedSmoothness = 0.08f;

        [Header("Water Layer")]
        [SerializeField] private float waterLayerScale = 1.006f;
        [SerializeField] private Color waterColor = new Color(0.07f, 0.3f, 0.58f, 0.42f);
        [SerializeField] private float waterSmoothness = 0.97f;
        [SerializeField] private float waterMetallic = 0.02f;
        [SerializeField] private float waterEmission = 0.04f;

        [Header("Atmosphere Layer")]
        [SerializeField] private float atmosphereScale = 1.08f;
        [SerializeField] private Color atmosphereColor = new Color(0.34f, 0.69f, 1f, 1f);
        [SerializeField] private Color atmosphereSunTint = new Color(0.95f, 0.97f, 1f, 1f);
        [SerializeField] private float atmosphereIntensity = 1.85f;
        [SerializeField] private float atmosphereRimPower = 3.2f;
        [SerializeField] private float atmosphereAlpha = 0.5f;
        [SerializeField] private float atmosphereDayStrength = 1.3f;
        [SerializeField] private float atmosphereNightStrength = 0.07f;
        [SerializeField] private float atmosphereTerminatorSharpness = 2.8f;
        [SerializeField] private float atmosphereNightRimFloor = 0.03f;
        [SerializeField] private float atmosphereNightAlphaFloor = 0.02f;
        [SerializeField] private float atmosphereSunScatterPower = 8f;

        [Header("Lifecycle")]
        [SerializeField] private bool buildOnStart = true;

        private Material runtimeBaseMaterial;
        private Material runtimeLandMaterial;
        private Material runtimeWaterMaterial;
        private Material runtimeAtmosphereMaterial;

        private Texture2D runtimeBaseAlbedoTexture;
        private Texture2D runtimeBaseMaskTexture;
        private Texture2D runtimeLandAlbedoTexture;
        private Texture2D runtimeLandMaskTexture;
        private Texture2D runtimeNormalTexture;
        private Texture2D runtimeWaterAlbedoTexture;

        private Renderer landRenderer;
        private Renderer waterRenderer;
        private Renderer atmosphereRenderer;

        private MeshFilter planetMeshFilter;
        private MeshFilter landMeshFilter;
        private Mesh sourcePlanetMesh;
        private Mesh runtimeLandMesh;

        private float[] cachedHeightField;
        private float[] cachedLandField;
        private int cachedMapWidth;
        private int cachedMapHeight;

        private void Reset()
        {
            ResolveTargets();
        }

        private void Start()
        {
            if (buildOnStart)
            {
                BuildPlanetAppearance();
            }
        }

        private void LateUpdate()
        {
            if (runtimeAtmosphereMaterial == null)
            {
                return;
            }

            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                return;
            }

            Vector3 sunDirection = -sun.transform.forward;
            runtimeAtmosphereMaterial.SetVector("_SunDirection", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
        }

        private void OnDestroy()
        {
            CleanupRuntimeResources();
        }

        [ContextMenu("Build Planet Appearance")]
        public void BuildPlanetAppearance()
        {
            ResolveTargets();
            if (planetRenderer == null)
            {
                return;
            }

            BuildSurfaceTexturesAndBase();
            BuildLandLayer();
            BuildWaterLayer();
            BuildAtmosphereLayer();
        }

        private void ResolveTargets()
        {
            if (planetRenderer == null)
            {
                Transform named = transform.Find("PlanetVisual");
                if (named != null)
                {
                    planetRenderer = named.GetComponent<Renderer>();
                }
            }

            if (planetRenderer == null)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    string n = renderers[i].name;
                    if (n != "LandLayer" && n != "WaterLayer" && n != "AtmosphereShell")
                    {
                        planetRenderer = renderers[i];
                        break;
                    }
                }
            }

            if (layerAnchor == null && planetRenderer != null)
            {
                layerAnchor = planetRenderer.transform;
            }

            if (planetRenderer != null)
            {
                planetMeshFilter = planetRenderer.GetComponent<MeshFilter>();
            }
        }

        private void BuildSurfaceTexturesAndBase()
        {
            Shader surfaceShader = Shader.Find("Universal Render Pipeline/Lit");
            if (surfaceShader == null)
            {
                surfaceShader = Shader.Find("Standard");
            }
            if (surfaceShader == null)
            {
                return;
            }

            int width = Mathf.Max(256, textureWidth);
            int height = Mathf.Max(128, textureHeight);

            DestroyTexture(ref runtimeBaseAlbedoTexture);
            DestroyTexture(ref runtimeBaseMaskTexture);
            DestroyTexture(ref runtimeLandAlbedoTexture);
            DestroyTexture(ref runtimeLandMaskTexture);
            DestroyTexture(ref runtimeNormalTexture);

            runtimeBaseAlbedoTexture = NewTexture2D("Planet_Base_Albedo_Runtime", width, height, false);
            runtimeBaseMaskTexture = NewTexture2D("Planet_Base_Mask_Runtime", width, height, true);
            runtimeLandAlbedoTexture = NewTexture2D("Planet_Land_Albedo_Runtime", width, height, false);
            runtimeLandMaskTexture = NewTexture2D("Planet_Land_Mask_Runtime", width, height, true);
            runtimeNormalTexture = NewTexture2D("Planet_Normal_Runtime", width, height, true);

            cachedHeightField = new float[width * height];
            cachedLandField = new float[width * height];
            cachedMapWidth = width;
            cachedMapHeight = height;

            const float twoPi = Mathf.PI * 2f;
            const float halfPi = Mathf.PI * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float latitude = Mathf.Lerp(-halfPi, halfPi, v);
                float cosLat = Mathf.Cos(latitude);
                float sinLat = Mathf.Sin(latitude);

                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float longitude = u * twoPi;

                    Vector3 dir = new Vector3(
                        cosLat * Mathf.Cos(longitude),
                        sinLat,
                        cosLat * Mathf.Sin(longitude));

                    float macro = SampleSphereNoise(dir, continentScale, 11.7f, 43.2f, 78.9f);
                    float detail = SampleSphereNoise(dir, detailScale, 21.3f, 74.1f, 39.8f);
                    float ridges = Mathf.Abs((SampleSphereNoise(dir, ridgeScale, 8.2f, 15.6f, 52.4f) * 2f) - 1f);

                    float elevation = macro + ((detail - 0.5f) * detailStrength) - (ridges * ridgeStrength);
                    float rawLandMask = SmoothStep(continentThreshold - continentBlend, continentThreshold + continentBlend, elevation);

                    int idx = (y * width) + x;
                    cachedHeightField[idx] = elevation;
                    cachedLandField[idx] = rawLandMask;
                }
            }

            Color[] baseAlbedo = new Color[width * height];
            Color[] baseMask = new Color[width * height];
            Color[] landAlbedo = new Color[width * height];
            Color[] landMaskPixels = new Color[width * height];
            Color[] normals = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float latitude01 = y / (float)(height - 1);
                float latitudeAbs = Mathf.Abs((latitude01 * 2f) - 1f);

                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width) + x;
                    float elevation = cachedHeightField[idx];
                    float land = cachedLandField[idx];

                    float humidity = Sample2DNoise(x, y, width, height, 0.012f, 0.041f, 0.73f);
                    float mountain = SmoothStep(mountainStart, 1f, elevation);
                    float snowLat = SmoothStep(snowLatitudeStart, 1f, latitudeAbs);
                    float snowHigh = Mathf.Clamp01(mountain * 1.25f);
                    float snowBlend = Mathf.Clamp01(Mathf.Max(snowLat, snowHigh) * land);

                    float seabedVariation = 0.85f + (Sample2DNoise(x, y, width, height, 0.026f, 0.019f, 0.21f) * 0.3f);
                    float shore = SmoothStep(0.28f, 0.62f, land);
                    Color seabed = Color.Lerp(oceanFloorDeep, oceanFloorShallow, shore) * seabedVariation;

                    Color fertile = Color.Lerp(grassColor, forestColor, humidity);
                    Color dryMix = Color.Lerp(dryLandColor, fertile, humidity);
                    Color coastalLand = Color.Lerp(beachColor, dryMix, SmoothStep(0.03f, 0.18f, land));
                    Color highLand = Color.Lerp(coastalLand, mountainColor, mountain);
                    Color terrain = Color.Lerp(highLand, snowColor, snowBlend);

                    float landPresence = GetLandPresence(land);

                    baseAlbedo[idx] = new Color(seabed.r, seabed.g, seabed.b, 1f);
                    baseMask[idx] = new Color(0f, 1f, 0f, seabedSmoothness);

                    landAlbedo[idx] = new Color(terrain.r, terrain.g, terrain.b, landPresence);
                    float terrainSmoothness = Mathf.Lerp(landSmoothness, landSmoothness * 0.5f, mountain);
                    landMaskPixels[idx] = new Color(0f, 1f, 0f, Mathf.Clamp01(terrainSmoothness));

                    float hL = cachedHeightField[(y * width) + WrapX(x - 1, width)];
                    float hR = cachedHeightField[(y * width) + WrapX(x + 1, width)];
                    float hD = cachedHeightField[(WrapY(y - 1, height) * width) + x];
                    float hU = cachedHeightField[(WrapY(y + 1, height) * width) + x];

                    float dx = (hR - hL) * normalStrength;
                    float dy = (hU - hD) * normalStrength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    normals[idx] = new Color((n.x * 0.5f) + 0.5f, (n.y * 0.5f) + 0.5f, (n.z * 0.5f) + 0.5f, 1f);
                }
            }

            runtimeBaseAlbedoTexture.SetPixels(baseAlbedo);
            runtimeBaseAlbedoTexture.Apply(false, false);

            runtimeBaseMaskTexture.SetPixels(baseMask);
            runtimeBaseMaskTexture.Apply(false, false);

            runtimeLandAlbedoTexture.SetPixels(landAlbedo);
            runtimeLandAlbedoTexture.Apply(false, false);

            runtimeLandMaskTexture.SetPixels(landMaskPixels);
            runtimeLandMaskTexture.Apply(false, false);

            runtimeNormalTexture.SetPixels(normals);
            runtimeNormalTexture.Apply(false, false);

            DestroyMaterial(ref runtimeBaseMaterial);
            runtimeBaseMaterial = new Material(surfaceShader)
            {
                name = "Planet_Base_Runtime"
            };

            SetBaseMap(runtimeBaseMaterial, runtimeBaseAlbedoTexture, Color.white);
            SetMaskMap(runtimeBaseMaterial, runtimeBaseMaskTexture);
            SetNormalMap(runtimeBaseMaterial, runtimeNormalTexture, 0.75f);

            SetIfProperty(runtimeBaseMaterial, "_Metallic", 0f);
            SetIfProperty(runtimeBaseMaterial, "_Smoothness", seabedSmoothness);

            planetRenderer.sharedMaterial = runtimeBaseMaterial;
            planetRenderer.shadowCastingMode = ShadowCastingMode.On;
            planetRenderer.receiveShadows = true;
        }

        private void BuildLandLayer()
        {
            if (layerAnchor == null || planetMeshFilter == null || runtimeLandAlbedoTexture == null)
            {
                return;
            }

            Transform existing = layerAnchor.Find("LandLayer");
            GameObject landObject = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            landObject.name = "LandLayer";
            landObject.transform.SetParent(layerAnchor, false);
            landObject.transform.localPosition = Vector3.zero;
            landObject.transform.localRotation = Quaternion.identity;
            landObject.transform.localScale = Vector3.one * Mathf.Max(1.001f, landLayerScale);

            MeshCollider landCollider = landObject.GetComponent<MeshCollider>();
            if (landCollider == null)
            {
                landCollider = landObject.AddComponent<MeshCollider>();
            }

            Collider[] allColliders = landObject.GetComponents<Collider>();
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider collider = allColliders[i];
                if (collider != null && collider != landCollider)
                {
                    collider.enabled = false;
                    Destroy(collider);
                }
            }

            landMeshFilter = landObject.GetComponent<MeshFilter>();
            landRenderer = landObject.GetComponent<Renderer>();
            if (landMeshFilter == null || landRenderer == null)
            {
                return;
            }

            Mesh source = planetMeshFilter.sharedMesh;
            if (source == null)
            {
                return;
            }

            if (sourcePlanetMesh == null)
            {
                sourcePlanetMesh = Instantiate(source);
                sourcePlanetMesh.name = $"{source.name}_LandSourceRuntime";
            }

            if (runtimeLandMesh != null)
            {
                Destroy(runtimeLandMesh);
                runtimeLandMesh = null;
            }

            runtimeLandMesh = Instantiate(sourcePlanetMesh);
            runtimeLandMesh.name = $"{sourcePlanetMesh.name}_Displaced";

            ApplyLandDisplacement(runtimeLandMesh);
            landMeshFilter.sharedMesh = runtimeLandMesh;
            landCollider.sharedMesh = null;
            landCollider.sharedMesh = runtimeLandMesh;

            Shader surfaceShader = Shader.Find("Universal Render Pipeline/Lit");
            if (surfaceShader == null)
            {
                surfaceShader = Shader.Find("Standard");
            }
            if (surfaceShader == null)
            {
                return;
            }

            DestroyMaterial(ref runtimeLandMaterial);
            runtimeLandMaterial = new Material(surfaceShader)
            {
                name = "Planet_Land_Runtime"
            };

            SetBaseMap(runtimeLandMaterial, runtimeLandAlbedoTexture, Color.white);
            SetMaskMap(runtimeLandMaterial, runtimeLandMaskTexture);
            SetNormalMap(runtimeLandMaterial, runtimeNormalTexture, 1f);

            SetIfProperty(runtimeLandMaterial, "_Metallic", 0f);
            SetIfProperty(runtimeLandMaterial, "_Smoothness", landSmoothness);
            ConfigureAlphaClipMaterial(runtimeLandMaterial, surfaceShader.name, 0.5f);

            landRenderer.sharedMaterial = runtimeLandMaterial;
            landRenderer.shadowCastingMode = ShadowCastingMode.On;
            landRenderer.receiveShadows = true;
        }

        private void BuildWaterLayer()
        {
            if (layerAnchor == null)
            {
                return;
            }

            Transform existing = layerAnchor.Find("WaterLayer");
            GameObject waterObject = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            waterObject.name = "WaterLayer";
            waterObject.transform.SetParent(layerAnchor, false);
            waterObject.transform.localPosition = Vector3.zero;
            waterObject.transform.localRotation = Quaternion.identity;
            waterObject.transform.localScale = Vector3.one * Mathf.Max(1.001f, waterLayerScale);

            Collider waterCollider = waterObject.GetComponent<Collider>();
            if (waterCollider != null)
            {
                Destroy(waterCollider);
            }

            waterRenderer = waterObject.GetComponent<Renderer>();
            if (waterRenderer == null)
            {
                return;
            }

            Shader waterShader = Shader.Find("Universal Render Pipeline/Lit");
            if (waterShader == null)
            {
                waterShader = Shader.Find("Standard");
            }
            if (waterShader == null)
            {
                return;
            }

            DestroyMaterial(ref runtimeWaterMaterial);
            DestroyTexture(ref runtimeWaterAlbedoTexture);

            runtimeWaterMaterial = new Material(waterShader)
            {
                name = "Planet_Water_Runtime"
            };

            if (cachedLandField != null && cachedMapWidth > 0 && cachedMapHeight > 0)
            {
                runtimeWaterAlbedoTexture = BuildWaterMaskTexture(cachedLandField, cachedMapWidth, cachedMapHeight);
                SetBaseMap(runtimeWaterMaterial, runtimeWaterAlbedoTexture, Color.white);
            }
            else
            {
                SetBaseColor(runtimeWaterMaterial, waterColor);
            }

            SetIfProperty(runtimeWaterMaterial, "_Smoothness", waterSmoothness);
            SetIfProperty(runtimeWaterMaterial, "_Metallic", waterMetallic);

            if (runtimeWaterMaterial.HasProperty("_EmissionColor"))
            {
                runtimeWaterMaterial.SetColor("_EmissionColor", waterColor * waterEmission);
                runtimeWaterMaterial.EnableKeyword("_EMISSION");
            }

            ConfigureTransparentMaterial(runtimeWaterMaterial, waterShader.name);

            waterRenderer.sharedMaterial = runtimeWaterMaterial;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
        }

        private void BuildAtmosphereLayer()
        {
            if (layerAnchor == null)
            {
                return;
            }

            Transform existing = layerAnchor.Find("AtmosphereShell");
            GameObject atmosphereObject = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            atmosphereObject.name = "AtmosphereShell";
            atmosphereObject.transform.SetParent(layerAnchor, false);
            atmosphereObject.transform.localPosition = Vector3.zero;
            atmosphereObject.transform.localRotation = Quaternion.identity;
            atmosphereObject.transform.localScale = Vector3.one * Mathf.Max(1.01f, atmosphereScale);

            Collider atmosphereCollider = atmosphereObject.GetComponent<Collider>();
            if (atmosphereCollider != null)
            {
                Destroy(atmosphereCollider);
            }

            atmosphereRenderer = atmosphereObject.GetComponent<Renderer>();
            if (atmosphereRenderer == null)
            {
                return;
            }

            Shader atmosphereShader = Shader.Find("Custom/URP/AtmosphereRim");
            if (atmosphereShader == null)
            {
                atmosphereShader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (atmosphereShader == null)
            {
                return;
            }

            DestroyMaterial(ref runtimeAtmosphereMaterial);
            runtimeAtmosphereMaterial = new Material(atmosphereShader)
            {
                name = "Planet_Atmosphere_Runtime"
            };

            if (runtimeAtmosphereMaterial.HasProperty("_BaseColor"))
            {
                runtimeAtmosphereMaterial.SetColor("_BaseColor", atmosphereColor);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_SunTint"))
            {
                runtimeAtmosphereMaterial.SetColor("_SunTint", atmosphereSunTint);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_Intensity"))
            {
                runtimeAtmosphereMaterial.SetFloat("_Intensity", atmosphereIntensity);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_RimPower"))
            {
                runtimeAtmosphereMaterial.SetFloat("_RimPower", atmosphereRimPower);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_Alpha"))
            {
                runtimeAtmosphereMaterial.SetFloat("_Alpha", atmosphereAlpha);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_DayStrength"))
            {
                runtimeAtmosphereMaterial.SetFloat("_DayStrength", atmosphereDayStrength);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_NightStrength"))
            {
                runtimeAtmosphereMaterial.SetFloat("_NightStrength", atmosphereNightStrength);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_TerminatorSharpness"))
            {
                runtimeAtmosphereMaterial.SetFloat("_TerminatorSharpness", atmosphereTerminatorSharpness);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_NightRimFloor"))
            {
                runtimeAtmosphereMaterial.SetFloat("_NightRimFloor", atmosphereNightRimFloor);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_NightAlphaFloor"))
            {
                runtimeAtmosphereMaterial.SetFloat("_NightAlphaFloor", atmosphereNightAlphaFloor);
            }
            if (runtimeAtmosphereMaterial.HasProperty("_SunScatterPower"))
            {
                runtimeAtmosphereMaterial.SetFloat("_SunScatterPower", atmosphereSunScatterPower);
            }

            if (runtimeAtmosphereMaterial.HasProperty("_SunDirection"))
            {
                Vector3 initialSunDir = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.forward;
                runtimeAtmosphereMaterial.SetVector("_SunDirection", new Vector4(initialSunDir.x, initialSunDir.y, initialSunDir.z, 0f));
            }

            if (runtimeAtmosphereMaterial.shader.name == "Universal Render Pipeline/Unlit")
            {
                runtimeAtmosphereMaterial.SetColor("_BaseColor", new Color(atmosphereColor.r, atmosphereColor.g, atmosphereColor.b, atmosphereAlpha));
                ConfigureTransparentMaterial(runtimeAtmosphereMaterial, runtimeAtmosphereMaterial.shader.name);
            }

            atmosphereRenderer.sharedMaterial = runtimeAtmosphereMaterial;
            atmosphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
            atmosphereRenderer.receiveShadows = false;
        }

        private void ApplyLandDisplacement(Mesh mesh)
        {
            if (mesh == null || cachedLandField == null || cachedHeightField == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;

            if (uvs == null || uvs.Length != vertices.Length)
            {
                return;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 uv = uvs[i];
                float land = SampleFieldBilinear(cachedLandField, cachedMapWidth, cachedMapHeight, uv);
                float landPresence = GetLandPresence(land);
                float elevation = SampleFieldBilinear(cachedHeightField, cachedMapWidth, cachedMapHeight, uv);
                float relief = Mathf.Clamp01((elevation - continentThreshold) / Mathf.Max(0.0001f, 1f - continentThreshold));

                float underside = landUndersideDepth * (1f - relief) * landPresence;
                float displacement = ((landBaseLift * landPresence) + (landReliefAmplitude * relief * landPresence)) - underside;
                Vector3 outward = (normals != null && normals.Length == vertices.Length)
                    ? normals[i].normalized
                    : vertices[i].normalized;

                vertices[i] += outward * displacement;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static Texture2D NewTexture2D(string name, int width, int height, bool linear)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void SetBaseMap(Material material, Texture texture, Color fallbackColor)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", fallbackColor);
                return;
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", fallbackColor);
            }
        }

        private static void SetBaseColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetMaskMap(Material material, Texture texture)
        {
            if (material != null && material.HasProperty("_MaskMap"))
            {
                material.SetTexture("_MaskMap", texture);
                material.EnableKeyword("_MASKMAP");
            }
        }

        private static void SetNormalMap(Material material, Texture texture, float bumpScale)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", texture);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_BumpScale"))
            {
                material.SetFloat("_BumpScale", bumpScale);
            }
        }

        private static void SetIfProperty(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void ConfigureAlphaClipMaterial(Material material, string shaderName, float cutoff)
        {
            if (material == null)
            {
                return;
            }

            if (shaderName == "Universal Render Pipeline/Lit" || shaderName == "Universal Render Pipeline/Unlit")
            {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", cutoff);
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.EnableKeyword("_ALPHATEST_ON");
                return;
            }

            if (shaderName == "Standard")
            {
                material.SetFloat("_Mode", 1f);
                material.SetFloat("_Cutoff", cutoff);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
        }

        private static void ConfigureTransparentMaterial(Material material, string shaderName)
        {
            if (material == null)
            {
                return;
            }

            if (shaderName == "Universal Render Pipeline/Lit" || shaderName == "Universal Render Pipeline/Unlit")
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                return;
            }

            if (shaderName == "Standard")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private Texture2D BuildWaterMaskTexture(float[] landField, int width, int height)
        {
            Texture2D texture = NewTexture2D("Planet_Water_Runtime", width, height, false);

            Color[] pixels = new Color[width * height];
            Color deep = new Color(0.03f, 0.22f, 0.43f, 1f);
            Color shallow = new Color(0.14f, 0.5f, 0.74f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width) + x;
                    float land = Mathf.Clamp01(landField[idx]);
                    float landPresence = GetLandPresence(land);
                    float waterCoverage = Mathf.Clamp01(1f - landPresence);
                    float shore = Mathf.Clamp01(1f - SmoothStep(
                        Mathf.Max(0f, landMaskClipStart - 0.08f),
                        Mathf.Min(1f, landMaskClipStart + 0.02f),
                        land));
                    Color waterTint = Color.Lerp(deep, shallow, shore);
                    float alpha = waterColor.a * waterCoverage;
                    pixels[idx] = new Color(waterTint.r, waterTint.g, waterTint.b, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void CleanupRuntimeResources()
        {
            DestroyMaterial(ref runtimeBaseMaterial);
            DestroyMaterial(ref runtimeLandMaterial);
            DestroyMaterial(ref runtimeWaterMaterial);
            DestroyMaterial(ref runtimeAtmosphereMaterial);

            DestroyTexture(ref runtimeBaseAlbedoTexture);
            DestroyTexture(ref runtimeBaseMaskTexture);
            DestroyTexture(ref runtimeLandAlbedoTexture);
            DestroyTexture(ref runtimeLandMaskTexture);
            DestroyTexture(ref runtimeNormalTexture);
            DestroyTexture(ref runtimeWaterAlbedoTexture);

            if (runtimeLandMesh != null)
            {
                Destroy(runtimeLandMesh);
                runtimeLandMesh = null;
            }

            if (sourcePlanetMesh != null)
            {
                Destroy(sourcePlanetMesh);
                sourcePlanetMesh = null;
            }

            cachedHeightField = null;
            cachedLandField = null;
            cachedMapWidth = 0;
            cachedMapHeight = 0;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }

        private static float SampleSphereNoise(Vector3 dir, float scale, float seedA, float seedB, float seedC)
        {
            float nx = dir.x * scale;
            float ny = dir.y * scale;
            float nz = dir.z * scale;

            float a = Mathf.PerlinNoise(nx + seedA, ny + seedB);
            float b = Mathf.PerlinNoise(ny + seedB, nz + seedC);
            float c = Mathf.PerlinNoise(nz + seedC, nx + seedA);

            return (a + b + c) / 3f;
        }

        private static float Sample2DNoise(int x, int y, int width, int height, float scaleX, float scaleY, float seed)
        {
            float u = (x / (float)width) * scaleX;
            float v = (y / (float)height) * scaleY;
            return Mathf.PerlinNoise(u + seed, v + (seed * 0.5f));
        }

        private static float SampleFieldBilinear(float[] field, int width, int height, Vector2 uv)
        {
            if (field == null || width <= 0 || height <= 0)
            {
                return 0f;
            }

            float wrappedU = uv.x - Mathf.Floor(uv.x);
            float clampedV = Mathf.Clamp01(uv.y);

            float fx = wrappedU * (width - 1);
            float fy = clampedV * (height - 1);

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int x1 = WrapX(x0 + 1, width);
            int y1 = WrapY(y0 + 1, height);

            float tx = fx - x0;
            float ty = fy - y0;

            float v00 = field[(y0 * width) + x0];
            float v10 = field[(y0 * width) + x1];
            float v01 = field[(y1 * width) + x0];
            float v11 = field[(y1 * width) + x1];

            float vx0 = Mathf.Lerp(v00, v10, tx);
            float vx1 = Mathf.Lerp(v01, v11, tx);
            return Mathf.Lerp(vx0, vx1, ty);
        }

        private float GetLandPresence(float landMask)
        {
            float start = Mathf.Clamp01(landMaskClipStart);
            float end = Mathf.Clamp01(landMaskClipEnd);
            if (end <= start)
            {
                end = Mathf.Min(1f, start + 0.01f);
            }

            return SmoothStep(start, end, Mathf.Clamp01(landMask));
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return x < edge0 ? 0f : 1f;
            }

            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - (2f * t));
        }

        private static int WrapX(int x, int width)
        {
            if (width <= 0)
            {
                return 0;
            }

            int value = x % width;
            return value < 0 ? value + width : value;
        }

        private static int WrapY(int y, int height)
        {
            if (height <= 0)
            {
                return 0;
            }

            if (y < 0)
            {
                return 0;
            }
            if (y >= height)
            {
                return height - 1;
            }

            return y;
        }
    }
}
