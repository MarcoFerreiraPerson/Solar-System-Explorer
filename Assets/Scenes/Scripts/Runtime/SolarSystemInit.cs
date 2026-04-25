using SolarSystemExplorer.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SolarSystemInit : MonoBehaviour
{
    [SerializeField] private string solarSystemSceneName = "SampleScene";
    InputSystem_Actions playerInput;
    SolarSystemBootstrap solarSystemBootstrap;

    void Awake()
    {
        playerInput = new InputSystem_Actions();
        solarSystemBootstrap = new SolarSystemBootstrap();
        solarSystemBootstrap.initialize(transform);
    }

    private void Update()
    {
        solarSystemBootstrap.systemUpdate();
    }

    private void OnEnable()
    {
        playerInput.Player.Interact.performed += OnInteractPressed;
        playerInput.Player.Quit.performed += OnQuitPressed;
        playerInput.Enable();
    }

    void OnDisable()
    {
        if (playerInput == null) return;

        playerInput.Player.Interact.performed -= OnInteractPressed;
        playerInput.Player.Quit.performed -= OnQuitPressed;

        playerInput.Disable();

    }

    private void OnInteractPressed(InputAction.CallbackContext _) {
        SceneManager.LoadScene(solarSystemSceneName);
    }

    private void OnQuitPressed(InputAction.CallbackContext _) {
        #if UNITY_EDITOR
            EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

}
