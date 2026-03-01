using UnityEngine;
using UnityEngine.InputSystem;

namespace SolarSystemExplorer.Runtime
{
    public class PlanetPlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform planet;
        [SerializeField] private Renderer surfaceRenderer;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private FreeFlyCameraController freeFlyCamera;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float moveAcceleration = 18f;
        [SerializeField] private float gravityAcceleration = 24f;
        [SerializeField] private float maxFallSpeed = 60f;

        [Header("Body")]
        [SerializeField] private float feetSurfaceOffset = 0.2f;
        [SerializeField] private float eyeHeight = 1.65f;
        [SerializeField] private Vector3 spawnDirection = Vector3.up;
        [SerializeField] private float orientationSharpness = 14f;
        [SerializeField] private float groundSnapSharpness = 35f;
        [SerializeField] private float maxSpawnSurfaceRadius = 50000f;

        [Header("Camera")]
        [SerializeField] private float lookSensitivity = 0.14f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private bool lockCursorInPlayerMode = true;

        private float surfaceRadius = 2f;
        private Vector3 localPlanetCenter;
        private bool hasLocalPlanetCenter;
        private Collider surfaceCollider;

        private Vector3 planarVelocity;
        private float downwardSpeed;
        private float cameraPitch = 12f;
        private bool freeCamActive;
        private bool initialized;

        private Vector3 previousPlanetPosition;
        private Quaternion previousPlanetRotation;
        private bool hasPlanetHistory;

        public void Initialize(Transform planetTransform, Camera mainCamera, FreeFlyCameraController freeFlyCameraController)
        {
            planet = planetTransform;
            playerCamera = mainCamera;
            freeFlyCamera = freeFlyCameraController;

            RecalculateSurfaceGeometry();
            SpawnOnPlanetSurface();
            SetFreeCamMode(false);
            initialized = true;
        }

        public void SetSurfaceRenderer(Renderer renderer)
        {
            surfaceRenderer = renderer;
            surfaceCollider = surfaceRenderer != null ? surfaceRenderer.GetComponent<Collider>() : null;
            RecalculateSurfaceGeometry();
        }

        private void Start()
        {
            if (!initialized)
            {
                AutoResolveReferences();
                RecalculateSurfaceGeometry();
                SpawnOnPlanetSurface();
                SetFreeCamMode(false);
                initialized = true;
            }
        }

        private void Update()
        {
            AutoResolveReferences();
            if (planet == null)
            {
                return;
            }

            HandleModeToggle();
            if (!freeCamActive && lockCursorInPlayerMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            CarryWithPlanetMotion();

            Vector3 up = GetSurfaceUp();
            if (!freeCamActive)
            {
                HandleLookInput(up, Time.deltaTime);
            }

            SimulateMovement(up, Time.deltaTime);

            if (!freeCamActive)
            {
                UpdateFirstPersonCamera();
            }

            StorePlanetHistory();
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleModeToggle()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                SetFreeCamMode(!freeCamActive);
            }
        }

