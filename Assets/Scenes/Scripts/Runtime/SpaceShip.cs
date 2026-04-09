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

            Vector3 toShip = spaceShip.transform.position - startingPlanet.transform.position;
            float altitude = toShip.magnitude - planet.getPlanetDiameter() / 2f;

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
                velocity = spaceShip.transform.up * 5f;

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

            Vector3 surfaceNormal = (spaceShip.transform.position - pt.position).normalized;
            float snapRadius = planet.getPlanetDiameter() / 2f + (spaceShip.transform.localScale.y + 1f) / 2f;
            spaceShip.transform.position = pt.position + surfaceNormal * snapRadius;
            spaceShip.transform.rotation = Quaternion.FromToRotation(spaceShip.transform.up, surfaceNormal) * spaceShip.transform.rotation;

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

            Vector3 toShip = spaceShip.transform.position - planet.getPlanet().transform.position;
            float minDist = planet.getPlanetDiameter() / 2f + spaceShip.transform.localScale.y / 2f;

            if (toShip.magnitude < minDist)
            {
                Vector3 normal = toShip.normalized;
                spaceShip.transform.position = planet.getPlanet().transform.position + normal * minDist;
                velocity = Vector3.zero;
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
                player.getPlayer().GetComponent<MeshRenderer>().enabled = false;

                mainCamera.transform.SetParent(null);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (isBoarded && state == 0)
            {
                isBoarded = false;

                player.getPlayer().transform.position =
                    spaceShip.transform.position + spaceShip.transform.right * 2f;
                player.getPlayer().GetComponent<MeshRenderer>().enabled = true;

                mainCamera.transform.SetParent(player.getPlayer().transform);
                mainCamera.transform.localPosition = new Vector3(0f, 1f, 0f);
                mainCamera.transform.localRotation = Quaternion.identity;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = true;
            }
        }
    }
}
