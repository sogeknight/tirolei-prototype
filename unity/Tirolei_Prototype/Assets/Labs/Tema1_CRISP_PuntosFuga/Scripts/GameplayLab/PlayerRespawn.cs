using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerRespawn : MonoBehaviour
{
    [Header("Checkpoint (por ahora solo se guarda; más adelante respawnea aquí)")]
    public bool useCheckpoint = true;
    public Transform currentCheckpoint; // si es null, se usa la posición inicial
    private Vector3 initialSpawnPos;

    [Header("Game Over UI (OnGUI)")]
    public bool enableGameOverUI = true;
    public string gameOverTitle = "GAME OVER";
    public string restartQuestion = "Restart?";
    public string yesText = "YES";
    public string noText = "NO";

    [Header("Freeze")]
    public bool freezeTimeOnGameOver = true;

    [Header("Optional: disable player control on Game Over")]
    public Behaviour[] disableTheseBehaviours;

    [Header("Game Over Input")]
    public KeyCode confirmKey = KeyCode.Z;
    public bool acceptEnterSpaceToo = true;
    public KeyCode upKey = KeyCode.UpArrow;
    public KeyCode downKey = KeyCode.DownArrow;
    public KeyCode upAltKey = KeyCode.W;
    public KeyCode downAltKey = KeyCode.S;

    [Tooltip("Nombre del eje vertical (Input Manager). Normalmente 'Vertical'.")]
    public string verticalAxis = "Vertical";
    [Tooltip("Umbral para considerar que el stick/cruceta se ha pulsado.")]
    public float axisThreshold = 0.5f;
    [Tooltip("Cooldown para que el stick no navegue 200 veces por segundo.")]
    public float axisRepeatDelay = 0.18f;

    private bool isGameOver;
    private float prevTimeScale = 1f;

    // 0 = YES, 1 = NO
    private int selectedIndex = 0;
    private float axisCooldown = 0f;

    private void Awake()
    {
        isGameOver = false;
        initialSpawnPos = transform.position;
    }

    private void Update()
    {
        if (!isGameOver) return;

        if (axisCooldown > 0f)
            axisCooldown -= Time.unscaledDeltaTime;

        bool navUp = Input.GetKeyDown(upKey) || Input.GetKeyDown(upAltKey);
        bool navDown = Input.GetKeyDown(downKey) || Input.GetKeyDown(downAltKey);

        float v = 0f;
        if (!string.IsNullOrEmpty(verticalAxis))
            v = Input.GetAxisRaw(verticalAxis);

        if (axisCooldown <= 0f)
        {
            if (v >= axisThreshold) { navUp = true; axisCooldown = axisRepeatDelay; }
            else if (v <= -axisThreshold) { navDown = true; axisCooldown = axisRepeatDelay; }
        }

        if (navUp) selectedIndex = 0;
        else if (navDown) selectedIndex = 1;

        bool confirm = Input.GetKeyDown(confirmKey);
        if (acceptEnterSpaceToo)
            confirm |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);

        if (confirm)
        {
            if (selectedIndex == 0) RestartScene();
            else QuitOrStopPlaymode();
        }
    }

    // ===== API que tu proyecto ya espera (SectionTrigger.cs) =====
    public void SetCheckpoint(Transform checkpoint)
    {
        if (!useCheckpoint) return;
        currentCheckpoint = checkpoint;
        Debug.Log("[PlayerRespawn] SetCheckpoint -> " + (checkpoint ? checkpoint.name : "NULL"));
    }

    public void SetCheckpoint(Vector3 worldPos)
    {
        if (!useCheckpoint) return;
        currentCheckpoint = null;
        initialSpawnPos = worldPos;
        Debug.Log("[PlayerRespawn] SetCheckpoint(Vector3) -> " + worldPos);
    }

    public Vector3 GetCheckpointPosition()
    {
        if (!useCheckpoint) return initialSpawnPos;
        if (currentCheckpoint != null) return currentCheckpoint.position;
        return initialSpawnPos;
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;
        selectedIndex = 0;
        axisCooldown = 0f;

        if (disableTheseBehaviours != null)
        {
            for (int i = 0; i < disableTheseBehaviours.Length; i++)
                if (disableTheseBehaviours[i] != null)
                    disableTheseBehaviours[i].enabled = false;
        }

        if (freezeTimeOnGameOver)
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        Debug.Log("[PlayerRespawn] GAME OVER");
    }

    private void OnGUI()
    {
        if (!enableGameOverUI) return;
        if (!isGameOver) return;

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = Mathf.Min(520f, Screen.width * 0.8f);
        float h = 260f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;

        GUI.Box(new Rect(x, y, w, h), "");

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(x, y + 18, w, 40), gameOverTitle, titleStyle);

        var questionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18
        };
        GUI.Label(new Rect(x, y + 68, w, 30), restartQuestion, questionStyle);

        float btnW = Mathf.Min(220f, w * 0.6f);
        float btnH = 44f;
        float bx = x + (w - btnW) * 0.5f;

        float byYes = y + 120f;
        float byNo = y + 174f;

        var normalBtn = new GUIStyle(GUI.skin.button);
        var selectedBtn = new GUIStyle(GUI.skin.button);
        selectedBtn.fontStyle = FontStyle.Bold;
        selectedBtn.fontSize = normalBtn.fontSize + 2;

        string yesLabel = (selectedIndex == 0) ? ("> " + yesText) : yesText;
        string noLabel = (selectedIndex == 1) ? ("> " + noText) : noText;

        if (GUI.Button(new Rect(bx, byYes, btnW, btnH), yesLabel, selectedIndex == 0 ? selectedBtn : normalBtn))
            RestartScene();

        if (GUI.Button(new Rect(bx, byNo, btnW, btnH), noLabel, selectedIndex == 1 ? selectedBtn : normalBtn))
            QuitOrStopPlaymode();

        var hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        GUI.Label(new Rect(x, y + h - 34, w, 20), $"↑/↓ or D-Pad, Confirm: {confirmKey}", hintStyle);
    }

    private void RestartScene()
    {
        if (freezeTimeOnGameOver) Time.timeScale = prevTimeScale;

        if (disableTheseBehaviours != null)
        {
            for (int i = 0; i < disableTheseBehaviours.Length; i++)
                if (disableTheseBehaviours[i] != null)
                    disableTheseBehaviours[i].enabled = true;
        }

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        Debug.Log("[PlayerRespawn] Restart -> " + buildIndex);
        SceneManager.LoadScene(buildIndex);
    }

    private void QuitOrStopPlaymode()
    {
        if (freezeTimeOnGameOver) Time.timeScale = prevTimeScale;

#if UNITY_EDITOR
        Debug.Log("[PlayerRespawn] NO -> Stop Playmode (Editor)");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Debug.Log("[PlayerRespawn] NO -> Application.Quit()");
        Application.Quit();
#endif
    }
}
