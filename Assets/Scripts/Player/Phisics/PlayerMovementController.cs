using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerPhysicsStateController))]
public class PlayerMovementController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Salto variable")]
    [Range(0f, 1f)]
    public float jumpCutMultiplier = 0.5f;  // cuánto se reduce el salto al soltar antes

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Z;   // teclado
    public string jumpButton = "Jump";    // mando (X en Play)
    public string horizontalAxis = "Horizontal"; // eje de movimiento (stick izq)

    [Header("Ground Detection (por colisiones)")]
    public string groundTag = "Ground";

    [Range(0f, 1f)]
    public float groundNormalThreshold = 0.9f; // solo normales MUY hacia arriba cuentan como suelo

    [Header("Coyote Time / Jump Buffer")]
    public float coyoteTime = 0.1f;       // margen tras dejar el suelo
    public float jumpBufferTime = 0.25f;  // margen desde que pulsas hasta que pisas suelo

    [Header("Pocket Unstuck (huecos pequeños)")]
    public bool enablePocketUnstuck = true;

    [Tooltip("Caja aproximada para comprobar si estás encajado. (en unidades mundo)")]
    public Vector2 pocketCheckSize = new Vector2(0.55f, 0.95f);

    [Tooltip("Offset local del check (normalmente un poco hacia abajo para cubrir pies/cuerpo).")]
    public Vector2 pocketCheckOffset = new Vector2(0f, -0.05f);

    [Tooltip("Tiempo mínimo atascado antes de intentar sacarte, incluso grounded.")]
    public float pocketStuckTimeToNudge = 0.10f;

    [Tooltip("Paso vertical por intento para salir del hueco.")]
    public float pocketNudgeUpStep = 0.06f;

    [Tooltip("Máximo de intentos hacia arriba por tick.")]
    public int pocketMaxUpSteps = 6;

    [Tooltip("Paso lateral si subir no funciona.")]
    public float pocketNudgeSideStep = 0.06f;

    private float pocketStuckTimer = 0f;


    [HideInInspector]
    public bool movementLocked = false;

    private Rigidbody2D rb;
    private PlayerPhysicsStateController phys;

    // ANIMACIONES
    [Header("Animation")]
    [SerializeField] private PlayerLocomotionAnimator locomotionAnim;
    [SerializeField] private PlayerStaticFX staticFX;

    [Header("Facing")]
    [SerializeField] private Transform visualRoot;   // hijo "Visual"
    [SerializeField] private bool faceRightByDefault = true;
    [SerializeField] private float deadzone = 0.01f;

    private bool facingRight;

    [Header("Ground Probe (anti-atascos)")]
    public LayerMask groundMask;
    public Vector2 groundProbeSize = new Vector2(0.6f, 0.12f);
    public float groundProbeDistance = 0.08f;
    public Vector2 groundProbeOffset = new Vector2(0f, -0.5f);
    private bool groundedByProbe;

    [Header("Unstuck (anti-picos)")]
    public float stuckSpeedThreshold = 0.02f;
    public float stuckTimeToNudge = 0.12f;
    public float nudgeUpDistance = 0.06f;
    private float stuckTimer = 0f;

    // Grounded por colisiones
    private readonly Dictionary<Collider2D, bool> groundedColliders = new Dictionary<Collider2D, bool>();

    // Grounded final: colisiones válidas O probe
    private bool isGrounded => groundedColliders.Count > 0 || groundedByProbe;
    public bool IsGrounded => isGrounded;

    // timers internos
    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;

    private GameplayTelemetry telemetry;

    private const bool DEBUG_MOVEMENT = false;

    

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (locomotionAnim == null)
            locomotionAnim = GetComponentInChildren<PlayerLocomotionAnimator>(true);

        if (staticFX == null)
            staticFX = GetComponentInChildren<PlayerStaticFX>(true);

        phys = GetComponent<PlayerPhysicsStateController>();

        telemetry = GameplayTelemetry.Instance;
        if (telemetry != null)
            telemetry.LogEvent("LEVEL_START", transform.position, "Lab_Tema1_CRISP_PuntosFuga");

        if (visualRoot == null)
        {
            var t = transform.Find("Visual");
            if (t != null) visualRoot = t;
        }

        facingRight = faceRightByDefault;
        ApplyFacing();
    }

    private void Update()
    {
        // Si algún sistema externo bloquea movimiento, no aplicamos input
        if (movementLocked)
        {
            if (DEBUG_MOVEMENT) Debug.Log("DBG -> movementLocked, no aplico input");
            return;
        }

        // Si estás en Dash o SparkAnchor, NO TOQUES el Rigidbody
        if (phys != null && (phys.IsInDash() || phys.IsInSparkAnchor()))
            return;

        // --------------------
        // INPUT HORIZONTAL
        // --------------------
        float inputX = Input.GetAxisRaw(horizontalAxis);  // teclado + stick izq
        UpdateFacing(inputX);

        // Ground probe: evita softlocks en picos/esquinas
        groundedByProbe = ProbeGround();

        // --------------------
        // MOVIMIENTO HORIZONTAL
        // --------------------
        Vector2 v = rb.linearVelocity;
        v.x = inputX * moveSpeed;
        rb.linearVelocity = v;

        // --------------------
        // UNSTUCK (anti picos)
        // --------------------
        // Si estás empujando pero NO avanzas y NO estás grounded, te despega ligeramente hacia arriba.
        bool pushing = Mathf.Abs(inputX) > 0.1f;
        bool basicallyNotMoving = Mathf.Abs(rb.linearVelocity.x) < stuckSpeedThreshold && Mathf.Abs(rb.linearVelocity.y) < 0.2f;

        if (pushing && basicallyNotMoving && !isGrounded)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTimeToNudge)
            {
                rb.position += Vector2.up * nudgeUpDistance;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        TryPocketUnstuck(pushing, inputX);

        // --------------------
        // COYOTE TIME
        // --------------------
        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;

        if (coyoteTimer < 0f) coyoteTimer = 0f;

        // --------------------
        // JUMP BUFFER
        // --------------------
        bool jumpPressed = Input.GetKeyDown(jumpKey) || Input.GetKeyDown(KeyCode.JoystickButton0);
        if (jumpPressed)
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer -= Time.deltaTime;

        if (jumpBufferTimer < 0f) jumpBufferTimer = 0f;

        // --------------------
        // SALTO (buffer + coyote)
        // --------------------
        bool canJumpNow = (coyoteTimer > 0f);
        bool wantJump = (jumpBufferTimer > 0f) && canJumpNow;

        if (wantJump)
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;

            if (DEBUG_MOVEMENT) Debug.Log("DBG -> JUMP ejecutado");

            if (telemetry != null)
                telemetry.LogEvent("JUMP", transform.position, $"velX={rb.linearVelocity.x:F2}");

            v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;

            locomotionAnim?.NotifyJump(rb);
        }

        // --------------------
        // SALTO VARIABLE
        // --------------------
        bool jumpReleased = Input.GetKeyUp(jumpKey) || Input.GetKeyUp(KeyCode.JoystickButton0);
        if (rb.linearVelocity.y > 0f && jumpReleased)
        {
            v = rb.linearVelocity;
            v.y *= jumpCutMultiplier;
            rb.linearVelocity = v;

            if (DEBUG_MOVEMENT) Debug.Log("DBG -> Jump cut aplicado");
        }

        locomotionAnim?.TickAirborne(rb, IsGrounded);
        staticFX?.Tick(IsGrounded, inputX);
    }

    // ---------
    // GROUND CHECK HELPERS
    // ---------
    private bool HasValidGroundContact(Collision2D collision)
    {
        foreach (var contact in collision.contacts)
        {
            // punto por debajo del centro del jugador (pies, no cabeza)
            bool contactBelowPlayer = contact.point.y <= transform.position.y - 0.05f;

            // normal claramente hacia arriba
            bool normalUpEnough = contact.normal.y >= groundNormalThreshold;

            if (contactBelowPlayer && normalUpEnough)
                return true;
        }
        return false;
    }

    private void SetGroundedForCollider(Collider2D col, bool groundedNow)
    {
        bool wasGrounded = isGrounded;

        if (groundedNow)
            groundedColliders[col] = true;
        else
            groundedColliders.Remove(col);

        if (DEBUG_MOVEMENT)
            Debug.Log($"DBG -> collider {col.name} groundedNow={groundedNow}, totalGroundColliders={groundedColliders.Count}");

        // LAND solo al pasar de NO grounded a grounded (por colisiones)
        if (!wasGrounded && isGrounded)
        {
            if (telemetry != null)
                telemetry.LogEvent("LAND", transform.position);

            locomotionAnim?.NotifyLanded();
        }
    }

    // ---------
    // COLISIONES
    // ---------
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(groundTag))
            return;

        bool hasGroundContact = HasValidGroundContact(collision);
        SetGroundedForCollider(collision.collider, hasGroundContact);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(groundTag))
            return;

        bool hasGroundContact = HasValidGroundContact(collision);
        SetGroundedForCollider(collision.collider, hasGroundContact);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(groundTag))
            return;

        SetGroundedForCollider(collision.collider, false);
    }

    // ---------
    // FACING
    // ---------
    private void UpdateFacing(float inputX)
    {
        if (visualRoot == null) return;

        if (inputX > deadzone) facingRight = true;
        else if (inputX < -deadzone) facingRight = false;
        else return;

        ApplyFacing();
    }

    private void ApplyFacing()
    {
        if (visualRoot == null) return;

        Vector3 s = visualRoot.localScale;
        s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
        visualRoot.localScale = s;
    }

    // ---------
    // PROBE GROUND (OverlapBox + BoxCast)
    // ---------
    private bool ProbeGround()
    {
        Vector2 center = (Vector2)transform.position + groundProbeOffset;

        // 1) Overlap directo en pies
        Collider2D hit = Physics2D.OverlapBox(center, groundProbeSize, 0f, groundMask);
        if (hit != null) return true;

        // 2) Cast cortito hacia abajo
        RaycastHit2D cast = Physics2D.BoxCast(center, groundProbeSize, 0f, Vector2.down, groundProbeDistance, groundMask);
        return cast.collider != null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualiza el probe en el editor
        Gizmos.color = Color.yellow;
        Vector2 center = (Vector2)transform.position + groundProbeOffset;
        Gizmos.DrawWireCube(center, groundProbeSize);
    }
