using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SectionTrigger : MonoBehaviour
{
    [Header("Identificador del evento de sección")]
    public string sectionId = "S01_EASY_ENTER";

    [Header("Punto de respawn de esta sección")]
    public Transform respawnPoint;   // <<< NUEVO

    [Header("Detección del jugador")]
    public string playerTag = "Player";

    private void Reset()
    {
        // Aseguramos que el collider sea trigger
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Solo reaccionamos al jugador
        if (!other.CompareTag(playerTag))
        {
            Debug.Log($"[SectionTrigger] {name}: ha entrado {other.name} con tag {other.tag}, ignorado (esperaba {playerTag}).");
            return;
        }

        // 1) Telemetría de sección + sección actual
        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.SetSection(sectionId);

            GameplayTelemetry.Instance.LogEvent(
                "SECTION_ENTER",
                other.transform.position,
                sectionId
            );
        }

        // 2) Buscar PlayerRespawn EN EL PADRE (no solo en el hijo)
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null)
        {
            Debug.LogWarning($"[SectionTrigger] {name}: {other.name} NO tiene PlayerRespawn en el padre.");
            return;
        }

        // 3) Determinar checkpoint
        Vector2 checkpoint = other.transform.position;

        if (respawnPoint != null)
            checkpoint = respawnPoint.position;

        Debug.Log($"[SectionTrigger] {name}: SetCheckpoint({checkpoint}) sectionId={sectionId}");

        respawn.SetCheckpoint(checkpoint);
    }
}
