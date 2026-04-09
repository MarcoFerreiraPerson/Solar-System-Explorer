using UnityEngine;
using UnityEngine.InputSystem;

namespace SolarSystemExplorer.Runtime
{
    public class Planet
    {
        private const float PlanetDiameter = 300f;
        private const float InitialOrbitRadius = 1400f;
        private const float OrbitMinRadius = 700f;
        private const float OrbitMaxRadius = 3600f;
        private const float OrbitMaxSpeed = 600f;
        private const float planetMass = 2200000000f;
        private const float minDistance = 600f;

        private GameObject planet;

        public GameObject getPlanet()
        {
            return planet;
        }

        public float getPlanetDiameter()
        {
            return PlanetDiameter;
        }

        public float getMass()
        {
            return planetMass;
        }

        public float getMinDistance()
        {
            return minDistance;
        }

        public Planet(Transform starTransform, float OrbitAttractorMass, float OrbitGravityConstant)
        {
            planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ConfigurePlanetVisual(planet);

            ScaledNewtonianOrbit orbit = planet.AddComponent<ScaledNewtonianOrbit>();
            ConfigurePlanetOrbit(orbit, starTransform, OrbitAttractorMass, OrbitGravityConstant);

            planet.AddComponent<AxialSpinner>();
            PlanetAppearanceController appearance = planet.AddComponent<PlanetAppearanceController>();
            appearance.BuildPlanetAppearance();
        }

        private static void ConfigurePlanetOrbit(ScaledNewtonianOrbit orbit, Transform starTransform, float OrbitAttractorMass, float OrbitGravityConstant)
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
    }
}

