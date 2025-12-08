using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(Collider2D))]
public class PlayerBounceAttack : MonoBehaviour
{
    [Header("Ataque rebote")]
    public KeyCode attackKey = KeyCode.X;      // iniciar / cancelar
    public float launchSpeed = 25f;
    public int maxBounces = 3;

    [Header("Invencibilidad")]
    public bool invincibleDuringBounce = true;
    [HideInInspector] public bool isInvincible = false;

    [Header("Colisión de rebote")]
    public LayerMask bounceLayers;            // pon aquí Ground
    public float skin = 0.02f;                // separación mínima de la pared

    private Rigidbody2D rb;
    private PlayerMovementController movement;
    private CircleCollider2D circle;

    private bool isAiming = false;
    private bool isBouncing = false;

    private Vector2 aimDirection = Vector2.right;
    private Vector2 bounceDir = Vector2.right;
    private int remainingBounces = 0;

    private float originalGravityScale;
    private RigidbodyType2D originalBodyType;
    private float ballRadius;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovementController>();
        circle = GetComponent<CircleCollider2D>();

        if (rb == null) Debug.LogError("[PlayerBounceAttack] Falta Rigidbody2D.");
        if (movement == null) Debug.LogError("[PlayerBounceAttack] Falta PlayerMovementController.");
        if (circle == null) Debug.LogError("[PlayerBounceAttack] Falta CircleCollider2D.");

        // radio efectivo con escala
        ballRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
    }

    private void Update()
    {
        if (!isAiming && !isBouncing && Input.GetKeyDown(attackKey))
        {
            StartAiming();
        }

        if (isAiming)
        {
            HandleAiming();
        }

        if (isBouncing && (Input.GetKeyDown(attackKey) || Input.GetKeyDown(KeyCode.Z)))
        {
            EndBounce();
        }
    }

    private void FixedUpdate()
    {
        if (!isBouncing) return;

        float moveDist = launchSpeed * Time.fixedDeltaTime;
        Vector2 origin = rb.position;
        Vector2 dir = bounceDir.sqrMagnitude > 0.001f ? bounceDir.normalized : aimDirection.normalized;

        Vector2 targetPos = origin;

        // 1 solo cast por frame: suficiente y más estable visualmente
        RaycastHit2D hit = Physics2D.CircleCast(
            origin,
            ballRadius,
            dir,
            moveDist + skin,
            bounceLayers
        );

        if (hit.collider != null)
        {
            // mover hasta casi tocar la pared
            float travel = Mathf.Max(0f, hit.distance - skin);
            targetPos = origin + dir * travel;

            // reflejar dirección para el siguiente frame
            Vector2 reflDir = Vector2.Reflect(dir, hit.normal).normalized;
            bounceDir = reflDir;

            remainingBounces--;
            if (remainingBounces <= 0)
            {
                // aplicamos la posición de este frame y salimos
                rb.MovePosition(targetPos);
                EndBounce();
                return;
            }
        }
        else
        {
            // sin colisión este frame
            targetPos = origin + dir * moveDist;
        }

        // AQUÍ está la diferencia: usamos MovePosition, no rb.position
        rb.MovePosition(targetPos);
    }


    // ================== ESTADOS ==================

    private void StartAiming()
    {
        isAiming = true;
        aimDirection = Vector2.right;

        movement.movementLocked = true;

        // asegurarnos de que el rigidbody está quieto
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleAiming()
    {
        Vector2 inputDir = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        if (inputDir.sqrMagnitude > 0.1f)
            aimDirection = inputDir.normalized;

        if (Input.GetKeyUp(attackKey))
        {
            if (aimDirection.sqrMagnitude < 0.1f)
                aimDirection = Vector2.right;

            StartBounce();
        }
    }

    private void StartBounce()
    {
        isAiming = false;
        isBouncing = true;
        remainingBounces = maxBounces;

        originalGravityScale = rb.gravityScale;
        originalBodyType = rb.bodyType;

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        bounceDir = aimDirection.normalized;

        if (invincibleDuringBounce)
            isInvincible = true;
    }

    private void EndBounce()
    {
        isBouncing = false;

        rb.bodyType = originalBodyType;
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = Vector2.zero;

        movement.movementLocked = false;
        isInvincible = false;
    }

    // NO usamos OnCollisionEnter2D para el rebote,
    // toda la colisión se controla con CircleCast.
}
