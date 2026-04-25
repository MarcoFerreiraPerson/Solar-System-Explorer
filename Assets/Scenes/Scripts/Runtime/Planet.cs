using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public class Planet
    {
        private const float PlanetDiameter = 300f;
        private const float InitialOrbitRadius = 2000f;
        private const float OrbitMinRadius = 700f;
        private const float OrbitMaxRadius = 3600f;
        private const float OrbitMaxSpeed = 600f;
        private const float planetMass = 2200000000f;
        private const float minDistance = 600f;

        // Resources-relative path to the CelestialBodySettings asset for Earth.
        // Humble Abode is the imported Earth preset; we copied it into Resources at this path.
        private const string EarthSettingsResource = "CelestialBodies/Earth/Earth";

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
            planet = CelestialBodyPlanetBuilder.Build(PlanetDiameter / 2f, EarthSettingsResource);
            if (planet == null)
            {
                // Fall back to a plain sphere so the scene still boots if the asset is missing
                planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                planet.name = "Planet (fallback)";
                planet.transform.localScale = Vector3.one * PlanetDiameter;
            }

            planet.transform.position = new Vector3(InitialOrbitRadius, 0f, 0f);

            ScaledNewtonianOrbit orbit = planet.AddComponent<ScaledNewtonianOrbit>();
            ConfigurePlanetOrbit(orbit, starTransform, OrbitAttractorMass, OrbitGravityConstant);

            planet.AddComponent<AxialSpinner>();
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
    }
}
