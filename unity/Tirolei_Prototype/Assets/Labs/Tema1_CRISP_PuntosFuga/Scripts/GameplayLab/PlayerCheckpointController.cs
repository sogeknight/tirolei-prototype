using UnityEngine;

public class PlayerCheckpointController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerBounceAttack bounce;  // arrástralo o auto-find
    public Rigidbody2D rb;             // arrástralo o auto-find

    [Header("Respawn")]
    public Vector2 currentCheckpointPos;

    private const string PREF_HAS = "CP_HAS";
    private const string PREF_X = "CP_X";
    private const string PREF_Y = "CP_Y";
    private const string PREF_FLAME = "CP_FLAME";
    private const string PREF_ID = "CP_ID";

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!bounce) bounce = GetComponent<PlayerBounceAttack>();
    }

    private void Start()
    {
        LoadCheckpointFromPrefsIfAny();
    }

    // Llamado por el checkpoint
    public void SetCheckpoint(Vector2 pos)
    {
        currentCheckpointPos = pos;
    }

    // Llamado por el checkpoint
    public void RefillPower()
    {
        if (bounce == null) return;

        // Recarga total de llama (tu sistema actual)
        bounce.flame = bounce.maxFlame;
    }

    // Llamado por el checkpoint
    public void SaveCheckpointToPrefs(string checkpointId)
    {
        PlayerPrefs.SetInt(PREF_HAS, 1);
        PlayerPrefs.SetFloat(PREF_X, currentCheckpointPos.x);
        PlayerPrefs.SetFloat(PREF_Y, currentCheckpointPos.y);
        PlayerPrefs.SetString(PREF_ID, checkpointId);

        if (bounce != null)
            PlayerPrefs.SetFloat(PREF_FLAME, bounce.flame);

        PlayerPrefs.Save();
    }

    // Llamar al iniciar escena o tras reinicio
    public void LoadCheckpointFromPrefsIfAny()
    {
        if (PlayerPrefs.GetInt(PREF_HAS, 0) != 1) return;

        float x = PlayerPrefs.GetFloat(PREF_X, transform.position.x);
        float y = PlayerPrefs.GetFloat(PREF_Y, transform.position.y);

        currentCheckpointPos = new Vector2(x, y);

        // Teleport limpio a respawn
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = currentCheckpointPos;
        }
        else
        {
            transform.position = currentCheckpointPos;
        }

        // Restaura llama guardada (si existe)
        if (bounce != null)
        {
            float flame = PlayerPrefs.GetFloat(PREF_FLAME, bounce.maxFlame);
            bounce.flame = Mathf.Clamp(flame, 0f, bounce.maxFlame);
        }
    }

    // Útil si quieres borrar progreso (botón "New Game")
    public static void ClearSavedCheckpoint()
    {
        PlayerPrefs.DeleteKey(PREF_HAS);
        PlayerPrefs.DeleteKey(PREF_X);
        PlayerPrefs.DeleteKey(PREF_Y);
        PlayerPrefs.DeleteKey(PREF_FLAME);
        PlayerPrefs.DeleteKey(PREF_ID);
        PlayerPrefs.Save();
    }
}
