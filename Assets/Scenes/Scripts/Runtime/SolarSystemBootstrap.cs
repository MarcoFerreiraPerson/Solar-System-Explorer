using UnityEngine;
using UnityEngine.Rendering;

namespace SolarSystemExplorer.Runtime
{
    public static class SolarSystemBootstrap
    {
        private const string BootstrapObjectName = "Solar System MVP Runtime";
        private const float StarDiameter = 600f;
        private const float PlanetDiameter = 300f;
        private const float InitialOrbitRadius = 1400f;
        private const float OrbitGravityConstant = 1f;
        private const float OrbitAttractorMass = 22000000f;
        private const float OrbitMinRadius = 700f;
        private const float OrbitMaxRadius = 3600f;
        private const float OrbitMaxSpeed = 600f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildRuntimeSlice()
        {
            if (GameObject.Find(BootstrapObjectName) != null)
            {
                return;
            }

            GameObject bootstrapObject = new GameObject(BootstrapObjectName);

            DisableLegacy2DLighting();
            ConfigureEnvironmentLighting();

            GameObject star = FindOrCreateStar();
            GameObject planet = FindOrCreatePlanet(star.transform);
            Light sun = FindOrCreateDirectionalSunLight();
            CreateLightController(bootstrapObject.transform, star.transform, planet.transform, sun);
            Camera mainCamera = ConfigureMainCamera();
            ConfigurePlayer(planet, mainCamera);
        }

        private static void DisableLegacy2DLighting()
        {
            GameObject globalLight2D = GameObject.Find("Global Light 2D");
            if (globalLight2D != null)
            {
                globalLight2D.SetActive(false);
            }
        }

        private static void ConfigureEnvironmentLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.035f);
            RenderSettings.reflectionIntensity = 0f;

