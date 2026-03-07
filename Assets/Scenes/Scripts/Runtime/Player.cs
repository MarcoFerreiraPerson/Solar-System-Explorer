using SolarSystemExplorer.Runtime;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SolarSystemExplorer.Runtime
{
    public class Player
    {

        [SerializeField] private float movementSpeed = 8;
        [SerializeField] private float mass;
        [SerializeField] private Vector3 position;

        private float mouseSensitivity = 0.2f;
        private float pitch = 0f;

        GameObject startingPlanet;
        Vector3 planetCenterPos;
        Vector3 lastPlanetCenterPos;
        Quaternion lastPlanetRotation;
        Camera mainCamera;
        private Vector3 velocity;
        [SerializeField] private float maxSpeed = 6000f;

        private GameObject player;
        // Start is called once before the first execution of Update after the MonoBehaviour is create

        public Player(Planet planet)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mainCamera = Camera.main;
            mainCamera.transform.SetParent(player.transform);
            mainCamera.transform.localPosition = new Vector3(0, 1.6f, 0);
            mainCamera.transform.localRotation = Quaternion.identity;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            startingPlanet = planet.getPlanet();
            lastPlanetCenterPos = startingPlanet.transform.position;
            lastPlanetRotation = startingPlanet.transform.rotation;
            Vector3 playerStartingPos = startingPlanet.transform.position + player.transform.up * (planet.getPlanetDiamter() / 2f + (player.transform.localScale.y + 3) / 2f);

            player.transform.position = playerStartingPos;
        }

        public void updatePlayer(Planet planet,float Gconstant)
        {
            Vector3 playerPos = player.transform.position;
            Transform planetTransform = planet.getPlanet().transform;

            planetCenterPos = planetTransform.position;

            Vector3 planetDelta = planetCenterPos - lastPlanetCenterPos;
            playerPos += planetDelta;

            Quaternion currentRotation = planetTransform.rotation;

            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastPlanetRotation);

            Vector3 offset = playerPos - planetCenterPos;
            offset = deltaRotation * offset;

            playerPos = planetCenterPos + offset;

            player.transform.rotation = deltaRotation * player.transform.rotation;

            Vector3 surfaceNormal = (playerPos - planetCenterPos).normalized;
            RotateCamera(surfaceNormal);
            playerPos = MovePlayer(playerPos, surfaceNormal);

            surfaceNormal = (playerPos - planetCenterPos).normalized;


            float surfaceHeight = planet.getPlanetDiamter() / 2f + (player.transform.localScale.y + 1) / 2f;

            playerPos = planetCenterPos + surfaceNormal * surfaceHeight;
            player.transform.rotation = Quaternion.FromToRotation(player.transform.up, surfaceNormal) * player.transform.rotation;

            //float attractorMass = planet.getMass();
            //float minDistance = planet.getMinDistance();
            //Vector3 acceleration = GravityModel.ComputeAcceleration(playerPos, planetTransform.position, Gconstant, attractorMass, minDistance);

            //float dt = Time.fixedDeltaTime;

            //velocity += acceleration * dt;
            //if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
            //{
            //    velocity = velocity.normalized * maxSpeed;
            //}

            //Vector3 nextPosition = playerPos + velocity * dt;

            player.transform.position = playerPos;

            lastPlanetCenterPos = planetCenterPos;
            lastPlanetRotation = currentRotation;

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
            Vector2 delta = Mouse.current.delta.ReadValue();

            float mouseX = delta.x;
            float mouseY = delta.y;

            float yaw = mouseX * mouseSensitivity;
            float pitchDelta = mouseY * mouseSensitivity;

            player.transform.Rotate(Vector3.up, yaw, Space.Self);

            pitch -= pitchDelta;
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            mainCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
