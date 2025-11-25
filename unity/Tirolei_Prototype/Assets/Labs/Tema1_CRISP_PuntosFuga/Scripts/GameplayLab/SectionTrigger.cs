using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SectionTrigger : MonoBehaviour
{
    [Header("Identificador del evento de sección")]
    public string sectionId = "S01_EASY_ENTER";

    [Header("Detección del jugador")]
    public string playerTag = "Player";

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        Debug.Log($"[SectionTrigger] Enter {sectionId}");

        if (GameplayTelemetry.Instance != null)
        {
            GameplayTelemetry.Instance.LogEvent(
                "SECTION_ENTER",
                other.transform.position,
                sectionId
            );
        }
        else
        {
            Debug.LogWarning("[SectionTrigger] No hay GameplayTelemetry.Instance en la escena.");
        }
    }
}
