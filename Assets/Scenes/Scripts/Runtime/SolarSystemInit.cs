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

    void Awake()
    {
        playerInput = new InputSystem_Actions();
        Debug.Log(playerInput.Player.enabled);
        SolarSystemBootstrap.Initialize();
    }

    private void OnEnable()
{
        Debug.Log("SolarSystemInit OnEnable");

        playerInput.Player.Interact.performed += OnInteractPressed;
        playerInput.Player.Quit.performed += OnQuitPressed;
        playerInput.Enable();
        Debug.Log(playerInput.Player.enabled);

        Debug.Log("Input enabled");
    }

    void OnDisable()
    {
        if (playerInput == null) return;

        playerInput.Player.Interact.performed -= OnInteractPressed;
        playerInput.Player.Quit.performed -= OnQuitPressed;

        playerInput.Disable();

    }

    private void OnInteractPressed(InputAction.CallbackContext _) {
        Debug.Log("Interact pressed");
        SceneManager.LoadScene(solarSystemSceneName);
    }

    private void OnQuitPressed(InputAction.CallbackContext _) {
        Debug.Log("Quit pressed");
       
        #if UNITY_EDITOR
            EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

}

