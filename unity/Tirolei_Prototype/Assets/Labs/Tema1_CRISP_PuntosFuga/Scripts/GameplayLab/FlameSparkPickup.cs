using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlameSparkPickup : MonoBehaviour
{
    [Header("Spark Pickup")]
    public float windowDuration = 1.0f;
    public Transform anchorPoint;
    public bool destroyOnPickup = true;
    public float respawnSeconds = 0f;

    private SpriteRenderer sr;
    private Collider2D col;

    private bool available = true;

    // Trigger-wait-exit (solo para consumo por caminar)
    private bool waitingForExit = false;
    private Collider2D holderCol;

    private Coroutine respawnCo;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    public Vector2 GetAnchorWorld()
    {
        return anchorPoint != null
            ? (Vector2)anchorPoint.position
            : (Vector2)transform.position;
    }

    // =====================================================
    // TRIGGER NORMAL (caminar) -> espera Exit
    // =====================================================
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!available) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        var spark = rb.GetComponent<PlayerSparkBoost>();
        if (spark == null) return;

        // Si está en dash o ya tiene spark activo, el trigger NO gestiona nada
        if (spark.IsDashing() || spark.IsSparkActive()) return;

        holderCol = other;

        // CLAVE: AL CAMINAR NO HAY "PICKUP BOUNCE".
        // Solo activas la ventana de spark anclada.
        spark.ActivateSpark(windowDuration, GetAnchorWorld());

        Consume(waitForExit: true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!waitingForExit) return;
        if (other != holderCol) return;

        waitingForExit = false;
        holderCol = null;

        StartRespawn();
    }

    // =====================================================
    // DASH/X (rb.Cast) -> TU CÓDIGO LLAMA A ESTO
    // =====================================================
    public void Consume()
    {
        if (!available) return;
        Consume(waitForExit: false); // dash: NO esperar Exit (no va a ocurrir)
    }

    // =====================================================
    // NÚCLEO
    // =====================================================
    private void Consume(bool waitForExit)
    {
        if (!destroyOnPickup) return;

        available = false;

        if (sr != null)
            sr.enabled = false;

        if (respawnSeconds <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (waitForExit)
        {
            // Camino trigger: dejamos collider activo para que exista Exit
            waitingForExit = true;
            return;
        }

        // Camino dash: no existe Exit fiable en tu diseño (ancla/teleport)
        waitingForExit = false;
        holderCol = null;

        StartRespawn();
    }

    // =====================================================
    // RESPAWN
    // =====================================================
    private void StartRespawn()
    {
        if (respawnCo != null)
            StopCoroutine(respawnCo);

        respawnCo = StartCoroutine(RespawnAfterTime());
    }

    private IEnumerator RespawnAfterTime()
    {
        // Apaga collider durante cooldown para que no haya re-trigger fantasma
        if (col != null) col.enabled = false;

        yield return new WaitForSeconds(respawnSeconds);

        available = true;

        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;

        respawnCo = null;
    }
}
