using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SolarSystemExplorer.Runtime
{
    public class FreeFlyCameraController : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float GetDevicePixelRatio();
#else
        private static float GetDevicePixelRatio() { return 1f; }
#endif

        private const float MaxMouseDelta = 50f;

        [Header("Translation")]
        [SerializeField] private float moveSpeed = 260f;
        [SerializeField] private float boostMultiplier = 4f;
        [SerializeField] private float acceleration = 12f;

        [Header("Rotation")]
        [SerializeField] private float lookSensitivity = 0.15f;
        [SerializeField] private bool requireRightMouseForLook = true;
        [SerializeField] private bool lockCursorWhileLooking = true;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        private float yaw;
        private float pitch;
        private Vector3 currentVelocity;
        private int skipFrames;
        private CursorLockMode previousLockState;

        public void ConfigureLookMode(bool requireRmbToLook, bool lockCursorWhenLooking)
        {
            requireRightMouseForLook = requireRmbToLook;
            lockCursorWhileLooking = lockCursorWhenLooking;
        }

        private void Awake()
        {
            SyncRotationState();
        }

        private void OnEnable()
        {
            SyncRotationState();
            currentVelocity = Vector3.zero;
            skipFrames = 2;
            previousLockState = Cursor.lockState;
        }

        private void Update()
        {
            HandleLook();
            HandleTranslation();
        }

        private void OnDisable()
        {
            if (lockCursorWhileLooking)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void SyncRotationState()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
        }

        private void HandleLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            bool canLook = !requireRightMouseForLook || mouse.rightButton.isPressed;
            if (!canLook)
            {
                if (lockCursorWhileLooking)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }

            if (lockCursorWhileLooking)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    skipFrames = 3;
                }
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (skipFrames > 0)
            {
                skipFrames--;
                return;
            }

            Vector2 mouseDelta = mouse.delta.ReadValue();
            float dpr = GetDevicePixelRatio();
            float mouseX = Mathf.Clamp(mouseDelta.x / dpr, -MaxMouseDelta, MaxMouseDelta);
            float mouseY = Mathf.Clamp(mouseDelta.y / dpr, -MaxMouseDelta, MaxMouseDelta);

            yaw += mouseX * lookSensitivity;
            pitch -= mouseY * lookSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleTranslation()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float forwardInput = 0f;
            if (keyboard.wKey.isPressed)
            {
                forwardInput += 1f;
            }
            if (keyboard.sKey.isPressed)
            {
                forwardInput -= 1f;
            }

            float rightInput = 0f;
            if (keyboard.dKey.isPressed)
            {
                rightInput += 1f;
            }
            if (keyboard.aKey.isPressed)
            {
                rightInput -= 1f;
            }

            float upInput = 0f;
            if (keyboard.eKey.isPressed)
            {
                upInput += 1f;
            }
            if (keyboard.qKey.isPressed)
            {
                upInput -= 1f;
            }

            Vector3 moveDirection = (transform.forward * forwardInput) + (transform.right * rightInput) + (transform.up * upInput);
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            float speed = moveSpeed;
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            {
                speed *= boostMultiplier;
            }

            Vector3 targetVelocity = moveDirection * speed;
            float lerpFactor = 1f - Mathf.Exp(-acceleration * Time.deltaTime);
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, lerpFactor);

            transform.position += currentVelocity * Time.deltaTime;
        }
    }
}
