using UnityEngine;

[RequireComponent(typeof(Transform))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Tags de detección")]
    public string hazardTag = "Hazard";
    public string goalTag = "Goal";
    public string groundTag = "Ground";   // para detectar suelo seguro

    [Header("Respawn")]
    [Tooltip("Solo informativo en el inspector")]
    public Vector2 initialRespawnPoint;
    public Vector2 currentRespawnPoint;

    [Header("Respawn automático en último suelo")]
    public bool useAutoGroundCheckpoint = true;
    [Tooltip("Offset vertical para evitar reaparecer clavado en el suelo")]
    public float groundRespawnYOffset = 0.1f;
    [Range(0f, 1f)]
    public float minGroundNormalY = 0.7f;  // normal claramente hacia arriba

    private Rigidbody2D rb;
    private bool levelFinished = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        initialRespawnPoint = transform.position;
        currentRespawnPoint = initialRespawnPoint;
    }

    /// <summary>
    /// Llamado cuando entras en una nueva sección (S01, S02, S03, S04…).
    /// Sigue existiendo POR SI los triggers están bien colocados.
    /// </summary>
    public void SetCheckpoint(Vector2 newPoint)
    {
        currentRespawnPoint = newPoint;
        initialRespawnPoint = newPoint;

        Debug.Log($"[PlayerRespawn] Checkpoint MANUAL -> {currentRespawnPoint}");
    }

    /// <summary>
    /// Actualiza el checkpoint automáticamente cuando estás pisando suelo válido.
    /// Esto hace que el respawn vaya a la última plataforma en la que estabas,
    /// aunque los SectionTrigger fallen.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!useAutoGroundCheckpoint) return;

        if (!collision.collider.CompareTag(groundTag))
            return;

        // Buscamos contactos que sean "suelo" (normales hacia arriba)
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y >= minGroundNormalY)
            {
                Vector2 safe = contact.point;
                safe.y += groundRespawnYOffset;

                currentRespawnPoint = safe;

                // Opcional: comentar si te molesta el spam
                // Debug.Log($"[PlayerRespawn] Checkpoint AUTO -> {currentRespawnPoint}");
                break;
            }
        }
    }

    /// <summary>
    /// Teletransporte al último punto seguro.
    /// </summary>
    public void Respawn()
    {
        if (levelFinished) return;

        Debug.Log($"[PlayerRespawn] Respawn en {currentRespawnPoint}");

        transform.position = currentRespawnPoint;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Evento de RESPAWN
        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogEvent(
                "RESPAWN",
                currentRespawnPoint,
                ""
            );
        }
    }

    /// <summary>
    /// Llamado al tocar la meta.
    /// </summary>
    public void FinishLevel()
    {
        if (levelFinished) return;
        levelFinished = true;

        Debug.Log("[PlayerRespawn] GOAL alcanzado. Nivel completado.");

        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogEvent(
                "GOAL_REACHED",
                transform.position,
                ""
            );
        }

        // Desactivar control del jugador
        var controller = GetComponent<PlayerSimpleController>();
        if (controller != null) controller.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        Time.timeScale = 0f;
    }

    // COLLIDER FÍSICO
    private void OnCollisionEnter2D(Collision2D collision)
    {
        string otherTag = collision.collider.tag;

        if (otherTag == hazardTag)
        {
            // Evento de MUERTE
            if (GameplayTelemetry.Instance != null)
            {
                GameplayTelemetry.Instance.LogEvent(
                    "DEATH_HAZARD",
                    transform.position,
                    ""
                );
            }

            Respawn();
        }
        else if (otherTag == goalTag)
        {
            FinishLevel();
        }
    }

    // TRIGGER
    private void OnTriggerEnter2D(Collider2D other)
    {
        string otherTag = other.tag;

        if (otherTag == hazardTag)
        {
            if (GameplayTelemetry.Instance != null)
            {
                GameplayTelemetry.Instance.LogEvent(
                    "DEATH_HAZARD",
                    transform.position,
                    ""
                );
            }

            Respawn();
        }
        else if (otherTag == goalTag)
        {
            FinishLevel();
        }
    }
}
