using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("Invulnerability (after hit)")]
    [Tooltip("Tiempo de gracia tras recibir daño (segundos).")]
    public bool useIFrames = true;
    public float iFrameDuration = 0.75f; // <- SUBE ESTO para más margen
    private float iFrameTimer = 0f;

    [Header("Respect Bounce Invincibility")]
    public bool respectBounceInvincibility = true;

    [Header("Hazard detection")]
    [Tooltip("If set (non-zero), hazards can be detected by layer.")]
    public LayerMask hazardLayers;
    [Tooltip("Fallback tag if hazardLayers is empty. Default: 'Hazard'.")]
    public string hazardTag = "Hazard";
    [Tooltip("Damage tick interval while staying in hazard. If 0, only damage on Enter.")]
    public float hazardTickInterval = 0.35f;
    [Tooltip("Damage applied if the Hazard object doesn't provide its own damage component.")]
    public int defaultHazardDamage = 1;

    private float hazardTickTimer = 0f;
    private bool touchingHazard = false;
    private int lastHazardDamage = 1;

    private PlayerRespawn respawn;
    private PlayerBounceAttack bounceAttack;

    private void Awake()
    {
        currentHealth = maxHealth;

        respawn = GetComponent<PlayerRespawn>();
        bounceAttack = GetComponent<PlayerBounceAttack>();

        if (respawn == null)
            Debug.LogWarning("[PlayerHealth] No PlayerRespawn found. GameOver won't trigger.");
    }

    private void Update()
    {
        // I-Frames cuentan incluso si pausarás timeScale (si lo haces)
        if (useIFrames && iFrameTimer > 0f)
            iFrameTimer -= Time.unscaledDeltaTime;

        // Hazard tick (si está dentro)
        if (touchingHazard && hazardTickInterval > 0f && currentHealth > 0)
        {
            hazardTickTimer -= Time.unscaledDeltaTime;
            if (hazardTickTimer <= 0f)
            {
                hazardTickTimer = hazardTickInterval;
                TryTakeDamage(lastHazardDamage);
            }
        }
    }

    /// <summary>
    /// Entry point: returns true if damage was actually applied.
    /// </summary>
    public bool TryTakeDamage(int dmg)
    {
        if (currentHealth <= 0) return false;

        // Respeta invencibilidad del bounce
        if (respectBounceInvincibility && bounceAttack != null && bounceAttack.isInvincible)
            return false;

        // I-Frames (gracia tras golpe)
        if (useIFrames && iFrameTimer > 0f)
            return false;

        dmg = Mathf.Max(0, dmg);
        if (dmg <= 0) return false;

        currentHealth -= dmg;
        Debug.Log($"[PlayerHealth] Damage: {dmg} -> HP: {currentHealth}");

        // Activa i-frames al recibir daño
        if (useIFrames)
            iFrameTimer = iFrameDuration;

        if (currentHealth <= 0)
            Die();

        return true;
    }

    // Compatibilidad
    public void TakeDamage(int dmg) => TryTakeDamage(dmg);

    private void Die()
    {
        Debug.Log("[PlayerHealth] PLAYER DEAD");
        if (respawn != null)
            respawn.TriggerGameOver();
    }

    public void ResetHealthFull()
    {
        currentHealth = maxHealth;
        iFrameTimer = 0f;

        touchingHazard = false;
        hazardTickTimer = 0f;
        lastHazardDamage = defaultHazardDamage;
    }

    public bool IsAlive() => currentHealth > 0;

    // =========================
    // Hazard detection (Trigger)
    // =========================
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsHazard(other.gameObject)) return;

        int dmg = ReadDamageOrDefault(other.gameObject);
        lastHazardDamage = dmg;

        Debug.Log($"[PlayerHealth] Trigger ENTER Hazard '{other.name}' dmg={dmg}");

        // Daño instantáneo al entrar
        TryTakeDamage(dmg);

        // Tick si está habilitado
        if (hazardTickInterval > 0f)
        {
            touchingHazard = true;
            hazardTickTimer = hazardTickInterval;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsHazard(other.gameObject)) return;

        Debug.Log($"[PlayerHealth] Trigger EXIT Hazard '{other.name}'");

        touchingHazard = false;
        hazardTickTimer = 0f;
        lastHazardDamage = defaultHazardDamage;
    }

    // ============================
    // Hazard detection (Collision)
    // ============================
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsHazard(collision.gameObject)) return;

        int dmg = ReadDamageOrDefault(collision.gameObject);
        lastHazardDamage = dmg;

        Debug.Log($"[PlayerHealth] Collision ENTER Hazard '{collision.gameObject.name}' dmg={dmg}");

        TryTakeDamage(dmg);

        if (hazardTickInterval > 0f)
        {
            touchingHazard = true;
            hazardTickTimer = hazardTickInterval;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!IsHazard(collision.gameObject)) return;

        Debug.Log($"[PlayerHealth] Collision EXIT Hazard '{collision.gameObject.name}'");

        touchingHazard = false;
        hazardTickTimer = 0f;
        lastHazardDamage = defaultHazardDamage;
    }

    // ============================
    // Helpers
    // ============================
    private bool IsHazard(GameObject go)
    {
        // Layer mask si está configurado
        if (hazardLayers.value != 0)
        {
            int layerBit = 1 << go.layer;
            if ((hazardLayers.value & layerBit) != 0) return true;
        }

        // Tag fallback
        if (!string.IsNullOrEmpty(hazardTag) && go.CompareTag(hazardTag))
            return true;

        return false;
    }

    private int ReadDamageOrDefault(GameObject go)
    {
        // Si luego metes componente HazardDamage, lo conectas aquí.
        return defaultHazardDamage;
    }
}
