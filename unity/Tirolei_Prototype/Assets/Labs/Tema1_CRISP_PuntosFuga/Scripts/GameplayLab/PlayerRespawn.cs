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

    [Tooltip("Margen mínimo desde el borde de la plataforma")]
    public float platformEdgeMargin = 0.5f;

    // 0 = inicio de la plataforma (lado izquierdo), 1 = final (lado derecho)
    [Range(0f, 1f)]
    public float platformRespawnAnchor = 0.15f;

    [Tooltip("Normal mínima para considerar que estamos ENCIMA del suelo")]
    public float minGroundNormalY = 0.7f;

    private Rigidbody2D rb;
    private bool levelFinished = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        initialRespawnPoint = transform.position;
        currentRespawnPoint = initialRespawnPoint;
    }

    /// <summary>
    /// Checkpoint manual (p.ej. S01, S02, S03, S04…).
    /// </summary>
    public void SetCheckpoint(Vector2 newPoint)
    {
        currentRespawnPoint = newPoint;
        initialRespawnPoint = newPoint;

        Debug.Log($"[PlayerRespawn] Checkpoint MANUAL -> {currentRespawnPoint}");
    }

    /// <summary>
    /// Actualiza el checkpoint automáticamente cuando estás pisando suelo válido.
    /// Siempre reaparece en el "inicio" de la plataforma actual.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!useAutoGroundCheckpoint) return;

        // Solo reaccionar a suelo
        if (!collision.collider.CompareTag(groundTag))
            return;

        foreach (var contact in collision.contacts)
        {
            // Normal suficientemente hacia arriba = estamos ENCIMA de la plataforma
            if (contact.normal.y >= minGroundNormalY)
            {
                Collider2D groundCol = collision.collider;
                Bounds b = groundCol.bounds;

                // 1) límites seguros
                float minX = b.min.x + platformEdgeMargin;
                float maxX = b.max.x - platformEdgeMargin;

                // 2) X de respawn fija para ESTA plataforma:
                //    0 = casi principio, 0.5 = centro, 1 = casi final
                float respawnX = Mathf.Lerp(minX, maxX, platformRespawnAnchor);

                Vector2 safe;
                safe.x = respawnX;
                safe.y = b.max.y + groundRespawnYOffset;

                currentRespawnPoint = safe;

                // Debug opcional:
                // Debug.Log($"[PlayerRespawn] Auto checkpoint en {currentRespawnPoint} para {groundCol.name}");

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
