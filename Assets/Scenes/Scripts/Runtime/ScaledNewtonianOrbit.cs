using UnityEngine;

namespace SolarSystemExplorer.Runtime
{
    public class ScaledNewtonianOrbit : MonoBehaviour
    {
        [Header("Body References")]
        [SerializeField] private Transform attractor;

        [Header("Scaled Gravity")]
        [SerializeField] private float gravitationalConstant = 1f;
        [SerializeField] private float attractorMass = 22000000f;
        [SerializeField] private float minOrbitRadius = 700f;
        [SerializeField] private float maxOrbitRadius = 3600f;

        [Header("Initial Conditions")]
        [SerializeField] private bool autoInitializeCircularVelocity = true;
        [SerializeField] private Vector3 initialVelocity = Vector3.zero;
        [SerializeField] private Vector3 orbitPlaneNormal = Vector3.up;

        [Header("Stability")]
        [SerializeField] private float maxSpeed = 600f;
        [SerializeField] private bool renderInterpolation = true;

        private Vector3 velocity;
        private Vector3 previousSimulatedPosition;
        private Vector3 simulatedPosition;
        private bool hasInitialized;
        private bool simulationReady;

        public void Initialize(Transform starTransform)
        {
            attractor = starTransform;
            ConfigureInitialState();
        }

        public void ConfigureOrbit(
            Transform starTransform,
            float scaledGravityConstant,
            float scaledAttractorMass,
            float minimumOrbitRadius,
            float maximumOrbitRadius,
            float maximumAllowedSpeed,
            bool initializeCircularVelocity,
            Vector3 planeNormal)
        {
            attractor = starTransform;
            gravitationalConstant = Mathf.Max(0.0001f, scaledGravityConstant);
            attractorMass = Mathf.Max(0.0001f, scaledAttractorMass);
            minOrbitRadius = Mathf.Max(0.1f, minimumOrbitRadius);
            maxOrbitRadius = Mathf.Max(minOrbitRadius + 0.1f, maximumOrbitRadius);
            maxSpeed = Mathf.Max(0.1f, maximumAllowedSpeed);
            autoInitializeCircularVelocity = initializeCircularVelocity;
            orbitPlaneNormal = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;

            ConfigureInitialState();
        }

        private void Start()
        {
            if (!hasInitialized)
            {
                ConfigureInitialState();
            }
        }

        private void ConfigureInitialState()
        {
            if (attractor == null)
            {
                return;
            }

            Vector3 starPosition = attractor.position;
            Vector3 radial = transform.position - starPosition;

            if (radial.sqrMagnitude < 0.0001f)
            {
                radial = Vector3.right * Mathf.Max(minOrbitRadius, 10f);
                transform.position = starPosition + radial;
            }

            float radius = radial.magnitude;
            if (radius < minOrbitRadius || radius > maxOrbitRadius)
            {
                radius = Mathf.Clamp(radius, minOrbitRadius, maxOrbitRadius);
                radial = radial.normalized * radius;
                transform.position = starPosition + radial;
            }

            if (autoInitializeCircularVelocity)
            {
                Vector3 planeNormal = orbitPlaneNormal.sqrMagnitude > 0.0001f ? orbitPlaneNormal.normalized : Vector3.up;

                if (Mathf.Abs(Vector3.Dot(planeNormal, radial.normalized)) > 0.99f)
                {
                    planeNormal = Vector3.forward;
                }

                Vector3 tangent = Vector3.Cross(planeNormal, radial).normalized;
                float circularSpeed = Mathf.Sqrt((gravitationalConstant * attractorMass) / radius);
                velocity = tangent * circularSpeed;
            }
            else
            {
                velocity = initialVelocity;
            }

            simulatedPosition = transform.position;
            previousSimulatedPosition = simulatedPosition;
            simulationReady = true;
            hasInitialized = true;
        }

        private void FixedUpdate()
        {
            if (attractor == null)
            {
                return;
            }

            if (!simulationReady)
            {
                simulatedPosition = transform.position;
                previousSimulatedPosition = simulatedPosition;
                simulationReady = true;
            }

            Vector3 starPosition = attractor.position;
            Vector3 acceleration = GravityModel.ComputeAcceleration(
                simulatedPosition,
                starPosition,
                gravitationalConstant,
                attractorMass,
                minOrbitRadius);

            float dt = Time.fixedDeltaTime;

            velocity += acceleration * dt;
            if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                velocity = velocity.normalized * maxSpeed;
            }

            Vector3 nextPosition = simulatedPosition + velocity * dt;
            Vector3 radial = nextPosition - starPosition;
            float distance = radial.magnitude;

            if (distance > 0.0001f)
            {
                float clampedDistance = Mathf.Clamp(distance, minOrbitRadius, maxOrbitRadius);
                if (!Mathf.Approximately(clampedDistance, distance))
                {
                    Vector3 radialDirection = radial / distance;
                    nextPosition = starPosition + radialDirection * clampedDistance;

                    float radialVelocity = Vector3.Dot(velocity, radialDirection);
                    bool movingFurtherOut = distance > maxOrbitRadius && radialVelocity > 0f;
                    bool movingFurtherIn = distance < minOrbitRadius && radialVelocity < 0f;

                    if (movingFurtherOut || movingFurtherIn)
                    {
                        velocity -= radialDirection * radialVelocity;
                    }
                }
            }

            previousSimulatedPosition = simulatedPosition;
            simulatedPosition = nextPosition;

            if (!renderInterpolation)
            {
                transform.position = simulatedPosition;
            }
        }

        private void Update()
        {
            if (!simulationReady || !renderInterpolation)
            {
                return;
            }

            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            transform.position = Vector3.Lerp(previousSimulatedPosition, simulatedPosition, alpha);
        }
    }
}
