using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using TMPro;

namespace SolarSystemExplorer.Runtime
{
    public class SolarSystemBootstrap
    {
        private const float StarDiameter = 600f;
        private const float SunScale = 6f;
        private const float OrbitGravityConstant = 1f;
        private const float OrbitAttractorMass = 2200000f;
        private const string ObjectiveTextName = "Objective Text";
        private const string ObjectiveTextValue = "Explore the solar system!";

        private readonly List<Planet> planets = new List<Planet>();
        private Planet activePlanet;
        private Planet earthStartPlanet;
        private GameObject star;
        private Player player;
        private SpaceShip spaceShip;
        private StarDirectionalLightController lightController;

        public void initialize(Transform t)
        {
            DisableLegacy2DLighting();
            ConfigureEnvironmentLighting();
            star = CreateStar();
            CreatePlanets();
            earthStartPlanet = FindEarthPlanet();
            activePlanet = earthStartPlanet;

            player = new Player(earthStartPlanet);
            spaceShip = new SpaceShip(earthStartPlanet, player);
            Light sunLight = CreateDirectionalSunLight();
            ConfigureSunBloom(t, star);
            CreateObjectiveText();
            lightController = CreateLightController(t, star.transform, activePlanet.Transform, sunLight);
        }

        public void systemUpdate()
        {
            spaceShip.HandleBoarding();
            spaceShip.HandleMouseLook();
            spaceShip.spaceshipUpdate(planets);
            spaceShip.UpdateCamera();

            if (!spaceShip.IsBoarded)
            {
                player.updatePlayer();
            }

            activePlanet = ResolveActivePlanet();
            if (lightController != null && activePlanet != null)
            {
                lightController.SetPlanet(activePlanet.Transform);
            }
        }

        private void CreatePlanets()
        {
            planets.Clear();
            for (int i = 0; i < PlanetCatalog.All.Count; i++)
            {
                planets.Add(new Planet(PlanetCatalog.All[i], star.transform, OrbitAttractorMass, OrbitGravityConstant));
            }
        }

        private Planet FindEarthPlanet()
        {
            for (int i = 0; i < planets.Count; i++)
            {
                if (planets[i].Profile.Name == PlanetCatalog.Earth.Name)
                {
                    return planets[i];
                }
            }

            return planets.Count > 0 ? planets[0] : null;
        }

        private Planet ResolveActivePlanet()
        {
            if (spaceShip != null && spaceShip.IsBoarded && spaceShip.CurrentPlanet != null)
            {
                return spaceShip.CurrentPlanet;
            }

            if (player != null && player.CurrentPlanet != null)
            {
                return player.CurrentPlanet;
            }

            return earthStartPlanet;
        }

        private void DisableLegacy2DLighting()
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
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0f;

            if (RenderSettings.skybox != null)
            {
                DynamicGI.UpdateEnvironment();
            }
        }

        private static GameObject CreateStar()
        {
            GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "Star";
            star.transform.position = Vector3.zero;
            star.transform.localScale = Vector3.one * StarDiameter * SunScale;

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

            return star;
        }

        private static void CreateObjectiveText()
        {
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
            {
                return;
            }

            Transform existing = canvasObject.transform.Find(ObjectiveTextName);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(ObjectiveTextName);
            textObject.layer = canvasObject.layer;
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = textObject.AddComponent<RectTransform>();
            }

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(360f, 40f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = ObjectiveTextValue;
            text.color = Color.white;
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.TopRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
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

        private static Material CreateLitMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return shader == null ? null : new Material(shader);
        }

        private static void ConfigureSunBloom(Transform root, GameObject sun)
        {
            if (root == null || sun == null)
            {
                return;
            }

            var bloom = root.GetComponent<SunBloomEffect>();
            if (bloom == null)
            {
                bloom = root.gameObject.AddComponent<SunBloomEffect>();
            }

            bloom.Initialize(sun);
        }

        private Light CreateDirectionalSunLight()
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

            lightComponent.gameObject.SetActive(true);
            lightComponent.enabled = true;
        }

        private StarDirectionalLightController CreateLightController(Transform gameObject, Transform star, Transform planet, Light sun)
        {
            StarDirectionalLightController controller = gameObject.gameObject.AddComponent<StarDirectionalLightController>();
            controller.Initialize(star, planet, sun);
            return controller;
        }
    }
}
