using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public class Planet
    {
        private const float OrbitMaxSpeed = 600f;

        private readonly GameObject planet;

        public PlanetProfile Profile { get; }
        public Transform Transform => planet != null ? planet.transform : null;
        public float Radius => Profile.ScaledRadius;

        public GameObject getPlanet()
        {
            return planet;
        }

        public float getPlanetDiameter()
        {
            return Profile.ScaledDiameter;
        }

        public Planet(PlanetProfile profile, Transform starTransform, float orbitAttractorMass, float orbitGravityConstant)
        {
            Profile = profile;
            planet = CelestialBodyPlanetBuilder.Build(profile);
            if (planet == null)
            {
                planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                planet.name = $"{profile.Name} (fallback)";
                planet.transform.localScale = Vector3.one * profile.ScaledDiameter;
            }

            planet.name = profile.Name;
            planet.transform.position = CalculateStartingPosition(profile);

            var orbit = planet.AddComponent<ScaledNewtonianOrbit>();
            ConfigurePlanetOrbit(orbit, profile, starTransform, orbitAttractorMass, orbitGravityConstant);

            var spinner = planet.AddComponent<AxialSpinner>();
            spinner.Configure(Vector3.up, profile.AxialSpinDegPerSec * PlanetCatalog.AxialRotationScale);
        }

        public float GetSurfaceDistance(Vector3 position)
        {
            if (Transform == null)
            {
                return float.PositiveInfinity;
            }

            return Vector3.Distance(position, Transform.position) - Radius;
        }

        private static Vector3 CalculateStartingPosition(PlanetProfile profile)
        {
            Quaternion orbitRotation = Quaternion.AngleAxis(profile.InitialOrbitAngleDeg, Vector3.up);
            return orbitRotation * Vector3.right * profile.ScaledOrbitRadius;
        }

        private static void ConfigurePlanetOrbit(
            ScaledNewtonianOrbit orbit,
            PlanetProfile profile,
            Transform starTransform,
            float orbitAttractorMass,
            float orbitGravityConstant)
        {
            if (orbit == null || starTransform == null)
            {
                return;
            }

            float minOrbitRadius = Mathf.Max(200f, profile.ScaledOrbitRadius - profile.ScaledOrbitBand);
            float maxOrbitRadius = profile.ScaledOrbitRadius + profile.ScaledOrbitBand;
            orbit.ConfigureOrbit(
                starTransform,
                orbitGravityConstant,
                orbitAttractorMass,
                minOrbitRadius,
                maxOrbitRadius,
                OrbitMaxSpeed,
                true,
                Vector3.up);
        }
    }
}
