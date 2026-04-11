using UnityEngine;
using UnityEngine.InputSystem;

namespace SolarSystemExplorer.Runtime
{
    public class SpaceShip
    {
        private float thrustPower = 60f;
        private float maxSpeed = 150f;
        private float fakeGravity = 80000f;
        private float mouseSensitivity = 2f;

        private int state = 0;

        private bool isBoarded = false;
        private float boardDistance = 5f;
        private Player player;

        private GameObject spaceShip;
        private Camera mainCamera;
        private Vector3 velocity;

        private GameObject startingPlanet;

        private float landedThreshold = 3f;

        private float launchLockTimer = 0f;
        private float launchLockDuration = 2.5f;
        private float shipSurfaceClearance = 0.35f;
        private float launchVelocity = 20f;

        private Vector3 lastPlanetPos;
        private Quaternion lastPlanetRot;

        private float camFollowDistance = 15f;
        private float camFollowHeight = 3f;

        public bool IsBoarded => isBoarded;

        public SpaceShip(Planet planet, Player player)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Spaceship");
            this.player = player;
            spaceShip = GameObject.Instantiate(prefab);
            // Put the ship (and all its children) on Ignore Raycast (layer 2) so
            // landing/collision raycasts don't hit the ship's own colliders before
            // the planet terrain.
            SetLayerRecursively(spaceShip, 2);
            mainCamera = Camera.main;
            startingPlanet = planet.getPlanet();

            Transform pt = startingPlanet.transform;
            float radius = planet.getPlanetDiameter() / 2f;
            Vector3 up = pt.up;

            spaceShip.transform.position = pt.position + up * (radius + spaceShip.transform.localScale.y / 2f);
            spaceShip.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);

            lastPlanetPos = pt.position;
            lastPlanetRot = pt.rotation;

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

        public void spaceshipUpdate(Planet planet, float Gconstant)
        {
            UpdateState(planet);

            if (state == 0)
                updateLanded(planet);
            else
                updateFlying(planet);
        }

        private void UpdateState(Planet planet)
        {
            if (launchLockTimer > 0f)
            {
                launchLockTimer -= Time.deltaTime;
                state = 1;
                return;
            }

            // Altitude above the actual (displaced) terrain, not the nominal sphere.
            // Raycast from the ship toward the planet center; altitude is the distance
            // traveled before hitting terrain. Fall back to sphere-based if the raycast
            // misses (first frame before collider exists, or ship is below surface).
            Vector3 toShip = spaceShip.transform.position - startingPlanet.transform.position;
            Vector3 shipDown = -toShip.normalized;
            float planetDiameter = planet.getPlanetDiameter();
            float altitude;
            if (Physics.Raycast(spaceShip.transform.position, shipDown, out RaycastHit altHit, planetDiameter))
            {
                altitude = altHit.distance - spaceShip.transform.localScale.y / 2f;
            }
            else
            {
                altitude = toShip.magnitude - planetDiameter / 2f;
            }

            if (altitude <= landedThreshold)
                state = 0;
            else
                state = 1;
        }

        private void updateLanded(Planet planet)
        {
            if (isBoarded && Keyboard.current.spaceKey.isPressed)
            {
                state = 1;
                launchLockTimer = launchLockDuration;
                velocity = spaceShip.transform.up * launchVelocity;

                lastPlanetPos = startingPlanet.transform.position;
                lastPlanetRot = startingPlanet.transform.rotation;
                return;
            }

            Transform pt = startingPlanet.transform;

            Vector3 planetDelta = pt.position - lastPlanetPos;
            spaceShip.transform.position += planetDelta;

            Quaternion rotDelta = pt.rotation * Quaternion.Inverse(lastPlanetRot);
            Vector3 offset = spaceShip.transform.position - pt.position;
            offset = rotDelta * offset;
            spaceShip.transform.position = pt.position + offset;
            spaceShip.transform.rotation = rotDelta * spaceShip.transform.rotation;

            // Raycast-based landing snap. Cast from well above the peak toward the planet
            // center to find the actual terrain, then sit on it oriented to the local
            // surface normal. Fall back to sphere snap if the collider isn't ready.
            Vector3 radialNormal = (spaceShip.transform.position - pt.position).normalized;
            float shipHalfHeight = (spaceShip.transform.localScale.y + 1f) / 2f;
            float planetDiameter = planet.getPlanetDiameter();
            Vector3 rayStart = pt.position + radialNormal * planetDiameter;

            Vector3 uprightNormal;
            if (Physics.Raycast(rayStart, -radialNormal, out RaycastHit landHit, planetDiameter * 2f))
            {
                spaceShip.transform.position = landHit.point + landHit.normal * (shipHalfHeight + shipSurfaceClearance);
                uprightNormal = landHit.normal;
            }
            else
            {
                float snapRadius = planetDiameter / 2f + shipHalfHeight + shipSurfaceClearance;
                spaceShip.transform.position = pt.position + radialNormal * snapRadius;
                uprightNormal = radialNormal;
            }
            spaceShip.transform.rotation = Quaternion.FromToRotation(spaceShip.transform.up, uprightNormal) * spaceShip.transform.rotation;

            velocity = Vector3.zero;
            lastPlanetPos = pt.position;
            lastPlanetRot = pt.rotation;
        }

