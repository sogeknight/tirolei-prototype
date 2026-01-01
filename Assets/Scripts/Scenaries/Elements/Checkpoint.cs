using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint")]
    public string checkpointId = "CP_01";
    public bool refillFlame = true;

    [Header("Opcional: feedback")]
    public AudioSource sfx;
    public GameObject activateVfx;

    // Para pruebas: NO bloquees con activated hasta que funcione al 100%
    // private bool activated = false;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1) Encuentra al player controller (esto ya te funciona porque "guarda")
        var player = other.GetComponentInParent<PlayerCheckpointController>();
        if (player == null) return;

        // 2) Set checkpoint
        player.SetCheckpoint(transform.position);

        // 3) RECARGA usando la referencia CANÓNICA del player
        if (refillFlame)
        {
            if (player.bounce == null)
                player.bounce = player.GetComponent<PlayerBounceAttack>();

            if (player.bounce != null)
            {
                float before = player.bounce.flame;
                player.bounce.flame = player.bounce.maxFlame;

                Debug.Log($"[Checkpoint] Refill OK | bounceID={player.bounce.GetInstanceID()} | {before:0.##} -> {player.bounce.flame:0.##}");
            }
            else
            {
                Debug.LogWarning("[Checkpoint] ERROR: PlayerCheckpointController no tiene referencia a PlayerBounceAttack.");
            }
        }

        // 4) Guarda DESPUÉS de recargar
        player.SaveCheckpointToPrefs(checkpointId, transform.position);


        if (sfx) sfx.Play();
        if (activateVfx) activateVfx.SetActive(true);
    }
}
