using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButtonActions : MonoBehaviour
{
    [SerializeField] private string defaultSceneName = "ztl";

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void LoadNextScene()
    {
        LoadSceneByName(defaultSceneName);
    }

    public void LoadNextScene(string sceneName)
    {
        LoadSceneByName(sceneName);
    }

    private void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[MainMenuButtonActions] Scene name is empty.", this);
            return;
        }

        SceneManager.LoadScene(sceneName.Trim());
    }
}
