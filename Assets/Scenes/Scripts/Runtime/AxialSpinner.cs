using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public class AxialSpinner : MonoBehaviour
    {
        [SerializeField] private Vector3 localAxis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 10f;

        private Vector3 normalizedAxis = Vector3.up;

        private void Awake()
        {
            CacheAxis();
        }

        private void OnValidate()
        {
            CacheAxis();
        }

        private void Update()
        {
            transform.Rotate(normalizedAxis, degreesPerSecond * Time.deltaTime, Space.Self);
        }

        private void CacheAxis()
        {
            normalizedAxis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        }
    }
}
