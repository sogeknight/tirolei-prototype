// using System.Collections;
// using UnityEngine;

// [RequireComponent(typeof(Collider2D))]
// public class FlameSparkPickup : MonoBehaviour
// {
//     [Header("Spark Pickup")]
//     public float windowDuration = 1.0f;

//     [Tooltip("Si es null, el ancla es transform.position")]
//     public Transform anchorPoint;

//     [Tooltip("Si está activo, el pickup desaparece tras recogerlo.")]
//     public bool destroyOnPickup = true;

//     [Tooltip("Respawn simple por tiempo (opcional). Si 0, no respawnea.")]
//     public float respawnSeconds = 0f;

//     private SpriteRenderer sr;
//     private Collider2D col;

//     private bool available = true;
//     private Coroutine respawnCo;

//     private void Awake()
//     {
//         sr = GetComponent<SpriteRenderer>();
//         col = GetComponent<Collider2D>();
//         col.isTrigger = true;
//     }

//     public Vector2 GetAnchorWorld()
//     {
//         return anchorPoint != null ? (Vector2)anchorPoint.position : (Vector2)transform.position;
//     }

//     private void OnTriggerEnter2D(Collider2D other)
//     {
//         if (!available) return;

//         // Busca el core en el objeto que entra (player) o en su parent
//         var core = other.GetComponent<PlayerSparkBoostCore>();
//         if (core == null) core = other.GetComponentInParent<PlayerSparkBoostCore>();
//         if (core == null) return;

//         available = false;

//         Vector2 anchor = GetAnchorWorld();

//         // activa ventana spark
//         core.ActivateSpark(windowDuration, anchor);

//         // rebote físico opcional (para evitar auto-dash y que “se sienta”)
//         core.NotifyPickupBounce();

//         // consume visual
//         Consume();

//         // respawn
//         if (!destroyOnPickup && respawnSeconds > 0f)
//         {
//             if (respawnCo != null) StopCoroutine(respawnCo);
//             respawnCo = StartCoroutine(RespawnAfterTime());
//         }
//         else if (destroyOnPickup)
//         {
//             // si destruyes, destruye
//             Destroy(gameObject);
//         }
//     }

//     public void Consume()
//     {
//         if (sr != null) sr.enabled = false;
//         if (col != null) col.enabled = false;
//     }

//     private IEnumerator RespawnAfterTime()
//     {
//         yield return new WaitForSeconds(respawnSeconds);

//         available = true;

//         if (sr != null) sr.enabled = true;
//         if (col != null) col.enabled = true;

//         respawnCo = null;
//     }
// }
