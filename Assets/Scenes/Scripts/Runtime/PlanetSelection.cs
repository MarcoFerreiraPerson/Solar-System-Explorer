using System.Collections.Generic;
using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public static class PlanetSelection
    {
        public static Planet SelectBySurfaceDistance(
            IReadOnlyList<Planet> planets,
            Vector3 position,
            Planet currentPlanet = null,
            float switchHysteresis = 0f)
        {
            if (planets == null || planets.Count == 0)
            {
                return currentPlanet;
            }

            Planet nearestPlanet = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < planets.Count; i++)
            {
                Planet candidate = planets[i];
                if (candidate == null || candidate.getPlanet() == null)
                {
                    continue;
                }

                float candidateDistance = candidate.GetSurfaceDistance(position);
                if (candidateDistance < nearestDistance)
                {
                    nearestDistance = candidateDistance;
                    nearestPlanet = candidate;
                }
            }

            if (nearestPlanet == null)
            {
                return currentPlanet;
            }

            if (currentPlanet == null || currentPlanet.getPlanet() == null)
            {
                return nearestPlanet;
            }

            float currentDistance = currentPlanet.GetSurfaceDistance(position);
            return currentDistance <= nearestDistance + switchHysteresis ? currentPlanet : nearestPlanet;
        }
    }
}