            if (RenderSettings.skybox != null)
            {
                DynamicGI.UpdateEnvironment();
            }
        }

        private static GameObject FindOrCreateStar()
        {
            GameObject existing = GameObject.Find("Star");
            if (existing != null)
            {
                ConfigureStar(existing);
                return existing;
            }

            return CreateStar();
        }

        private static GameObject CreateStar()
        {
            GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ConfigureStar(star);

            return star;
        }

        private static void ConfigureStar(GameObject star)
        {
            star.name = "Star";
            star.transform.position = Vector3.zero;
            star.transform.localScale = Vector3.one * StarDiameter;

            Renderer renderer = star.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Material starMaterial = CreateUnlitMaterial();
                if (starMaterial != null)
                {
                    starMaterial.SetColor("_BaseColor", new Color(1f, 0.85f, 0.45f));
                    renderer.sharedMaterial = starMaterial;
                }
            }
        }

        private static GameObject FindOrCreatePlanet(Transform starTransform)
        {
            GameObject existing = GameObject.Find("Planet");
            if (existing != null)
            {
                ConfigurePlanetVisual(existing);

                ScaledNewtonianOrbit existingOrbit = existing.GetComponent<ScaledNewtonianOrbit>();
                if (existingOrbit == null)
                {
                    existingOrbit = existing.AddComponent<ScaledNewtonianOrbit>();
                }
                ConfigurePlanetOrbit(existingOrbit, starTransform);

                if (existing.GetComponent<AxialSpinner>() == null)
                {
                    existing.AddComponent<AxialSpinner>();
                }

                if (existing.GetComponent<PlanetAppearanceController>() == null)
                {
                    existing.AddComponent<PlanetAppearanceController>();
                }

                PlanetAppearanceController existingAppearance = existing.GetComponent<PlanetAppearanceController>();
                if (existingAppearance != null)
                {
                    existingAppearance.BuildPlanetAppearance();
                }

                return existing;
            }

            return CreatePlanet(starTransform);
        }

        private static GameObject CreatePlanet(Transform starTransform)
        {
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ConfigurePlanetVisual(planet);

            ScaledNewtonianOrbit orbit = planet.AddComponent<ScaledNewtonianOrbit>();
            ConfigurePlanetOrbit(orbit, starTransform);

            planet.AddComponent<AxialSpinner>();
            PlanetAppearanceController appearance = planet.AddComponent<PlanetAppearanceController>();
            appearance.BuildPlanetAppearance();

            return planet;
        }

        private static void ConfigurePlanetVisual(GameObject planet)
        {
            planet.name = "Planet";
            planet.transform.position = new Vector3(InitialOrbitRadius, 0f, 0f);
            planet.transform.localScale = Vector3.one * PlanetDiameter;

            Renderer renderer = planet.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material planetMaterial = CreateLitMaterial();
                if (planetMaterial != null)
                {
                    planetMaterial.SetColor("_BaseColor", new Color(0.18f, 0.45f, 0.9f));
                    planetMaterial.SetFloat("_Smoothness", 0.35f);
                    renderer.sharedMaterial = planetMaterial;
                }
            }
        }

        private static Material CreateLitMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return shader == null ? null : new Material(shader);
        }

        private static Material CreateUnlitMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            return shader == null ? null : new Material(shader);
        }

        private static void ConfigurePlanetOrbit(ScaledNewtonianOrbit orbit, Transform starTransform)
        {
            if (orbit == null || starTransform == null)
            {
                return;
            }

            orbit.ConfigureOrbit(
                starTransform,
                OrbitGravityConstant,
                OrbitAttractorMass,
                OrbitMinRadius,
                OrbitMaxRadius,
                OrbitMaxSpeed,
                true,
                Vector3.up);
        }

        private static Light FindOrCreateDirectionalSunLight()
        {
            Light existingSun = RenderSettings.sun;
            if (existingSun != null && existingSun.type == LightType.Directional)
            {
                ConfigureSunLight(existingSun);
                return existingSun;
            }

            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    ConfigureSunLight(lights[i]);
                    RenderSettings.sun = lights[i];
                    return lights[i];
                }
            }

            return CreateDirectionalSunLight();
        }

        private static Light CreateDirectionalSunLight()
        {
            GameObject lightObject = new GameObject("Solar Directional Light");
            Light lightComponent = lightObject.AddComponent<Light>();
            ConfigureSunLight(lightComponent);

            RenderSettings.sun = lightComponent;

            return lightComponent;
        }

        private static void ConfigureSunLight(Light lightComponent)
        {
            lightComponent.type = LightType.Directional;
            lightComponent.color = new Color(1f, 0.97f, 0.9f);
            lightComponent.intensity = 1.3f;
            lightComponent.shadows = LightShadows.Soft;
            lightComponent.shadowStrength = 0.85f;
        }

        private static void CreateLightController(Transform parent, Transform star, Transform planet, Light sun)
        {
            StarDirectionalLightController controller = parent.gameObject.AddComponent<StarDirectionalLightController>();
            controller.Initialize(star, planet, sun);
        }

        private static Camera ConfigureMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 60f;
            mainCamera.nearClipPlane = 0.3f;
            mainCamera.farClipPlane = 50000f;

            Transform cameraTransform = mainCamera.transform;
            cameraTransform.position = new Vector3(-2800f, 900f, -2800f);
            cameraTransform.LookAt(Vector3.zero);

            if (mainCamera.GetComponent<FreeFlyCameraController>() == null)
            {
                mainCamera.gameObject.AddComponent<FreeFlyCameraController>();
            }

            return mainCamera;
        }

        private static void ConfigurePlayer(GameObject planet, Camera mainCamera)
        {
            if (planet == null || mainCamera == null)
            {
                return;
            }

            GameObject player = FindOrCreatePlayer();
            player.transform.SetParent(null, true);

            PlanetPlayerController controller = player.GetComponent<PlanetPlayerController>();
            if (controller == null)
            {
                controller = player.AddComponent<PlanetPlayerController>();
            }

            Renderer preferredSurface = FindPreferredPlanetSurfaceRenderer(planet.transform);
            if (preferredSurface != null)
            {
                controller.SetSurfaceRenderer(preferredSurface);
            }

            FreeFlyCameraController freeFly = mainCamera.GetComponent<FreeFlyCameraController>();
            if (freeFly == null)
            {
                freeFly = mainCamera.gameObject.AddComponent<FreeFlyCameraController>();
            }

            freeFly.ConfigureLookMode(false, true);
            freeFly.enabled = false;
            controller.Initialize(planet.transform, mainCamera, freeFly);
        }

        private static Renderer FindPreferredPlanetSurfaceRenderer(Transform planetTransform)
        {
            if (planetTransform == null)
            {
                return null;
            }

            Transform landLayer = planetTransform.Find("LandLayer");
            if (landLayer != null)
            {
                Renderer landRenderer = landLayer.GetComponent<Renderer>();
                if (landRenderer != null)
                {
                    return landRenderer;
                }
            }
            else
            {
                Renderer[] allRenderers = planetTransform.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    Renderer probe = allRenderers[i];
                    if (probe != null && probe.name == "LandLayer")
                    {
                        return probe;
                    }
                }
            }

            Transform named = planetTransform.Find("PlanetVisual");
            if (named != null)
            {
                Renderer namedRenderer = named.GetComponent<Renderer>();
                if (namedRenderer != null)
                {
                    return namedRenderer;
                }
            }

            Renderer[] renderers = planetTransform.GetComponentsInChildren<Renderer>(true);
            Renderer nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (candidate == null || candidate.name == "AtmosphereShell" || candidate.name == "WaterLayer")
                {
                    continue;
                }

                float distance = Vector3.Distance(planetTransform.position, candidate.bounds.center);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static GameObject FindOrCreatePlayer()
        {
            GameObject existing = GameObject.Find("Player");
            if (existing != null)
            {
                EnsurePlayerVisual(existing);
                return existing;
            }

            GameObject player = new GameObject("Player");
            EnsurePlayerVisual(player);
            return player;
        }

        private static void EnsurePlayerVisual(GameObject playerRoot)
        {
            Transform body = playerRoot.transform.Find("Body");
            GameObject bodyObject = body != null ? body.gameObject : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyObject.name = "Body";
            bodyObject.transform.SetParent(playerRoot.transform, false);
            bodyObject.transform.localPosition = Vector3.up;
            bodyObject.transform.localRotation = Quaternion.identity;
            bodyObject.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            Collider collider = bodyObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            Renderer renderer = bodyObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material bodyMaterial = CreateLitMaterial();
                if (bodyMaterial != null)
                {
                    bodyMaterial.SetColor("_BaseColor", new Color(0.82f, 0.86f, 0.95f));
                    renderer.sharedMaterial = bodyMaterial;
                }

                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.enabled = false;
            }
        }
    }
}