        private void SetFreeCamMode(bool enabled)
        {
            freeCamActive = enabled;

            if (freeFlyCamera != null)
            {
                freeFlyCamera.enabled = enabled;
            }

            if (enabled)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                if (lockCursorInPlayerMode)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                UpdateFirstPersonCamera();
            }
        }

        private void HandleLookInput(Vector3 up, float deltaTime)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                RotateBody(up, 0f, deltaTime);
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            float yawDelta = delta.x * lookSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch - (delta.y * lookSensitivity), minPitch, maxPitch);

            RotateBody(up, yawDelta, deltaTime);
        }

        private void SimulateMovement(Vector3 up, float deltaTime)
        {
            Vector2 moveInput = ReadMoveInput();
            Vector3 desiredPlanarVelocity = Vector3.zero;

            if (!freeCamActive)
            {
                desiredPlanarVelocity = CalculateDesiredPlanarVelocity(moveInput, up);
            }

            float planarLerp = 1f - Mathf.Exp(-moveAcceleration * deltaTime);
            planarVelocity = Vector3.Lerp(planarVelocity, desiredPlanarVelocity, planarLerp);

            downwardSpeed += gravityAcceleration * deltaTime;
            if (downwardSpeed > maxFallSpeed)
            {
                downwardSpeed = maxFallSpeed;
            }

            transform.position += (planarVelocity * deltaTime) - (up * downwardSpeed * deltaTime);
            SnapFeetToSurface(deltaTime);
        }

        private void RotateBody(Vector3 up, float yawDelta, float deltaTime)
        {
            Vector3 forwardProjected = GetSafeProjectedForward(transform.forward, up);
            Quaternion baseRotation = Quaternion.LookRotation(forwardProjected, up);
            Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, up);
            Quaternion target = yawRotation * baseRotation;

            float rotationLerp = 1f - Mathf.Exp(-orientationSharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationLerp);
        }

        private Vector3 CalculateDesiredPlanarVelocity(Vector2 moveInput, Vector3 up)
        {
            Vector3 referenceForward = transform.forward;
            if (playerCamera != null)
            {
                referenceForward = playerCamera.transform.forward;
            }

            Vector3 forward = GetSafeProjectedForward(referenceForward, up);
            Vector3 right = Vector3.Cross(up, forward).normalized;

            Vector3 desired = (forward * moveInput.y) + (right * moveInput.x);
            if (desired.sqrMagnitude > 1f)
            {
                desired.Normalize();
            }

            return desired * moveSpeed;
        }

        private static Vector2 ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            float horizontal = 0f;
            if (keyboard.aKey.isPressed)
            {
                horizontal -= 1f;
            }
            if (keyboard.dKey.isPressed)
            {
                horizontal += 1f;
            }

            float vertical = 0f;
            if (keyboard.sKey.isPressed)
            {
                vertical -= 1f;
            }
            if (keyboard.wKey.isPressed)
            {
                vertical += 1f;
            }

            return new Vector2(horizontal, vertical);
        }

        private void UpdateFirstPersonCamera()
        {
            if (playerCamera == null)
            {
                return;
            }

            Vector3 up = GetSurfaceUp();
            Vector3 forward = GetSafeProjectedForward(transform.forward, up);
            Vector3 right = Vector3.Cross(up, forward).normalized;

            Quaternion pitchRotation = Quaternion.AngleAxis(cameraPitch, right);
            Vector3 lookDirection = (pitchRotation * forward).normalized;

            playerCamera.transform.position = transform.position + (up * eyeHeight);
            playerCamera.transform.rotation = Quaternion.LookRotation(lookDirection, up);
        }

        private void SpawnOnPlanetSurface()
        {
            if (planet == null)
            {
                return;
            }

            Vector3 center = GetPlanetCenterWorld();
            Vector3 direction = spawnDirection.sqrMagnitude > 0.0001f ? spawnDirection.normalized : Vector3.up;
            float targetDistance = GetTargetSurfaceDistance(center, direction);
            transform.position = center + (direction * targetDistance);
            SnapFeetToSurface(1f);

            Vector3 up = GetSurfaceUp();
            Vector3 forward = GetSafeProjectedForward(Vector3.forward, up);
            transform.rotation = Quaternion.LookRotation(forward, up);

            planarVelocity = Vector3.zero;
            downwardSpeed = 0f;

            previousPlanetPosition = planet.position;
            previousPlanetRotation = planet.rotation;
            hasPlanetHistory = true;

            UpdateFirstPersonCamera();
        }

        private void SnapFeetToSurface(float deltaTime)
        {
            if (planet == null)
            {
                return;
            }

            Vector3 center = GetPlanetCenterWorld();
            Vector3 fromCenter = transform.position - center;
            float distance = fromCenter.magnitude;

            if (distance < 0.0001f)
            {
                float fallbackDistance = GetTargetSurfaceDistance(center, Vector3.up);
                transform.position = center + (Vector3.up * fallbackDistance);
                downwardSpeed = 0f;
                return;
            }

            Vector3 normal = fromCenter / distance;
            float targetDistance = GetTargetSurfaceDistance(center, normal);
            if (distance < targetDistance)
            {
                transform.position = center + (normal * targetDistance);
                downwardSpeed = 0f;
                return;
            }

            float hoverTolerance = Mathf.Max(0.04f, feetSurfaceOffset * 0.25f);
            if (distance > targetDistance + hoverTolerance)
            {
                float snapLerp = 1f - Mathf.Exp(-groundSnapSharpness * Mathf.Max(0.0001f, deltaTime));
                float snappedDistance = Mathf.Lerp(distance, targetDistance, snapLerp);
                transform.position = center + (normal * snappedDistance);
                downwardSpeed = 0f;
            }
        }

        private Vector3 GetSurfaceUp()
        {
            if (planet == null)
            {
                return Vector3.up;
            }

            Vector3 center = GetPlanetCenterWorld();
            Vector3 fromCenter = transform.position - center;
            if (fromCenter.sqrMagnitude < 0.0001f)
            {
                return Vector3.up;
            }

            return fromCenter.normalized;
        }

        private Vector3 GetPlanetCenterWorld()
        {
            if (planet == null)
            {
                return Vector3.zero;
            }

            return hasLocalPlanetCenter ? planet.TransformPoint(localPlanetCenter) : planet.position;
        }

        private void CarryWithPlanetMotion()
        {
            if (planet == null)
            {
                return;
            }

            if (!hasPlanetHistory)
            {
                previousPlanetPosition = planet.position;
                previousPlanetRotation = planet.rotation;
                hasPlanetHistory = true;
                return;
            }

            Vector3 localRelative = Quaternion.Inverse(previousPlanetRotation) * (transform.position - previousPlanetPosition);
            transform.position = planet.position + (planet.rotation * localRelative);
        }

        private void StorePlanetHistory()
        {
            if (planet == null)
            {
                hasPlanetHistory = false;
                return;
            }

            previousPlanetPosition = planet.position;
            previousPlanetRotation = planet.rotation;
            hasPlanetHistory = true;
        }

        private void RecalculateSurfaceGeometry()
        {
            if (planet == null)
            {
                return;
            }

            ResolveSurfaceRenderer();

            float chosenRadius = 0f;
            hasLocalPlanetCenter = false;
            localPlanetCenter = Vector3.zero;

            if (surfaceCollider != null && !ShouldIgnoreSurfaceProbe(surfaceCollider.transform))
            {
                Bounds surfaceBounds = surfaceCollider.bounds;
                chosenRadius = Mathf.Max(0f, Mathf.Max(surfaceBounds.extents.x, Mathf.Max(surfaceBounds.extents.y, surfaceBounds.extents.z)));
                localPlanetCenter = planet.InverseTransformPoint(surfaceBounds.center);
                hasLocalPlanetCenter = true;
            }
            else if (surfaceRenderer != null && !ShouldIgnoreSurfaceProbe(surfaceRenderer.transform))
            {
                chosenRadius = Mathf.Max(0f, Mathf.Max(surfaceRenderer.bounds.extents.x, Mathf.Max(surfaceRenderer.bounds.extents.y, surfaceRenderer.bounds.extents.z)));
                localPlanetCenter = planet.InverseTransformPoint(surfaceRenderer.bounds.center);
                hasLocalPlanetCenter = true;
            }

            if (chosenRadius <= 0.001f)
            {
                Collider[] colliders = planet.GetComponentsInChildren<Collider>(true);
                Collider bestCollider = null;
                float bestScore = float.MaxValue;

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider probe = colliders[i];
                    if (probe == null || ShouldIgnoreSurfaceProbe(probe.transform))
                    {
                        continue;
                    }

                    float score = Vector3.Distance(planet.position, probe.bounds.center);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCollider = probe;
                    }
                }

                if (bestCollider != null)
                {
                    chosenRadius = Mathf.Max(bestCollider.bounds.extents.x, Mathf.Max(bestCollider.bounds.extents.y, bestCollider.bounds.extents.z));
                    localPlanetCenter = planet.InverseTransformPoint(bestCollider.bounds.center);
                    hasLocalPlanetCenter = true;
                }
            }

            if (chosenRadius <= 0.001f)
            {
                chosenRadius = Mathf.Max(planet.lossyScale.x, Mathf.Max(planet.lossyScale.y, planet.lossyScale.z)) * 0.5f;
            }

            surfaceRadius = Mathf.Max(0.5f, chosenRadius);
        }

        private void ResolveSurfaceRenderer()
        {
            if (planet == null)
            {
                return;
            }

            if (surfaceRenderer != null)
            {
                surfaceCollider = surfaceRenderer.GetComponent<Collider>();
                return;
            }

            Renderer[] renderers = planet.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Renderer prioritized = null;
            Renderer nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer probe = renderers[i];
                if (probe == null || ShouldIgnoreSurfaceProbe(probe.transform))
                {
                    continue;
                }

                string probeName = probe.name;
                if (probeName == "LandLayer")
                {
                    prioritized = probe;
                    break;
                }

                if (probeName == "PlanetVisual")
                {
                    prioritized = probe;
                    break;
                }

                float distance = Vector3.Distance(planet.position, probe.bounds.center);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = probe;
                }
            }

            surfaceRenderer = prioritized != null ? prioritized : nearest;
            surfaceCollider = surfaceRenderer != null ? surfaceRenderer.GetComponent<Collider>() : null;
        }

        private float GetTargetSurfaceDistance(Vector3 center, Vector3 direction)
        {
            float fallback = surfaceRadius + feetSurfaceOffset;
            if (planet == null)
            {
                return fallback;
            }

            Vector3 outward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;

            if (surfaceCollider != null)
            {
                float probeRadius = Mathf.Max(surfaceRadius + 64f, 128f);
                Vector3 probeStart = center + (outward * probeRadius);
                Ray inwardRay = new Ray(probeStart, -outward);
                float rayDistance = probeRadius * 2f;
                if (surfaceCollider.Raycast(inwardRay, out RaycastHit hit, rayDistance))
                {
                    float hitRadius = Vector3.Dot(hit.point - center, outward);
                    return Mathf.Max(feetSurfaceOffset + 0.05f, hitRadius + feetSurfaceOffset);
                }
            }

            return fallback;
        }

        private static bool ShouldIgnoreSurfaceProbe(Transform probe)
        {
            string objectName = probe.name;
            return objectName == "AtmosphereShell" || objectName == "WaterLayer";
        }

        private static Vector3 GetSafeProjectedForward(Vector3 referenceForward, Vector3 up)
        {
            Vector3 forward = Vector3.ProjectOnPlane(referenceForward, up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.Cross(up, Vector3.right);
            }
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.Cross(up, Vector3.forward);
            }

            return forward.normalized;
        }

        private void AutoResolveReferences()
        {
            if (planet == null)
            {
                GameObject planetObject = GameObject.Find("Planet");
                if (planetObject != null)
                {
                    planet = planetObject.transform;
                    RecalculateSurfaceGeometry();
                }
            }
            else if (surfaceRenderer == null || surfaceCollider == null)
            {
                ResolveSurfaceRenderer();
                if (surfaceCollider != null)
                {
                    RecalculateSurfaceGeometry();
                }
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (freeFlyCamera == null && playerCamera != null)
            {
                freeFlyCamera = playerCamera.GetComponent<FreeFlyCameraController>();
            }
        }
    }
}
