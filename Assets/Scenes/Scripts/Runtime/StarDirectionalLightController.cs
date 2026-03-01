using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public class StarDirectionalLightController : MonoBehaviour
    {
        [SerializeField] private Transform star;
        [SerializeField] private Transform planet;
        [SerializeField] private Light directionalLight;
        [SerializeField] private bool invertLightForward = false;

        public void Initialize(Transform starTransform, Transform planetTransform, Light sunLight)
        {
            star = starTransform;
            planet = planetTransform;
            directionalLight = sunLight;
        }

        private void LateUpdate()
        {
            if (star == null || planet == null || directionalLight == null)
            {
                return;
            }

            Vector3 starToPlanet = planet.position - star.position;
            if (starToPlanet.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 forward = starToPlanet.normalized;
            if (invertLightForward)
            {
                forward = -forward;
            }

            Transform lightTransform = directionalLight.transform;
            lightTransform.position = star.position;
            lightTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
