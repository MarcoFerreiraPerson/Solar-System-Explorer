using UnityEngine;
using UnityEngine.InputSystem;

namespace SolarSystemExplorer.Runtime
{
    public class Player
    {
        [SerializeField] private float movementSpeed = 8f;
        private const float GroundClearance = 0.35f;
        private const float GroundProbeHeight = 4f;
        private const float GravityAcceleration = 26f;
        private const float JumpSpeed = 12f;
        private const float GroundSnapSharpness = 20f;
        private const float GroundContactPadding = 0.75f;
        private const float CameraHeight = 1f;
        private const float CameraPositionSmoothness = 18f;
        private const float CameraRotationSmoothness = 22f;
        private const float ColliderHeight = 2f;
        private const float ColliderRadius = 0.5f;
        private const float PenetrationBuffer = 0.02f;
        private const int TerrainQueryMask = ~(1 << 2);

        private float mouseSensitivity = 0.2f;
        private float pitch = 0f;

        private Planet currentPlanet;
        private Vector3 planetCenterPos;
        private Vector3 lastPlanetCenterPos;
        private Quaternion lastPlanetRotation;
        private Camera mainCamera;
        private CapsuleCollider playerCollider;
        private float verticalVelocity;
        private bool isGrounded;
        private bool cameraInitialized;

        private GameObject player;
        public Planet CurrentPlanet => currentPlanet;

        public GameObject getPlayer()
        {
            return player;
        }

        public Player(Planet planet)
        {
            player = new GameObject("Player");
            // Put the player on Ignore Raycast (layer 2) so terrain queries
            // exclude the player's own collision body.
            player.layer = 2;
            playerCollider = player.AddComponent<CapsuleCollider>();
            playerCollider.height = ColliderHeight;
            playerCollider.radius = ColliderRadius;
            playerCollider.center = Vector3.zero;

            mainCamera = Camera.main;
            mainCamera.transform.SetParent(null);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SetCurrentPlanet(planet);

            Transform planetTransform = currentPlanet.Transform;
            Vector3 playerStartingPos = planetTransform.position + planetTransform.up * (currentPlanet.getPlanetDiameter() / 2f + GetStandingHeight() + 1.5f);
            player.transform.position = playerStartingPos;
            player.transform.rotation = Quaternion.FromToRotation(Vector3.up, planetTransform.up);
            SnapCameraToPlayer();
        }

        public void SetCurrentPlanet(Planet planet)
        {
            currentPlanet = planet;
            if (currentPlanet == null)
            {
                return;
            }

            lastPlanetCenterPos = currentPlanet.Transform.position;
            lastPlanetRotation = currentPlanet.Transform.rotation;
        }

        public void LandOnPlanet(Planet planet, Vector3 desiredPosition)
        {
            if (planet == null)
            {
                return;
            }

            SetCurrentPlanet(planet);
            SnapToSurface(planet.Transform, planet.getPlanetDiameter(), desiredPosition);
            lastPlanetCenterPos = planet.Transform.position;
            lastPlanetRotation = planet.Transform.rotation;
        }

        public void updatePlayer()
        {
            if (currentPlanet == null || currentPlanet.getPlanet() == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            Vector3 playerPos = player.transform.position;
            Transform planetTransform = currentPlanet.Transform;
            planetCenterPos = planetTransform.position;

            Vector3 planetDelta = planetCenterPos - lastPlanetCenterPos;
            playerPos += planetDelta;

            Quaternion currentRotation = planetTransform.rotation;

            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastPlanetRotation);

            Vector3 offset = playerPos - planetCenterPos;
            offset = deltaRotation * offset;

            playerPos = planetCenterPos + offset;

            player.transform.rotation = deltaRotation * player.transform.rotation;

            Vector3 radialNormal = (playerPos - planetCenterPos).normalized;
            RotateCamera(radialNormal);
            // Move relative to the radial-up so hills don't reduce stride length
            playerPos = MovePlayer(playerPos, radialNormal);

            radialNormal = (playerPos - planetCenterPos).normalized;

            bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            Vector3 targetUp = radialNormal;
            float standingHeight = GetStandingHeight();

            if (TryFindGround(playerPos, radialNormal, planetCenterPos, currentPlanet.getPlanetDiameter(), out RaycastHit hit))
            {
                targetUp = Vector3.Slerp(radialNormal, hit.normal, 0.25f).normalized;
                float distanceToGround = Vector3.Dot(playerPos - hit.point, radialNormal);
                bool closeToGround = distanceToGround <= standingHeight + GroundContactPadding;

                if (closeToGround && verticalVelocity <= 0f)
                {
                    isGrounded = true;
                    Vector3 groundedPosition = hit.point + radialNormal * standingHeight;

                    if (jumpPressed)
                    {
                        playerPos = groundedPosition;
                        verticalVelocity = JumpSpeed;
                        isGrounded = false;
                    }
                    else
                    {
                        verticalVelocity = 0f;
                        float snapT = 1f - Mathf.Exp(-GroundSnapSharpness * dt);
                        playerPos = Vector3.Lerp(playerPos, groundedPosition, snapT);
                    }
                }
                else
                {
                    isGrounded = false;
                }
            }
            else
            {
                isGrounded = false;
            }

            if (!isGrounded)
            {
                verticalVelocity -= GravityAcceleration * dt;
                playerPos += radialNormal * (verticalVelocity * dt);
            }

            Quaternion targetRotation = Quaternion.FromToRotation(player.transform.up, targetUp) * player.transform.rotation;
            ResolveTerrainPenetration(ref playerPos, targetRotation, targetUp);
            player.transform.rotation = targetRotation;
            player.transform.position = playerPos;
            UpdateCameraTransform(dt);

            lastPlanetCenterPos = planetCenterPos;
            lastPlanetRotation = currentRotation;
        }

