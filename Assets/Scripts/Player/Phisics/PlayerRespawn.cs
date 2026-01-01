using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Checkpoint")]
    public bool useCheckpoint = true;

    [Tooltip("Opcional: si lo asignas, NEW usará este spawn en vez de la pos inicial del player.")]
    public Transform startSpawnOverride;

    [Tooltip("Checkpoint actual en runtime (el último tocado en esta sesión).")]
    public Transform currentCheckpoint;

    [Header("Run Mode (NEW vs CONTINUE) - solo afecta al SPAWN INICIAL")]
    [Tooltip("Si está activo, al iniciar en NEW ignorará el save (pero NO lo borra).")]
    public bool newGameIgnoresSavedCheckpointOnStart = true;

    public KeyCode newGameKey = KeyCode.F1;     // fuerza NEW (spawn en inicio)
    public KeyCode continueKey = KeyCode.C;     // fuerza CONTINUE (spawn en save si existe)

    [Header("Death Respawn")]
    [Tooltip("Si está activo, no recarga escena al morir; teleporta y listo.")]
    public bool respawnWithoutReload = true;

    [Tooltip("Si false, y NO hay checkpoint, recarga escena. Si true, teleporta a inicio igualmente.")]
    public bool teleportToInitialIfNoCheckpoint = true;

    [Header("Physics reset (Unity 6)")]
    public bool resetVelocityOnTeleport = true;

    // ===== PlayerPrefs keys =====
    private const string PREF_RUNMODE = "RUN_MODE"; // 0 NEW, 1 CONTINUE
    private const string PREF_HAS = "CP_HAS";
    private const string PREF_X = "CP_X";
    private const string PREF_Y = "CP_Y";
    private const string PREF_ID = "CP_ID";

    private Rigidbody2D rb;
    private Vector3 initialSpawnPos;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Posición inicial = donde esté el player en la escena (o override)
        initialSpawnPos = (startSpawnOverride != null) ? startSpawnOverride.position : transform.position;
    }

    private void Start()
    {
        ApplySpawnForSceneEntry();
    }

    private void Update()
    {
        // Teclas para decidir desde la propia TrainingArea (sin menú)
        if (Input.GetKeyDown(newGameKey))
        {
            SetRunMode(0);
            ApplySpawnForSceneEntry();
        }
        else if (Input.GetKeyDown(continueKey))
        {
            SetRunMode(1);
            ApplySpawnForSceneEntry();
        }
    }

    // =========================
    // API para tus checkpoints
    // =========================
    public void SetCheckpoint(Transform checkpoint)
    {
        if (!useCheckpoint) return;

        currentCheckpoint = checkpoint;
        Vector3 p = checkpoint != null ? checkpoint.position : initialSpawnPos;

        SaveCheckpointPrefs(checkpoint != null ? checkpoint.name : "NULL", p);

        Debug.Log($"[PlayerRespawn] SetCheckpoint -> {(checkpoint ? checkpoint.name : "NULL")} @ {p}");
    }

    public void SetCheckpoint(Vector3 worldPos)
    {
        if (!useCheckpoint) return;

        currentCheckpoint = null; // runtime no transform, pero guardamos posición
        SaveCheckpointPrefs("POS", worldPos);

        Debug.Log($"[PlayerRespawn] SetCheckpoint(Vector3) -> {worldPos}");
    }

    // =========================
    // Lo que tú quieres: MORIR => checkpoint si existe
    // =========================
    public void RespawnAfterDeath()
    {
        Vector3 target = GetBestRespawnPoint();

        // CLAVE: aunque hayas empezado NEW, si ya hay checkpoint, respawnea ahí.
        bool hasAnyCheckpoint = HasRuntimeCheckpoint() || HasSavedCheckpoint();

        if (respawnWithoutReload || hasAnyCheckpoint)
        {
            TeleportTo(target);
            Debug.Log($"[PlayerRespawn] RespawnAfterDeath -> {target} (hasCP={hasAnyCheckpoint})");
            return;
        }

        // Si no hay checkpoint y no quieres teleport directo:
        if (teleportToInitialIfNoCheckpoint)
        {
            TeleportTo(initialSpawnPos);
            Debug.Log($"[PlayerRespawn] RespawnAfterDeath -> INITIAL {initialSpawnPos} (no checkpoint)");
            return;
        }

        // Último recurso: recargar escena
        Debug.Log("[PlayerRespawn] RespawnAfterDeath -> Reload scene (no checkpoint)");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // =========================
    // Spawn al ENTRAR a la escena (NEW vs CONTINUE)
    // =========================
    private void ApplySpawnForSceneEntry()
    {
        int mode = GetRunMode(); // 0 NEW, 1 CONTINUE

        if (mode == 1)
        {
            // CONTINUE: si hay save, úsalo; si no, inicio
            Vector3 p = HasSavedCheckpoint() ? GetSavedCheckpointPosition() : initialSpawnPos;
            TeleportTo(p);
            Debug.Log($"[PlayerRespawn] Start spawn (CONTINUE) -> {p} (hasSave={HasSavedCheckpoint()})");
        }
        else
        {
            // NEW: inicio SIEMPRE (aunque exista save), pero NO se borra el save.
            // Eso permite: empezar en Unity y, si coges checkpoint en esta run, morir => checkpoint.
            if (newGameIgnoresSavedCheckpointOnStart)
            {
                TeleportTo(initialSpawnPos);
                Debug.Log($"[PlayerRespawn] Start spawn (NEW) -> {initialSpawnPos} (save ignored on start)");
            }
            else
            {
                // Si quieres permitir que NEW también use save (normalmente no lo quieres)
                Vector3 p = HasSavedCheckpoint() ? GetSavedCheckpointPosition() : initialSpawnPos;
                TeleportTo(p);
                Debug.Log($"[PlayerRespawn] Start spawn (NEW but allowed save) -> {p}");
            }
        }
    }

    // =========================
    // Helpers
    // =========================
    private Vector3 GetBestRespawnPoint()
    {
        // Prioridad: checkpoint runtime > checkpoint guardado > inicio
        if (HasRuntimeCheckpoint()) return currentCheckpoint.position;
        if (HasSavedCheckpoint()) return GetSavedCheckpointPosition();
        return initialSpawnPos;
    }

    private bool HasRuntimeCheckpoint()
    {
        return useCheckpoint && currentCheckpoint != null;
    }

    private bool HasSavedCheckpoint()
    {
        return useCheckpoint && PlayerPrefs.GetInt(PREF_HAS, 0) == 1;
    }

    private Vector3 GetSavedCheckpointPosition()
    {
        float x = PlayerPrefs.GetFloat(PREF_X, initialSpawnPos.x);
        float y = PlayerPrefs.GetFloat(PREF_Y, initialSpawnPos.y);
        return new Vector3(x, y, transform.position.z);
    }

    private void SaveCheckpointPrefs(string id, Vector3 p)
    {
        PlayerPrefs.SetInt(PREF_HAS, 1);
        PlayerPrefs.SetFloat(PREF_X, p.x);
        PlayerPrefs.SetFloat(PREF_Y, p.y);
        PlayerPrefs.SetString(PREF_ID, id);
        PlayerPrefs.Save();
    }

    public void ClearSavedCheckpoint()
    {
        PlayerPrefs.DeleteKey(PREF_HAS);
        PlayerPrefs.DeleteKey(PREF_X);
        PlayerPrefs.DeleteKey(PREF_Y);
        PlayerPrefs.DeleteKey(PREF_ID);
        PlayerPrefs.Save();

        currentCheckpoint = null;

        Debug.Log("[PlayerRespawn] ClearSavedCheckpoint()");
    }

    private int GetRunMode()
    {
        return PlayerPrefs.GetInt(PREF_RUNMODE, 0);
    }

    private void SetRunMode(int mode)
    {
        PlayerPrefs.SetInt(PREF_RUNMODE, mode);
        PlayerPrefs.Save();
        Debug.Log("[PlayerRespawn] SetRunMode -> " + mode);
    }

    private void TeleportTo(Vector3 worldPos)
    {
        if (rb != null)
        {
            // Unity 6: velocity obsoleto => linearVelocity
            rb.position = worldPos;

            if (resetVelocityOnTeleport)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
        else
        {
            transform.position = worldPos;
        }
    }

    // Compatibilidad con código antiguo que llama TriggerGameOver()
    public void TriggerGameOver()
    {
        RespawnAfterDeath();
    }

}