        private void updateFlying(Planet planet)
        {
            float dt = Time.deltaTime;

            Vector3 thrust = Vector3.zero;
            bool thrusting = false;

            if (isBoarded)
            {
                if (Keyboard.current.wKey.isPressed) { thrust -= spaceShip.transform.forward; thrusting = true; }
                if (Keyboard.current.sKey.isPressed) { thrust += spaceShip.transform.forward; thrusting = true; }
                if (Keyboard.current.aKey.isPressed) { thrust += spaceShip.transform.right; thrusting = true; }
                if (Keyboard.current.dKey.isPressed) { thrust -= spaceShip.transform.right; thrusting = true; }
                if (Keyboard.current.spaceKey.isPressed) { thrust += spaceShip.transform.up; thrusting = true; }
                if (Keyboard.current.leftCtrlKey.isPressed) { thrust -= spaceShip.transform.up; thrusting = true; }

                if (thrusting)
                    velocity += thrust.normalized * thrustPower * dt;
            }

            Vector3 toPlanet = planet.getPlanet().transform.position - spaceShip.transform.position;
            float dist = Mathf.Max(toPlanet.magnitude, 1f);
            float gravScale = fakeGravity / (dist * dist);
            velocity += toPlanet.normalized * gravScale * dt;

            if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
                velocity = velocity.normalized * maxSpeed;

            spaceShip.transform.position += velocity * dt;

            // Collision clamp: raycast from the ship toward the planet center. If the
            // ray hits terrain within the ship's own half-height, push the ship back
            // along the terrain normal and zero its velocity. Fall back to the old
            // sphere clamp if the collider isn't ready.
            Vector3 planetCenter = planet.getPlanet().transform.position;
            Vector3 toShip = spaceShip.transform.position - planetCenter;
            Vector3 downToPlanet = -toShip.normalized;
            float shipHalfHeight = spaceShip.transform.localScale.y / 2f;
            float castLen = toShip.magnitude + 1f;

            if (launchLockTimer <= 0f && Physics.Raycast(spaceShip.transform.position, downToPlanet, out RaycastHit clampHit, castLen))
            {
                if (clampHit.distance < shipHalfHeight + shipSurfaceClearance)
                {
                    spaceShip.transform.position = clampHit.point + clampHit.normal * (shipHalfHeight + shipSurfaceClearance);
                    velocity = Vector3.zero;
                }
            }
            else if (launchLockTimer <= 0f)
            {
                float minDist = planet.getPlanetDiameter() / 2f + shipHalfHeight + shipSurfaceClearance;
                if (toShip.magnitude < minDist)
                {
                    Vector3 normal = toShip.normalized;
                    spaceShip.transform.position = planetCenter + normal * minDist;
                    velocity = Vector3.zero;
                }
            }
        }

        public void HandleMouseLook()
        {
            if (!isBoarded) return;

            Vector2 delta = Mouse.current.delta.ReadValue();

            if (state == 0)
            {
                Vector3 surfaceNormal = (spaceShip.transform.position - startingPlanet.transform.position).normalized;
                    spaceShip.transform.Rotate(surfaceNormal, delta.x * mouseSensitivity, Space.World);
            }
            else
            {
                spaceShip.transform.Rotate(Vector3.up, delta.x * mouseSensitivity, Space.Self);
                spaceShip.transform.Rotate(Vector3.right, delta.y * mouseSensitivity, Space.Self);
            }
        }

        public void HandleBoarding()
        {
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

                Vector3 planetCenter = startingPlanet.transform.position;
                Vector3 radialNormal = (spaceShip.transform.position - planetCenter).normalized;
                Vector3 lateralOffset = Vector3.ProjectOnPlane(spaceShip.transform.right, radialNormal).normalized;
                if (lateralOffset.sqrMagnitude < 0.0001f)
                {
                    lateralOffset = Vector3.Cross(radialNormal, spaceShip.transform.forward).normalized;
                }

                float planetDiameter = startingPlanet.transform.lossyScale.x * 2f;
                Vector3 exitPosition = spaceShip.transform.position + lateralOffset * 4f + radialNormal * 2f;
                player.SnapToSurface(startingPlanet.transform, planetDiameter, exitPosition);

                mainCamera.transform.SetParent(null);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = true;
            }
        }
    }
}
