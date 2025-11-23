using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerSimpleController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Salto variable")]
    [Range(0f, 1f)]
    public float jumpCutMultiplier = 0.5f;  // cuánto se reduce el salto al soltar antes

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Z;

    [Header("Ground Detection (por colisiones)")]
    public string groundTag = "Ground";

    [Range(0f, 1f)]
    public float groundNormalThreshold = 0.8f;
    // solo normales MUY hacia arriba cuentan como suelo

    [Header("Coyote Time / Jump Buffer")]
    public float coyoteTime = 0.1f;       // margen tras dejar el suelo
    public float jumpBufferTime = 0.25f;  // margen desde que pulsas hasta que pisas suelo

    private Rigidbody2D rb;

    // contactos de suelo
    private int groundContacts = 0;
    private bool isGrounded => groundContacts > 0;

    // timers internos
    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;

    private GameplayTelemetry telemetry;

    private const bool DEBUG_MOVEMENT = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        telemetry = GameplayTelemetry.Instance;

        if (telemetry != null)
        {
            telemetry.LogEvent("LEVEL_START", transform.position, "Lab_Tema1_CRISP_PuntosFuga");
        }
    }

    private void Update()
    {
        // --------------------
        // MOVIMIENTO HORIZONTAL
        // --------------------
        Vector2 v = rb.linearVelocity;
        float inputX = Input.GetAxisRaw("Horizontal");
        v.x = inputX * moveSpeed;
        rb.linearVelocity = v;

        // --------------------
        // COYOTE TIME
        // --------------------
        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;

        if (coyoteTimer < 0f) coyoteTimer = 0f;

        // --------------------
        // JUMP BUFFER (solo GetKeyDown)
        // --------------------
        if (Input.GetKeyDown(jumpKey))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        if (jumpBufferTimer < 0f) jumpBufferTimer = 0f;

        // --------------------
        // SALTO:
        //  - tiene que haber buffer activo
        //  - tiene que quedar coyote
        //  - y NO puedes ir hacia arriba (velY <= 0) para evitar bugs debajo de S2
        // --------------------
        bool verticalOk = rb.linearVelocity.y <= 0.01f;
        bool canJumpNow = (coyoteTimer > 0f) && verticalOk;
        bool wantJump = (jumpBufferTimer > 0f) && canJumpNow;

        if (wantJump)
        {
            // consumir timers
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;

            if (DEBUG_MOVEMENT) Debug.Log("DBG -> JUMP ejecutado");

            // telemetría
            if (telemetry != null)
            {
                telemetry.LogEvent("JUMP", transform.position, $"velX={v.x:F2}");
            }

            v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;
        }

        // --------------------
        // SALTO VARIABLE (min/max por duración de pulsación)
        // si sueltas la tecla y aún estás subiendo, recortamos el salto
        // --------------------
        if (rb.linearVelocity.y > 0f && Input.GetKeyUp(jumpKey))
        {
            v = rb.linearVelocity;
            v.y *= jumpCutMultiplier;
            rb.linearVelocity = v;

            if (DEBUG_MOVEMENT) Debug.Log("DBG -> Jump cut aplicado");
        }

        if (DEBUG_MOVEMENT)
        {
            Debug.Log($"DBG -> grounded={isGrounded}, contacts={groundContacts}, " +
                      $"coyote={coyoteTimer:F3}, buffer={jumpBufferTimer:F3}, velY={rb.linearVelocity.y:F2}");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(groundTag))
            return;

        // Solo contamos como suelo si el contacto está POR DEBAJO del centro del jugador
        // y la normal apunta claramente hacia arriba. Así evitamos techos/laterales.
        bool hasGroundContact = false;

        foreach (var contact in collision.contacts)
        {
            bool contactBelowPlayer = contact.point.y <= transform.position.y - 0.05f;
            bool normalUpEnough = contact.normal.y >= groundNormalThreshold;

            if (contactBelowPlayer && normalUpEnough)
            {
                hasGroundContact = true;
                break;
            }
        }

        if (!hasGroundContact)
        {
            if (DEBUG_MOVEMENT)
                Debug.Log("DBG -> Collision con Ground pero sin contacto de suelo válido (techo/lateral).");
            return;
        }

        bool wasGrounded = isGrounded;
        groundContacts++;

        if (DEBUG_MOVEMENT)
            Debug.Log($"DBG -> OnCollisionEnter suelo, groundContacts={groundContacts}");

        // Telemetría LAND solo al pasar de no grounded a grounded
        if (!wasGrounded && isGrounded && telemetry != null)
        {
            telemetry.LogEvent("LAND", transform.position);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(groundTag))
            return;

        if (groundContacts > 0)
            groundContacts--;

        if (DEBUG_MOVEMENT)
            Debug.Log($"DBG -> OnCollisionExit suelo, groundContacts={groundContacts}");
    }
}
