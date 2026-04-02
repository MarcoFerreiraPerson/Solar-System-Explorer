using UnityEngine;
using UnityEngine.Rendering;

namespace SolarSystemExplorer.Runtime
{
    public class SolarSystemBootstrap: MonoBehaviour
    {
        private const float StarDiameter = 600f;
        private const float OrbitGravityConstant = 1f;
        private const float OrbitAttractorMass = 2200000f;

        private Planet planet;
        private GameObject star;
        private Player player;
        private SpaceShip spaceShip;

        void Awake()
        {
            DisableLegacy2DLighting();
            ConfigureEnvironmentLighting();

            star = CreateStar();
            planet = new Planet(star.transform, OrbitAttractorMass, OrbitGravityConstant);
            player = new Player(planet);
            spaceShip = new SpaceShip(planet, player);
            Light sunLight = CreateDirectionalSunLight();
            CreateLightController(transform, star.transform, planet.getPlanet().transform, sunLight);
        }

        void Update()
        {
            spaceShip.HandleBoarding();
            spaceShip.HandleMouseLook();
            spaceShip.spaceshipUpdate(planet, OrbitGravityConstant);
            player.updatePlayer(planet, OrbitGravityConstant);
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
            RenderSettings.ambientLight = new Color(0.025f, 0.025f, 0.035f);
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

            return star;
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
        }

        private void CreateLightController(Transform gameObject, Transform star, Transform planet, Light sun)
        {
            StarDirectionalLightController controller = gameObject.gameObject.AddComponent<StarDirectionalLightController>();
            controller.Initialize(star, planet, sun);
        }
    }
}
