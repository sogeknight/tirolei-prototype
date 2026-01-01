using UnityEngine;
using System.Collections;

public class PlayerCheckpointController : MonoBehaviour
{
    [Header("Refs (optional)")]
    public PlayerBounceAttack bounce;
    public Rigidbody2D rb;

    [Header("Runtime")]
    public Vector2 currentCheckpointPos;

    [Header("Spawn (Initial)")]
    [Tooltip("Spawn inicial capturado al arrancar (solo informativo).")]
    public Vector2 initialSpawnPos;

    [Header("Load behavior")]
    [Tooltip("Si está activo, al entrar en Play y existir checkpoint guardado, teleporta automáticamente.")]
    public bool autoLoadOnStart = true;

    [Tooltip("Si autoLoadOnStart está activo, espera 1 frame para no pelearte con otros scripts en Start().")]
    public bool delayLoadOneFrame = true;

    [Header("Debug / Inspector controls")]
    [Tooltip("Si true, IGNORA el checkpoint guardado y empieza desde initialSpawnPos (solo esta sesión).")]
    public bool forceStartFromInitialSpawn = false;

    [Tooltip("Marca true en Play para borrar el checkpoint guardado (se auto-desmarca).")]
    public bool debugResetSavedCheckpoint = false;

    [Tooltip("Marca true en Play para teletransportar YA a initialSpawnPos (se auto-desmarca).")]
    public bool applyStartSpawnNow = false;

    private const string PREF_HAS   = "CP_HAS";
    private const string PREF_X     = "CP_X";
    private const string PREF_Y     = "CP_Y";
    private const string PREF_FLAME = "CP_FLAME";
    private const string PREF_ID    = "CP_ID";

    private bool initialSpawnCaptured = false;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!bounce) bounce = GetComponent<PlayerBounceAttack>();
    }

    private void Start()
    {
        CaptureInitialSpawnOnce();

        if (!autoLoadOnStart) return;

        if (delayLoadOneFrame) StartCoroutine(LoadNextFrame());
        else LoadCheckpointOrSpawn();
    }

    private void Update()
    {
        // Debug: reset persistido
        if (debugResetSavedCheckpoint)
        {
            debugResetSavedCheckpoint = false;
            ClearSavedCheckpoint();
            // y opcionalmente resetea runtime al spawn inicial
            currentCheckpointPos = initialSpawnPos;
        }

        // Debug: aplicar spawn inicial ahora
        if (applyStartSpawnNow)
        {
            applyStartSpawnNow = false;
            currentCheckpointPos = initialSpawnPos;
            TeleportTo(initialSpawnPos);
        }
    }

    private void CaptureInitialSpawnOnce()
    {
        if (initialSpawnCaptured) return;

        initialSpawnPos = (rb != null) ? rb.position : (Vector2)transform.position;
        currentCheckpointPos = initialSpawnPos;

        initialSpawnCaptured = true;
    }

    private IEnumerator LoadNextFrame()
    {
        yield return null;
        LoadCheckpointOrSpawn();
    }

    /// <summary>
    /// Política de spawn:
    /// - Si forceStartFromInitialSpawn: spawn inicial
    /// - Si hay checkpoint guardado: checkpoint
    /// - Si no: spawn inicial
    /// </summary>
    private void LoadCheckpointOrSpawn()
    {
        if (forceStartFromInitialSpawn)
        {
            currentCheckpointPos = initialSpawnPos;
            TeleportTo(initialSpawnPos);
            return;
        }

        if (PlayerPrefs.GetInt(PREF_HAS, 0) == 1)
        {
            LoadCheckpointFromPrefsIfAny();
            return;
        }

        // No hay guardado
        currentCheckpointPos = initialSpawnPos;
        TeleportTo(initialSpawnPos);
    }

    // Si tu checkpoint te llama con SetCheckpoint:
    public void SetCheckpoint(Vector2 pos)
    {
        currentCheckpointPos = pos;
    }

    public void RefillPower()
    {
        if (bounce == null) return;
        bounce.flame = bounce.maxFlame;
    }

    // Forma robusta: el checkpoint te pasa la posición REAL a guardar.
    public void SaveCheckpointToPrefs(string checkpointId, Vector2 checkpointPos)
    {
        currentCheckpointPos = checkpointPos;

        PlayerPrefs.SetInt(PREF_HAS, 1);
        PlayerPrefs.SetFloat(PREF_X, currentCheckpointPos.x);
        PlayerPrefs.SetFloat(PREF_Y, currentCheckpointPos.y);
        PlayerPrefs.SetString(PREF_ID, checkpointId);

        if (bounce != null)
            PlayerPrefs.SetFloat(PREF_FLAME, bounce.flame);

        PlayerPrefs.Save();
    }

    // Compatibilidad por si algún checkpoint viejo llama sin pos.
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

    // Llama a esto cuando MUERES o al pulsar "Continue"
    public void LoadCheckpointFromPrefsIfAny()
    {
        if (PlayerPrefs.GetInt(PREF_HAS, 0) != 1) return;

        float x = PlayerPrefs.GetFloat(PREF_X, initialSpawnPos.x);
        float y = PlayerPrefs.GetFloat(PREF_Y, initialSpawnPos.y);

        currentCheckpointPos = new Vector2(x, y);

        TeleportTo(currentCheckpointPos);

        if (bounce != null)
        {
            float savedFlame = PlayerPrefs.GetFloat(PREF_FLAME, bounce.maxFlame);
            bounce.flame = Mathf.Clamp(savedFlame, 0f, bounce.maxFlame);
        }
    }

    public void RespawnToCurrentCheckpoint()
    {
        // Si hay checkpoint guardado, SIEMPRE úsalo para respawn (aunque hayas arrancado en NEW).
        if (PlayerPrefs.GetInt(PREF_HAS, 0) == 1)
        {
            float x = PlayerPrefs.GetFloat(PREF_X, currentCheckpointPos.x);
            float y = PlayerPrefs.GetFloat(PREF_Y, currentCheckpointPos.y);
            currentCheckpointPos = new Vector2(x, y);
        }

        TeleportTo(currentCheckpointPos);

        if (bounce != null && PlayerPrefs.GetInt(PREF_HAS, 0) == 1)
        {
            float savedFlame = PlayerPrefs.GetFloat(PREF_FLAME, bounce.maxFlame);
            bounce.flame = Mathf.Clamp(savedFlame, 0f, bounce.maxFlame);
        }
    }


    public void TeleportTo(Vector2 pos)
    {
        // 1) Apaga bounce 1 frame SIEMPRE que teletransportas.
        if (bounce != null && bounce.enabled)
            StartCoroutine(DisableBounceOneFrame());

        // 2) Corta físicas y coloca
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = pos;
        }
        else
        {
            transform.position = pos;
        }
    }

    private IEnumerator DisableBounceOneFrame()
    {
        bounce.enabled = false;
        yield return null;
        if (bounce != null) bounce.enabled = true;
    }

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
