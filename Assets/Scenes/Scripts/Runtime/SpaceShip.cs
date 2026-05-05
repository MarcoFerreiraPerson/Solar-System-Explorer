using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SolarSystemExplorer.Runtime
{
    public class SpaceShip
    {
        private const float PlanetSwitchHysteresis = 25f;
        private const float BoostMultiplier = 3f;
        private const float InitialSpawnClearance = 10f * PlanetCatalog.SystemScale;
        private const float LandingSurfaceClearance = 3f;
        private const int TerrainQueryMask = ~(1 << 2);

        private float thrustPower = 60f * PlanetCatalog.SystemScale;
        private float maxSpeed = 150f * PlanetCatalog.SystemScale;
        private float fakeGravity = 80000f * PlanetCatalog.SystemScale * PlanetCatalog.SystemScale;
        private float mouseSensitivity = 2f;

        private int state = 0;

        private bool isBoarded = false;
        private float boardDistance = 5f;
        private Player player;

        private GameObject spaceShip;
        private Collider[] shipColliders;
        private Camera mainCamera;
        private Vector3 velocity;

        private Planet currentPlanet;

        private float landedThreshold = 3f * PlanetCatalog.SystemScale;

        private float launchLockTimer = 0f;
        private float launchLockDuration = 2.5f;
        private float shipSurfaceClearance = 0.35f;
        private float launchVelocity = 20f * PlanetCatalog.SystemScale;

        private Vector3 lastPlanetPos;
        private Quaternion lastPlanetRot;
        private bool hasLandingAnchor;
        private Vector3 localLandingPosition;
        private Vector3 localLandingSurfacePoint;
        private Vector3 localLandingSurfaceNormal = Vector3.up;
        private Quaternion localLandingRotation = Quaternion.identity;
        private Planet landingAnchorPlanet;
        private bool waitingForLandingSurface;
        private bool landedLaunchArmed;

        private float camFollowDistance = 48f;
        private float camFollowHeight = 12f;

        public bool IsBoarded => isBoarded;
        public bool IsLanded => state == 0;
        public Planet CurrentPlanet => currentPlanet;

        public SpaceShip(Planet planet, Player player)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Spaceship");
            this.player = player;
            spaceShip = GameObject.Instantiate(prefab);
            shipColliders = spaceShip.GetComponentsInChildren<Collider>();
            // Put the ship (and all its children) on Ignore Raycast (layer 2) so
            // landing/collision raycasts don't hit the ship's own colliders before
            // the planet terrain.
            SetLayerRecursively(spaceShip, 2);
            mainCamera = Camera.main;
            currentPlanet = planet;

            Transform pt = currentPlanet.Transform;
            float radius = planet.getPlanetDiameter() / 2f;
            Vector3 up = pt.up;

            spaceShip.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
            spaceShip.transform.position = pt.position + up * (radius + GetShipSurfaceOffset(up) + InitialSpawnClearance);

            lastPlanetPos = pt.position;
            lastPlanetRot = pt.rotation;
            ClearLandingAnchor();
            waitingForLandingSurface = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public int getState() => state;

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public void UpdateCamera()
        {
            if (!isBoarded) return;

            Vector3 desiredPos = spaceShip.transform.position
                + spaceShip.transform.forward * camFollowDistance
                + spaceShip.transform.up * camFollowHeight;

            mainCamera.transform.position = desiredPos;
            mainCamera.transform.rotation = Quaternion.LookRotation(
                -spaceShip.transform.forward,
                spaceShip.transform.up);
        }

        public void spaceshipUpdate(IReadOnlyList<Planet> planets)
        {
            if (!IsLanded || currentPlanet == null)
            {
                SetCurrentPlanet(PlanetSelection.SelectBySurfaceDistance(planets, spaceShip.transform.position, currentPlanet, PlanetSwitchHysteresis));
            }

            if (IsLanded && hasLandingAnchor)
            {
                ApplyLandingAnchor();
            }

            UpdateState();

            if (state == 0)
            {
                updateLanded();
            }
            else
            {
                updateFlying(planets);
            }
        }

        private void UpdateState()
        {
            if (currentPlanet == null || currentPlanet.getPlanet() == null)
            {
                state = 1;
                ClearLandingAnchor();
                return;
            }

            if (launchLockTimer > 0f)
            {
                launchLockTimer -= Time.deltaTime;
                state = 1;
                return;
            }

            if (waitingForLandingSurface)
            {
                state = 0;
                return;
            }

            if (state == 0 && hasLandingAnchor && landingAnchorPlanet == currentPlanet)
            {
                state = 0;
                return;
            }

            if (!TryGetCurrentSurfaceAltitude(out float altitude))
            {
                state = 1;
                ClearLandingAnchor();
                return;
            }

            if (altitude <= landedThreshold)
                state = 0;
            else
            {
                state = 1;
                ClearLandingAnchor();
            }
        }

        private void updateLanded()
        {
            if (currentPlanet == null || currentPlanet.getPlanet() == null)
            {
                return;
            }

            if (Keyboard.current != null && !Keyboard.current.spaceKey.isPressed)
            {
                landedLaunchArmed = true;
            }

            if (Keyboard.current != null && isBoarded && landedLaunchArmed && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                state = 1;
                ClearLandingAnchor();
                landedLaunchArmed = false;
                launchLockTimer = launchLockDuration;
                velocity = spaceShip.transform.up * launchVelocity;

                lastPlanetPos = currentPlanet.Transform.position;
                lastPlanetRot = currentPlanet.Transform.rotation;
                return;
            }

            Transform pt = currentPlanet.Transform;
            if (!hasLandingAnchor || landingAnchorPlanet != currentPlanet || waitingForLandingSurface)
            {
                if (!TrySnapToLandingSurfaceAndCaptureAnchor(waitingForLandingSurface))
                {
                    velocity = Vector3.zero;
                    lastPlanetPos = pt.position;
                    lastPlanetRot = pt.rotation;
                    return;
                }
            }

            ApplyLandingAnchor();

            velocity = Vector3.zero;
            lastPlanetPos = pt.position;
            lastPlanetRot = pt.rotation;
        }

        private void updateFlying(IReadOnlyList<Planet> planets)
        {
            if (currentPlanet == null || currentPlanet.getPlanet() == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            float speedMultiplier = 1f;

            Vector3 thrust = Vector3.zero;
            bool thrusting = false;

            if (isBoarded && Keyboard.current != null)
            {
                bool isBoosting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                speedMultiplier = isBoosting ? BoostMultiplier : 1f;

                if (Keyboard.current.wKey.isPressed) { thrust -= spaceShip.transform.forward; thrusting = true; }
                if (Keyboard.current.sKey.isPressed) { thrust += spaceShip.transform.forward; thrusting = true; }
                if (Keyboard.current.aKey.isPressed) { thrust += spaceShip.transform.right; thrusting = true; }
                if (Keyboard.current.dKey.isPressed) { thrust -= spaceShip.transform.right; thrusting = true; }
                if (Keyboard.current.spaceKey.isPressed) { thrust += spaceShip.transform.up; thrusting = true; }
                if (Keyboard.current.leftCtrlKey.isPressed) { thrust -= spaceShip.transform.up; thrusting = true; }

                if (thrusting)
                    velocity += thrust.normalized * thrustPower * speedMultiplier * dt;
            }

            Vector3 toPlanet = currentPlanet.Transform.position - spaceShip.transform.position;
            float dist = Mathf.Max(toPlanet.magnitude, 1f);
            float gravScale = fakeGravity / (dist * dist);
            velocity += toPlanet.normalized * gravScale * dt;

            float currentMaxSpeed = maxSpeed * speedMultiplier;
            if (velocity.sqrMagnitude > currentMaxSpeed * currentMaxSpeed)
                velocity = velocity.normalized * currentMaxSpeed;

            spaceShip.transform.position += velocity * dt;

            SetCurrentPlanet(PlanetSelection.SelectBySurfaceDistance(planets, spaceShip.transform.position, currentPlanet, PlanetSwitchHysteresis));
            if (currentPlanet == null || currentPlanet.getPlanet() == null)
            {
                return;
            }

            TryResolveFlyingSurfaceContact();
        }

        public void HandleMouseLook()
        {
            if (!isBoarded || Mouse.current == null) return;

            Vector2 delta = Mouse.current.delta.ReadValue();

            if (state == 0)
            {
                if (currentPlanet == null || currentPlanet.getPlanet() == null)
                {
                    return;
                }

                if (waitingForLandingSurface)
                {
                    return;
                }

                Vector3 surfaceNormal = GetRadialNormal(currentPlanet.Transform);
                spaceShip.transform.Rotate(surfaceNormal, delta.x * mouseSensitivity, Space.World);
                CaptureLandingRotation();
            }
            else
            {
                spaceShip.transform.Rotate(Vector3.up, delta.x * mouseSensitivity, Space.Self);
                spaceShip.transform.Rotate(Vector3.right, delta.y * mouseSensitivity, Space.Self);
            }
        }

        public void HandleBoarding()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (!Keyboard.current.fKey.wasPressedThisFrame) return;

            float dist = Vector3.Distance(
                player.getPlayer().transform.position,
                spaceShip.transform.position);

            if (!isBoarded && dist < boardDistance)
            {
                isBoarded = true;

                mainCamera.transform.SetParent(null);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (isBoarded && state == 0)
            {
                isBoarded = false;

                if (currentPlanet == null || currentPlanet.getPlanet() == null)
                {
                    return;
                }

                Vector3 planetCenter = currentPlanet.Transform.position;
                Vector3 radialNormal = (spaceShip.transform.position - planetCenter).normalized;
                Vector3 lateralOffset = Vector3.ProjectOnPlane(spaceShip.transform.right, radialNormal).normalized;
                if (lateralOffset.sqrMagnitude < 0.0001f)
                {
                    lateralOffset = Vector3.Cross(radialNormal, spaceShip.transform.forward).normalized;
                }

                Vector3 exitPosition = spaceShip.transform.position + lateralOffset * 4f + radialNormal * 2f;
                player.LandOnPlanet(currentPlanet, exitPosition);

                mainCamera.transform.SetParent(null);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = true;
            }
        }

        private void SetCurrentPlanet(Planet planet)
        {
            if (planet == currentPlanet)
            {
                return;
            }

            currentPlanet = planet;
            ClearLandingAnchor();
        }

        private bool TrySnapToLandingSurfaceAndCaptureAnchor(bool requireTerrainHit)
        {
            if (currentPlanet == null || currentPlanet.getPlanet() == null)
            {
                return false;
            }

            Transform pt = currentPlanet.Transform;
            Vector3 radialNormal = GetRadialNormal(pt);
            float planetDiameter = currentPlanet.getPlanetDiameter();

            if (TryFindCurrentPlanetSurface(radialNormal, out RaycastHit landHit))
            {
                Vector3 outwardNormal = GetOutwardSurfaceNormal(landHit.normal, radialNormal);
                spaceShip.transform.rotation = Quaternion.FromToRotation(spaceShip.transform.up, outwardNormal) * spaceShip.transform.rotation;
                spaceShip.transform.position = landHit.point + outwardNormal * GetLandingSurfaceOffset(outwardNormal);
                velocity = Vector3.zero;
                launchLockTimer = 0f;
                landedLaunchArmed = false;
                CaptureLandingAnchor(landHit.point, outwardNormal);
                return true;
            }

            spaceShip.transform.rotation = Quaternion.FromToRotation(spaceShip.transform.up, radialNormal) * spaceShip.transform.rotation;
            float extraClearance = requireTerrainHit ? InitialSpawnClearance : 0f;
            float snapRadius = planetDiameter / 2f + GetShipSurfaceOffset(radialNormal) + extraClearance;
            spaceShip.transform.position = pt.position + radialNormal * snapRadius;

            if (requireTerrainHit)
            {
                return false;
            }

            velocity = Vector3.zero;
            launchLockTimer = 0f;
            landedLaunchArmed = false;
            CaptureLandingAnchor(pt.position + radialNormal * (planetDiameter / 2f), radialNormal);
            return true;
        }

        private bool TryResolveFlyingSurfaceContact()
        {
            if (launchLockTimer > 0f || currentPlanet == null || currentPlanet.Transform == null)
            {
                return false;
            }

            Transform pt = currentPlanet.Transform;
            Vector3 toShip = spaceShip.transform.position - pt.position;
            if (toShip.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 radialNormal = toShip.normalized;
            if (TryFindCurrentPlanetSurface(radialNormal, out RaycastHit surfaceHit))
            {
                Vector3 outwardNormal = GetOutwardSurfaceNormal(surfaceHit.normal, radialNormal);
                float surfaceOffset = GetLandingSurfaceOffset(outwardNormal);
                float altitude = Vector3.Dot(spaceShip.transform.position - surfaceHit.point, outwardNormal) - surfaceOffset;
                if (altitude > landedThreshold)
                {
                    return false;
                }

                spaceShip.transform.rotation = Quaternion.FromToRotation(spaceShip.transform.up, outwardNormal) * spaceShip.transform.rotation;
                surfaceOffset = GetLandingSurfaceOffset(outwardNormal);
                spaceShip.transform.position = surfaceHit.point + outwardNormal * surfaceOffset;
                velocity = Vector3.zero;
                launchLockTimer = 0f;
                landedLaunchArmed = false;
                state = 0;
                CaptureLandingAnchor(surfaceHit.point, outwardNormal);
                return true;
            }

            float minDist = currentPlanet.getPlanetDiameter() / 2f + GetShipSurfaceOffset(radialNormal);
            if (toShip.magnitude > minDist + landedThreshold)
            {
                return false;
            }

            spaceShip.transform.rotation = Quaternion.FromToRotation(spaceShip.transform.up, radialNormal) * spaceShip.transform.rotation;
            spaceShip.transform.position = pt.position + radialNormal * minDist;
            velocity = Vector3.zero;
            launchLockTimer = 0f;
            landedLaunchArmed = false;
            state = 0;
            CaptureLandingAnchor(pt.position + radialNormal * (currentPlanet.getPlanetDiameter() / 2f), radialNormal);
            return true;
        }

        private bool TryGetCurrentSurfaceAltitude(out float altitude)
        {
            altitude = 0f;

            if (currentPlanet == null || currentPlanet.Transform == null)
            {
                return false;
            }

            Transform pt = currentPlanet.Transform;
            Vector3 toShip = spaceShip.transform.position - pt.position;
            if (toShip.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 radialNormal = toShip.normalized;
            if (TryFindCurrentPlanetSurface(radialNormal, out RaycastHit surfaceHit))
            {
                Vector3 outwardNormal = GetOutwardSurfaceNormal(surfaceHit.normal, radialNormal);
                altitude = Vector3.Dot(spaceShip.transform.position - surfaceHit.point, outwardNormal) - GetLandingSurfaceOffset(outwardNormal);
                return true;
            }

            altitude = toShip.magnitude - currentPlanet.getPlanetDiameter() / 2f - GetShipSurfaceOffset(radialNormal);
            return true;
        }

        private bool TryFindCurrentPlanetSurface(Vector3 radialNormal, out RaycastHit hit)
        {
            return TryFindPlanetSurface(currentPlanet, radialNormal, out hit);
        }

        private bool TryFindPlanetSurface(Planet planet, Vector3 radialNormal, out RaycastHit hit)
        {
            hit = default;

            if (planet == null || planet.Transform == null || radialNormal.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 normal = radialNormal.normalized;
            Transform pt = planet.Transform;
            float planetDiameter = planet.getPlanetDiameter();
            Vector3 rayStart = pt.position + normal * planetDiameter;
            return Physics.Raycast(rayStart, -normal, out hit, planetDiameter * 2f, TerrainQueryMask, QueryTriggerInteraction.Ignore);
        }

        private static Vector3 GetOutwardSurfaceNormal(Vector3 hitNormal, Vector3 radialNormal)
        {
            Vector3 fallbackNormal = radialNormal.sqrMagnitude > 0.0001f ? radialNormal.normalized : Vector3.up;
            Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : fallbackNormal;
            if (Vector3.Dot(normal, fallbackNormal) < 0f)
            {
                normal = -normal;
            }

            if (Vector3.Dot(normal, fallbackNormal) < 0.2f)
            {
                normal = fallbackNormal;
            }

            return normal;
        }

        private float GetLandingSurfaceOffset(Vector3 surfaceNormal)
        {
            return LandingSurfaceClearance;
        }

        private void ApplyLandingAnchor()
        {
            if (!hasLandingAnchor || landingAnchorPlanet == null || landingAnchorPlanet.Transform == null)
            {
                return;
            }

            Transform pt = landingAnchorPlanet.Transform;
            Vector3 surfacePoint = pt.TransformPoint(localLandingSurfacePoint);
            Vector3 outwardNormal = pt.TransformDirection(localLandingSurfaceNormal);
            if (outwardNormal.sqrMagnitude > 0.0001f)
            {
                outwardNormal.Normalize();
            }
            else
            {
                outwardNormal = (surfacePoint - pt.position).normalized;
            }

            Quaternion targetRotation = pt.rotation * localLandingRotation;
            targetRotation = Quaternion.FromToRotation(targetRotation * Vector3.up, outwardNormal) * targetRotation;
            spaceShip.transform.rotation = targetRotation;
            spaceShip.transform.position = surfacePoint + outwardNormal * GetLandingSurfaceOffset(outwardNormal);
            lastPlanetPos = pt.position;
            lastPlanetRot = pt.rotation;
        }

        private void CaptureLandingAnchor(Vector3 surfacePoint, Vector3 outwardNormal)
        {
            if (currentPlanet == null || currentPlanet.Transform == null)
            {
                return;
            }

            Transform pt = currentPlanet.Transform;
            Vector3 normal = outwardNormal.sqrMagnitude > 0.0001f ? outwardNormal.normalized : GetRadialNormal(pt);
            localLandingPosition = pt.InverseTransformPoint(spaceShip.transform.position);
            localLandingSurfacePoint = pt.InverseTransformPoint(surfacePoint);
            localLandingSurfaceNormal = pt.InverseTransformDirection(normal).normalized;
            localLandingRotation = Quaternion.Inverse(pt.rotation) * spaceShip.transform.rotation;
            landingAnchorPlanet = currentPlanet;
            hasLandingAnchor = true;
            waitingForLandingSurface = false;
            lastPlanetPos = pt.position;
            lastPlanetRot = pt.rotation;
        }

        private void CaptureLandingAnchor()
        {
            if (currentPlanet == null || currentPlanet.Transform == null)
            {
                return;
            }

            Vector3 radialNormal = GetRadialNormal(currentPlanet.Transform);
            if (TryFindCurrentPlanetSurface(radialNormal, out RaycastHit hit))
            {
                CaptureLandingAnchor(hit.point, GetOutwardSurfaceNormal(hit.normal, radialNormal));
                return;
            }

            Transform pt = currentPlanet.Transform;
            CaptureLandingAnchor(pt.position + radialNormal * (currentPlanet.getPlanetDiameter() / 2f), radialNormal);
        }

        private void CaptureLandingRotation()
        {
            if (currentPlanet == null || currentPlanet.Transform == null)
            {
                return;
            }

            Transform pt = currentPlanet.Transform;
            localLandingPosition = pt.InverseTransformPoint(spaceShip.transform.position);
            localLandingRotation = Quaternion.Inverse(pt.rotation) * spaceShip.transform.rotation;
            lastPlanetPos = pt.position;
            lastPlanetRot = pt.rotation;
        }

        private void ClearLandingAnchor()
        {
            hasLandingAnchor = false;
            landingAnchorPlanet = null;
            waitingForLandingSurface = false;
        }

        private Vector3 GetRadialNormal(Transform planetTransform)
        {
            Vector3 radial = spaceShip.transform.position - planetTransform.position;
            if (radial.sqrMagnitude > 0.0001f)
            {
                return radial.normalized;
            }

            return planetTransform.up.sqrMagnitude > 0.0001f ? planetTransform.up.normalized : Vector3.up;
        }

        private float GetShipSurfaceOffset(Vector3 surfaceNormal)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : spaceShip.transform.up;
            if (shipColliders == null || shipColliders.Length == 0)
            {
                shipColliders = spaceShip.GetComponentsInChildren<Collider>();
            }

            bool foundCollider = false;
            float lowestProjection = 0f;
            for (int i = 0; i < shipColliders.Length; i++)
            {
                Collider shipCollider = shipColliders[i];
                if (shipCollider == null || !shipCollider.enabled)
                {
                    continue;
                }

                Vector3 center;
                float extentProjection;
                if (!TryGetColliderProjection(shipCollider, normal, out center, out extentProjection))
                {
                    continue;
                }

                float centerProjection = Vector3.Dot(center - spaceShip.transform.position, normal);
                float minProjection = centerProjection - extentProjection;

                if (!foundCollider || minProjection < lowestProjection)
                {
                    lowestProjection = minProjection;
                    foundCollider = true;
                }
            }

            if (!foundCollider)
            {
                return spaceShip.transform.localScale.y * 0.5f + shipSurfaceClearance;
            }

            return Mathf.Max(shipSurfaceClearance, shipSurfaceClearance - lowestProjection);
        }

        private static bool TryGetColliderProjection(Collider collider, Vector3 normal, out Vector3 center, out float extentProjection)
        {
            center = collider.bounds.center;
            extentProjection = 0f;

            if (collider is BoxCollider box)
            {
                Transform t = box.transform;
                center = t.TransformPoint(box.center);
                Vector3 halfSize = Vector3.Scale(box.size * 0.5f, Abs(t.lossyScale));
                extentProjection =
                    Mathf.Abs(Vector3.Dot(normal, t.right)) * halfSize.x +
                    Mathf.Abs(Vector3.Dot(normal, t.up)) * halfSize.y +
                    Mathf.Abs(Vector3.Dot(normal, t.forward)) * halfSize.z;
                return true;
            }

            if (collider is SphereCollider sphere)
            {
                Transform t = sphere.transform;
                center = t.TransformPoint(sphere.center);
                Vector3 scale = Abs(t.lossyScale);
                extentProjection = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                return true;
            }

            if (collider is CapsuleCollider capsule)
            {
                Transform t = capsule.transform;
                center = t.TransformPoint(capsule.center);
                Vector3 scale = Abs(t.lossyScale);
                Vector3 axis = capsule.direction == 0 ? t.right : capsule.direction == 1 ? t.up : t.forward;
                float axisScale = capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z;
                float radiusScale = capsule.direction == 0
                    ? Mathf.Max(scale.y, scale.z)
                    : capsule.direction == 1
                        ? Mathf.Max(scale.x, scale.z)
                        : Mathf.Max(scale.x, scale.y);
                float radius = capsule.radius * radiusScale;
                float cylinderHalfHeight = Mathf.Max(0f, capsule.height * axisScale * 0.5f - radius);
                extentProjection = Mathf.Abs(Vector3.Dot(normal, axis)) * cylinderHalfHeight + radius;
                return true;
            }

            Bounds bounds = collider.bounds;
            center = bounds.center;
            Vector3 extents = bounds.extents;
            extentProjection =
                Mathf.Abs(normal.x) * extents.x +
                Mathf.Abs(normal.y) * extents.y +
                Mathf.Abs(normal.z) * extents.z;
            return true;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
