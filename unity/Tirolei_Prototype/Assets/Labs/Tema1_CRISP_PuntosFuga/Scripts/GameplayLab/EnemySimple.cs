using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemySimple : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Daño al jugador")]
    public int contactDamage = 1;

    [Header("Objetivo")]
    public Transform target;              // Player
    public float maxChaseDistance = 20f;  // distancia máxima a la que se plantea atacar

    [Header("Suelo (simple)")]
    public LayerMask groundLayers;        // normalmente Ground
    public float groundRayDistance = 0.2f;

    [Header("Ataque dash")]
    public float chargeTime = 0.3f;       // tiempo de "carga" antes del dash
    public float dashSpeed = 10f;         // velocidad horizontal del dash
    public float dashDuration = 0.4f;     // duración del dash
    public float attackCooldown = 1.5f;   // tiempo entre ataques

    [Header("Salto de rescate cuando se atasca")]
    public float obstacleJumpForce = 8f;      // fuerza del salto
    public float stuckMinDeltaX = 0.01f;      // si avanza menos que esto en X, se considera atascado
    public int maxJumpsPerDash = 2;           // nº máximo de saltos de rescate por dash

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector3 baseScale;

    private float cooldownTimer = 0f;
    private float stateTimer = 0f;
    private float dashDirection = 0f;

    private float lastPosX;
    private int jumpsThisDash = 0;

    private enum EnemyState { Idle, Charging, Dashing, Recovering }
    private EnemyState state = EnemyState.Idle;

    private void Awake()
    {
        currentHealth = maxHealth;

        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        baseScale = transform.localScale;

        lastPosX = transform.position.x;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        cooldownTimer -= Time.fixedDeltaTime;
        if (cooldownTimer < 0f) cooldownTimer = 0f;

        bool grounded = IsGroundedSimple();

        float dx = target.position.x - transform.position.x;
        float absDx = Mathf.Abs(dx);

        switch (state)
        {
            // ---------------- IDLE: esperando oportunidad de atacar ----------------
            case EnemyState.Idle:
            {
                Vector2 v = rb.linearVelocity;
                v.x = 0f;
                rb.linearVelocity = v;

                if (absDx > maxChaseDistance) return;

                if (cooldownTimer <= 0f && grounded)
                {
                    dashDirection = Mathf.Sign(dx);
                    if (dashDirection == 0f) dashDirection = 1f;

                    FlipVisual(dashDirection);

                    state = EnemyState.Charging;
                    stateTimer = chargeTime;
                }
            }
            break;

            // ---------------- CHARGING: se queda quieto cargando el dash ----------------
            case EnemyState.Charging:
            {
                stateTimer -= Time.fixedDeltaTime;

                Vector2 v = rb.linearVelocity;
                v.x = 0f;
                rb.linearVelocity = v;

                if (stateTimer <= 0f && grounded)
                {
                    state = EnemyState.Dashing;
                    stateTimer = dashDuration;

                    jumpsThisDash = 0;               // reset de saltos de rescate
                    lastPosX = transform.position.x; // empezamos a medir desplazamiento

                    rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
                }
            }
            break;

            // ---------------- DASHING: placaje en línea recta ----------------
            case EnemyState.Dashing:
            {
                stateTimer -= Time.fixedDeltaTime;

                // mantener velocidad horizontal constante
                Vector2 v = rb.linearVelocity;
                v.x = dashDirection * dashSpeed;
                rb.linearVelocity = v;

                // --- DETECCIÓN DE ATASCO EN ESQUINAS / PICOS ---
                float currentPosX = transform.position.x;
                float deltaX = Mathf.Abs(currentPosX - lastPosX);

                if (grounded && deltaX < stuckMinDeltaX && jumpsThisDash < maxJumpsPerDash)
                {
                    // no ha avanzado casi nada en X -> está atascado contra un borde
                    v = rb.linearVelocity;
                    v.y = obstacleJumpForce;
                    rb.linearVelocity = v;

                    jumpsThisDash++;
                }

                lastPosX = currentPosX;

                if (stateTimer <= 0f)
                {
                    state = EnemyState.Recovering;
                    stateTimer = 0.2f;
                    cooldownTimer = attackCooldown;
                }
            }
            break;

            // ---------------- RECOVERING: se queda quieto un instante ----------------
            case EnemyState.Recovering:
            {
                stateTimer -= Time.fixedDeltaTime;

                Vector2 v = rb.linearVelocity;
                v.x = 0f;
                rb.linearVelocity = v;

                if (stateTimer <= 0f)
                {
                    state = EnemyState.Idle;
                }
            }
            break;
        }
    }

    private bool IsGroundedSimple()
    {
        Vector2 origin = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundRayDistance, groundLayers);
        return hit.collider != null;
    }

    private void FlipVisual(float dirX)
    {
        if (dirX > 0.01f)
        {
            transform.localScale = new Vector3(
                Mathf.Abs(baseScale.x),
                baseScale.y,
                baseScale.z
            );
        }
        else if (dirX < -0.01f)
        {
            transform.localScale = new Vector3(
                -Mathf.Abs(baseScale.x),
                baseScale.y,
                baseScale.z
            );
        }
    }

    // --- Daño y muerte ---

    public void TakeHit(int dmg)
    {
        currentHealth -= dmg;
        Debug.Log($"[EnemySimple] Recibe {dmg} → vida = {currentHealth}");

        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 1) ¿Está el player en modo bounce invencible?
        PlayerBounceAttack bounce = other.GetComponent<PlayerBounceAttack>();
        if (bounce != null && bounce.invincibleDuringBounce && bounce.isInvincible)
        {
            Debug.Log("[EnemySimple] Player en BOUNCE ATTACK invencible → NO se aplica daño.");
            return;
        }

        // 2) Si no está invencible, se aplica daño normal
        Debug.Log("[EnemySimple] Trigger con Player -> daño de contacto");

        PlayerHealth playerHP = other.GetComponent<PlayerHealth>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(contactDamage);
        }
    }


}
