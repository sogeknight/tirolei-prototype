using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    public string gameplaySceneName = "0000_TrainingArea";

    private const string PREF_RUNMODE = "RUN_MODE"; // 0 NEW, 1 CONTINUE
    private const string PREF_HAS = "CP_HAS";

    public bool HasSave()
    {
        return PlayerPrefs.GetInt(PREF_HAS, 0) == 1;
    }

    public void NewGame()
    {
        PlayerPrefs.SetInt(PREF_RUNMODE, 0);

        PlayerPrefs.DeleteKey("CP_HAS");
        PlayerPrefs.DeleteKey("CP_X");
        PlayerPrefs.DeleteKey("CP_Y");
        PlayerPrefs.DeleteKey("CP_ID");

        PlayerPrefs.Save();
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void Continue()
    {
        if (!HasSave())
        {
            Debug.Log("[MainMenu] Continue blocked: no save found.");
            return;
        }

        PlayerPrefs.SetInt(PREF_RUNMODE, 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
