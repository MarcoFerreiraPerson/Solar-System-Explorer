using UnityEngine;

public class MainMenuHandler : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private string solarSystemSceneName;

    public void StartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(solarSystemSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