#endif

    private bool IsPocketBlockedAt(Vector2 pos)
    {
        Vector2 center = pos + pocketCheckOffset;
        // Si toca groundMask dentro de esta caja, consideramos que “está apretado”
        return Physics2D.OverlapBox(center, pocketCheckSize, 0f, groundMask) != null;
    }

    private void TryPocketUnstuck(bool pushing, float inputX)
    {
        if (!enablePocketUnstuck) return;

        // “Estoy empujando o intentando moverme” + “no me muevo de verdad”
        bool basicallyNotMoving =
            Mathf.Abs(rb.linearVelocity.x) < stuckSpeedThreshold &&
            Mathf.Abs(rb.linearVelocity.y) < 0.20f;

        if (!(pushing && basicallyNotMoving))
        {
            pocketStuckTimer = 0f;
            return;
        }

        // Si no estás bloqueado “de verdad”, no hagas nada
        if (!IsPocketBlockedAt(rb.position))
        {
            pocketStuckTimer = 0f;
            return;
        }

        pocketStuckTimer += Time.deltaTime;
        if (pocketStuckTimer < pocketStuckTimeToNudge) return;
        pocketStuckTimer = 0f;

        Vector2 start = rb.position;

        // 1) Intenta salir hacia ARRIBA en varios micro-steps
        for (int i = 1; i <= pocketMaxUpSteps; i++)
        {
            Vector2 test = start + Vector2.up * (pocketNudgeUpStep * i);
            if (!IsPocketBlockedAt(test))
            {
                rb.position = test;
                // Evita que te “reclave” por velocidad residual hacia abajo
                var v = rb.linearVelocity;
                if (v.y < 0f) v.y = 0f;
                rb.linearVelocity = v;
                return;
            }
        }

        // 2) Si no pudo subir, intenta un micro-lateral hacia el lado que empujas
        float dir = Mathf.Sign(inputX);
        if (Mathf.Abs(dir) < 0.1f) dir = 1f;

        Vector2 sideTest = start + Vector2.right * (pocketNudgeSideStep * dir);
        if (!IsPocketBlockedAt(sideTest))
        {
            rb.position = sideTest;
            return;
        }

        // 3) Último recurso: lateral contrario
        sideTest = start + Vector2.right * (pocketNudgeSideStep * -dir);
        if (!IsPocketBlockedAt(sideTest))
        {
            rb.position = sideTest;
            return;
        }
    }

}
