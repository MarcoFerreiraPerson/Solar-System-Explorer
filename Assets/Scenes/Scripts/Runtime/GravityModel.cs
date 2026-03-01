using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public static class GravityModel
    {
        public static Vector3 ComputeAcceleration(
            Vector3 bodyPosition,
            Vector3 attractorPosition,
            float gravitationalConstant,
            float attractorMass,
            float minDistance)
        {
            Vector3 offset = attractorPosition - bodyPosition;
            float sqrDistance = offset.sqrMagnitude;
            float minSqrDistance = minDistance * minDistance;

            if (sqrDistance < minSqrDistance)
            {
                sqrDistance = minSqrDistance;
            }

            float inverseDistance = 1f / Mathf.Sqrt(sqrDistance);
            Vector3 direction = offset * inverseDistance;
            float accelerationMagnitude = (gravitationalConstant * attractorMass) / sqrDistance;

            return direction * accelerationMagnitude;
        }
    }
}