        private void SnapToSurface(Transform planetTransform, float planetDiameter, Vector3 desiredPosition)
        {
            Vector3 planetPos = planetTransform.position;
            Vector3 radialNormal = (desiredPosition - planetPos).normalized;
            float standingHeight = GetStandingHeight();
            Vector3 snappedPosition;

            if (TryFindGround(desiredPosition, radialNormal, planetPos, planetDiameter, out RaycastHit hit))
            {
                snappedPosition = hit.point + hit.normal * standingHeight;
            }
            else
            {
                float surfaceHeight = planetDiameter / 2f + standingHeight;
                snappedPosition = planetPos + radialNormal * surfaceHeight;
            }

            player.transform.rotation = Quaternion.FromToRotation(player.transform.up, radialNormal) * player.transform.rotation;
            player.transform.position = snappedPosition;
            verticalVelocity = 0f;
            isGrounded = true;
            SnapCameraToPlayer();
        }

        private bool TryFindGround(Vector3 desiredPosition, Vector3 radialNormal, Vector3 planetPos, float planetDiameter, out RaycastHit hit)
        {
            Vector3 localProbeStart = desiredPosition + radialNormal * GroundProbeHeight;
            float localProbeDistance = GroundProbeHeight + GetStandingHeight() + 2f;
            if (Physics.Raycast(localProbeStart, -radialNormal, out hit, localProbeDistance, TerrainQueryMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            Vector3 fallbackRayStart = planetPos + radialNormal * planetDiameter;
            return Physics.Raycast(fallbackRayStart, -radialNormal, out hit, planetDiameter * 2f, TerrainQueryMask, QueryTriggerInteraction.Ignore);
        }

        private float GetStandingHeight()
        {
            return playerCollider.height * 0.5f + GroundClearance;
        }

        private void ResolveTerrainPenetration(ref Vector3 playerPos, Quaternion playerRotation, Vector3 up)
        {
            GetCapsuleWorldPoints(playerPos, up, out Vector3 pointA, out Vector3 pointB);
            Collider[] overlaps = Physics.OverlapCapsule(pointA, pointB, playerCollider.radius, TerrainQueryMask, QueryTriggerInteraction.Ignore);

            Vector3 totalCorrection = Vector3.zero;
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap == null)
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                    playerCollider, playerPos, playerRotation,
                    overlap, overlap.transform.position, overlap.transform.rotation,
                    out Vector3 direction, out float distance))
                {
                    totalCorrection += direction * (distance + PenetrationBuffer);
                }
            }

            if (totalCorrection != Vector3.zero)
            {
                playerPos += totalCorrection;
            }
        }

        private void GetCapsuleWorldPoints(Vector3 position, Vector3 up, out Vector3 pointA, out Vector3 pointB)
        {
            float halfSegment = Mathf.Max(0f, (playerCollider.height * 0.5f) - playerCollider.radius);
            Vector3 offset = up.normalized * halfSegment;
            pointA = position + offset;
            pointB = position - offset;
        }

        private Vector3 MovePlayer(Vector3 playerPos, Vector3 surfaceNormal)
        {
            float dt = Time.deltaTime;

            float inputX = 0f;
            float inputY = 0f;

            if (Keyboard.current.aKey.isPressed) inputX -= 1f;
            if (Keyboard.current.dKey.isPressed) inputX += 1f;

            if (Keyboard.current.wKey.isPressed) inputY += 1f;
            if (Keyboard.current.sKey.isPressed) inputY -= 1f;

            Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, surfaceNormal).normalized;
            Vector3 right = Vector3.Cross(surfaceNormal, forward).normalized;

            Vector3 move = (forward * inputY + right * inputX) * movementSpeed * dt;

            return playerPos + move;
        }

        private void RotateCamera(Vector3 surfaceNormal)
        {
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 delta = Mouse.current.delta.ReadValue();

            float mouseX = delta.x;
            float mouseY = delta.y;

            float yaw = mouseX * mouseSensitivity;
            float pitchDelta = mouseY * mouseSensitivity;

            player.transform.Rotate(Vector3.up, yaw, Space.Self);

            pitch -= pitchDelta;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        private void UpdateCameraTransform(float dt)
        {
            Vector3 desiredPosition = player.transform.position + player.transform.up * CameraHeight;
            Quaternion desiredRotation = Quaternion.AngleAxis(pitch, player.transform.right) * player.transform.rotation;

            if (!cameraInitialized)
            {
                mainCamera.transform.position = desiredPosition;
                mainCamera.transform.rotation = desiredRotation;
                cameraInitialized = true;
                return;
            }

            float positionT = 1f - Mathf.Exp(-CameraPositionSmoothness * dt);
            float rotationT = 1f - Mathf.Exp(-CameraRotationSmoothness * dt);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPosition, positionT);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, desiredRotation, rotationT);
        }

        private void SnapCameraToPlayer()
        {
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.transform.position = player.transform.position + player.transform.up * CameraHeight;
            mainCamera.transform.rotation = Quaternion.AngleAxis(pitch, player.transform.right) * player.transform.rotation;
            cameraInitialized = true;
        }
    }
}
