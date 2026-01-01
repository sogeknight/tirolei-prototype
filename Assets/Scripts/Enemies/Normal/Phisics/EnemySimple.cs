using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemySimple : MonoBehaviour, IBounceImpactReceiver, IPiercingBounceReceiver

{
    [Header("Vida")]
    [SerializeField] private int maxHealth = 80;   // editable en Inspector
    [SerializeField] private int currentHealth;    // visible (debug) en Inspector, pero no tocable desde fuera


    [Header("Daño al jugador")]
    public int contactDamage = 1;

    [Header("Objetivo")]
    public Transform target;
    public float maxChaseDistance = 20f;

    [Header("Persecución (locomoción)")]
    public float moveSpeed = 4f;
    public float stopDistance = 0.6f;

    [Header("Suelo / Obstáculos")]
    public LayerMask groundLayers;
    [Tooltip("Si lo dejas en 0, usará groundLayers.")]
    public LayerMask obstacleLayers;

    [Tooltip("0.12–0.30 suele ir bien.")]
    public float groundRayDistance = 0.22f;

    [Header("Detección pared (BoxCast)")]
    [Tooltip("Distancia hacia delante para detectar pared.")]
    public float wallCheckDistance = 0.60f;
    [Tooltip("Altura del box desde los pies (en mundo).")]
    public float wallCheckFeetOffset = 0.10f;
    [Tooltip("Multiplicador del tamaño del box respecto al collider.")]
    public float wallBoxWidthFactor = 0.40f;
    public float wallBoxHeightFactor = 0.35f;

    [Header("Salto de obstáculo")]
    [Tooltip("VELOCIDAD Y. 8–14 normalmente.")]
    public float obstacleJumpVelocity = 10f;
    [Tooltip("Cooldown anti-spam (segundos).")]
    public float jumpCooldown = 0.20f;

    [Header("Dash (ataque opcional)")]
    public bool useDashAttack = true;
    public float dashTriggerMinDistance = 1.5f;
    public float dashTriggerMaxDistance = 8f;
    public float chargeTime = 0.25f;
    public float dashSpeed = 12f;
    public float dashDuration = 0.25f;
    public float attackCooldown = 1.2f;

    [Header("Rigidbody overrides (opcional)")]
    public bool overrideRigidbodySettings = false;
    public float overrideGravityScale = 5f;
    public float overrideLinearDamping = 0f;

    [Header("Debug")]
    public bool debugCasts = false;

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector3 baseScale;

    private float jumpCooldownTimer = 0f;
    private float attackCooldownTimer = 0f;

    private enum State { Chase, Charging, Dashing, Recover }
    private State state = State.Chase;

    private float stateTimer = 0f;
    private float dashDir = 1f;

    [Header("Anti-hop (evita saltos tontos)")]
    public float blockedSpeedThreshold = 0.25f; // si va más lento que esto, está bloqueado
    public float blockedGraceTime = 0.08f;      // cuánto tiempo debe estar bloqueado antes de saltar
    private float blockedTimer = 0f;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (overrideRigidbodySettings)
        {
            rb.gravityScale = overrideGravityScale;
            rb.linearDamping = overrideLinearDamping;
        }

        baseScale = transform.localScale;

        // Si no has seteado obstacleLayers, usa groundLayers por defecto
        if (obstacleLayers.value == 0)
            obstacleLayers = groundLayers;

        currentHealth = maxHealth;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        // Timers
        float dt = Time.fixedDeltaTime;
        if (jumpCooldownTimer > 0f) jumpCooldownTimer -= dt;
        if (attackCooldownTimer > 0f) attackCooldownTimer -= dt;

        float dx = target.position.x - transform.position.x;
        float adx = Mathf.Abs(dx);

        bool grounded = IsGroundedSimple();
        float dir = (dx >= 0f) ? 1f : -1f;

        switch (state)
        {
            case State.Chase:
            {
                // Fuera de rango => no hacer nada
                if (adx > maxChaseDistance)
                {
                    SetVelX(0f);
                    return;
                }

                // Orientación visual
                FlipVisual(dir);

                // Si cerca, parar
                if (adx <= stopDistance)
                {
                    SetVelX(0f);
                }
                else
                {
                    // Si hay pared delante y estamos grounded, saltar
                    // medir si está bloqueado de verdad (no saltar por tocar una esquina)
                    float desiredX = dir * moveSpeed;
                    float actualX = Mathf.Abs(rb.linearVelocity.x);

                    bool tryingToMove = adx > stopDistance;
                    bool wallAhead = HasWallAheadBox(dir);

                    if (grounded && tryingToMove && wallAhead)
                    {
                        // si no está avanzando de verdad, acumula tiempo bloqueado
                        if (actualX < blockedSpeedThreshold)
                            blockedTimer += dt;
                        else
                            blockedTimer = 0f;

                        if (jumpCooldownTimer <= 0f && blockedTimer >= blockedGraceTime)
                        {
                            JumpUp();
                            jumpCooldownTimer = jumpCooldown;
                            blockedTimer = 0f;
                        }
                    }
                    else
                    {
                        blockedTimer = 0f;
}


                    // Mover hacia el player
                    SetVelX(dir * moveSpeed);
                }

                // Dash como ataque (opcional, solo si grounded y en rango)
                if (useDashAttack && grounded && attackCooldownTimer <= 0f &&
                    adx >= dashTriggerMinDistance && adx <= dashTriggerMaxDistance)
                {
                    dashDir = dir;
                    state = State.Charging;
                    stateTimer = chargeTime;
                    SetVelX(0f);
                }
            }
            break;

            case State.Charging:
            {
                stateTimer -= dt;
                SetVelX(0f);

                if (stateTimer <= 0f)
                {
                    state = State.Dashing;
                    stateTimer = dashDuration;

                    // Impulso horizontal del dash
                    Vector2 v = rb.linearVelocity;
                    v.x = dashDir * dashSpeed;
                    rb.linearVelocity = v;
                }
            }
            break;

            case State.Dashing:
            {
                stateTimer -= dt;

                // Mantener dash X
                Vector2 v = rb.linearVelocity;
                v.x = dashDir * dashSpeed;
                rb.linearVelocity = v;

                // Si se estrella con pared durante dash, saltar (una vez cada cooldown)
                if (grounded && jumpCooldownTimer <= 0f && HasWallAheadBox(dashDir))
                {
                    JumpUp();
                    jumpCooldownTimer = jumpCooldown;
                }

                if (stateTimer <= 0f)
                {
                    state = State.Recover;
                    stateTimer = 0.12f;
                    attackCooldownTimer = attackCooldown;
                }
            }
            break;

            case State.Recover:
            {
                stateTimer -= dt;
                SetVelX(0f);
                if (stateTimer <= 0f)
                    state = State.Chase;
            }
            break;
        }
    }

    // =========================
    // Movimiento / Física
    // =========================
    private void SetVelX(float x)
    {
        Vector2 v = rb.linearVelocity;
        v.x = x;
        rb.linearVelocity = v;
    }

    private void JumpUp()
    {
        Vector2 v = rb.linearVelocity;
        // No machacar si ya va más rápido hacia arriba
        v.y = Mathf.Max(v.y, obstacleJumpVelocity);
        rb.linearVelocity = v;
    }

    private bool IsGroundedSimple()
    {
        Bounds b = col.bounds;
        Vector2 origin = new Vector2(b.center.x, b.min.y + 0.02f);
        float dist = Mathf.Max(0.05f, groundRayDistance);

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, dist, groundLayers);

        if (debugCasts)
        {
            Debug.DrawRay(origin, Vector2.down * dist, hit.collider ? Color.green : Color.red, 0.02f);
        }

        return hit.collider != null;
    }

    private bool HasWallAheadBox(float dirX)
    {
        Bounds b = col.bounds;

        float boxH = Mathf.Max(0.10f, b.size.y * wallBoxHeightFactor);
        float boxW = Mathf.Max(0.08f, b.size.x * wallBoxWidthFactor);
        Vector2 boxSize = new Vector2(boxW, boxH);

        Vector2 origin = new Vector2(b.center.x, b.min.y + wallCheckFeetOffset);
        Vector2 dir = (dirX >= 0f) ? Vector2.right : Vector2.left;

        // Distancia hacia delante: mínimo que salga del propio collider
        float dist = Mathf.Max(wallCheckDistance, b.extents.x + 0.06f);

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, dir, dist, obstacleLayers);

        if (debugCasts)
        {
            Debug.DrawRay(origin, dir * dist, hit.collider ? Color.cyan : Color.yellow, 0.02f);
        }

        return hit.collider != null;
    }

    private void FlipVisual(float dirX)
    {
        if (dirX > 0.01f)
            transform.localScale = new Vector3(Mathf.Abs(baseScale.x), baseScale.y, baseScale.z);
        else if (dirX < -0.01f)
            transform.localScale = new Vector3(-Mathf.Abs(baseScale.x), baseScale.y, baseScale.z);
    }

    // =========================
    // Daño / interacción
    // =========================
    public void TakeHit(int dmg)
    {
        currentHealth -= dmg;
        if (currentHealth <= 0)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerBounceAttack bounce = other.GetComponent<PlayerBounceAttack>();
        if (bounce != null && bounce.invincibleDuringBounce && bounce.isInvincible)
            return;

        PlayerHealth hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(contactDamage);
    }

    public void ReceiveBounceImpact(BounceImpactData impact)
    {
        TakeHit(impact.damage);
    }

    public bool ApplyPiercingBounce(BounceImpactData impact, float incomingDamage, out float remainingDamage)
    {
        // Aplica daño (usa el incomingDamage como “pool”)
        int dmg = Mathf.Max(1, Mathf.CeilToInt(incomingDamage));
        TakeHit(dmg);

        // Decide cuánto “consume” este enemigo del pool.
        // Si quieres que el daño pase íntegro a los siguientes, NO consumas:
        remainingDamage = incomingDamage;

        // IMPORTANTE: devolver true = "se rompe / se atraviesa" => NO rebote
        return true;
    }


}
