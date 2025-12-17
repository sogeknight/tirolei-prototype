using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlameSparkPickup : MonoBehaviour
{
    [Header("Spark Pickup")]
    public float windowDuration = 1.0f;

    [Tooltip("Si es null, el ancla es transform.position")]
    public Transform anchorPoint;

    [Tooltip("Si está activo, el pickup desaparece tras recogerlo.")]
    public bool destroyOnPickup = true;

    [Tooltip("Respawn simple por tiempo (opcional). Si 0, no respawnea.")]
    public float respawnSeconds = 0f;

    private SpriteRenderer sr;
    private Collider2D col;

    private bool available = true;
    private Coroutine respawnRoutine;

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

    // =========================================================
    // CAMINO 1: consumo desde DASH (rb.Cast)
    // =========================================================
    // PlayerSparkBoost llama a ESTE método
    public void Consume()
    {
        ConsumeInternal(null);
    }

    // =========================================================
    // CAMINO 2: consumo por TRIGGER
    // =========================================================
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!available) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        var spark = rb.GetComponent<PlayerSparkBoost>();
        if (spark == null) return;

        // Durante dash, el pickup lo gestiona el dash por cast
        if (spark.IsDashing()) return;

        Vector2 anchor = GetAnchorWorld();

        spark.NotifyPickupBounce();
        spark.ActivateSpark(windowDuration, anchor);

        ConsumeInternal(other);
    }

    // =========================================================
    // LÓGICA REAL DE CONSUMO
    // =========================================================
    private void ConsumeInternal(Collider2D collectorCollider)
    {
        if (!destroyOnPickup) return;

        available = false;

        // Oculta y APAGA collider para evitar redisparos
        if (sr != null) sr.enabled = false;
        if (col != null) col.enabled = false;

        if (respawnSeconds > 0f)
        {
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);

            // Si venimos de trigger → esperar a que salga
            if (collectorCollider != null)
                respawnRoutine = StartCoroutine(RespawnAfterLeaving(collectorCollider));
            else
                // Si venimos de dash → no hay collider, empieza ya
                respawnRoutine = StartCoroutine(RespawnAfterSeconds(respawnSeconds));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // =========================================================
    // COROUTINES
    // =========================================================
    private IEnumerator RespawnAfterLeaving(Collider2D collector)
    {
        // Espera a que el player NO solape el pickup
        while (collector != null && col != null &&
               collector.bounds.Intersects(col.bounds))
        {
            yield return null;
        }

        yield return new WaitForSeconds(respawnSeconds);
        Respawn();
    }

    private IEnumerator RespawnAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Respawn();
    }

    // =========================================================
    // RESPAWN
    // =========================================================
    private void Respawn()
    {
        available = true;

        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;

        respawnRoutine = null;
    }
}
