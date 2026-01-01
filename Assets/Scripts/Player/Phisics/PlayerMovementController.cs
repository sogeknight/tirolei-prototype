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
    public float groundNormalThreshold = 0.9f;
    // solo normales MUY hacia arriba cuentan como suelo

    [Header("Coyote Time / Jump Buffer")]
    public float coyoteTime = 0.1f;       // margen tras dejar el suelo
    public float jumpBufferTime = 0.25f;  // margen desde que pulsas hasta que pisas suelo

    // Flag para que otros sistemas (dash, cinemáticas, etc.) puedan bloquear el movimiento
    [HideInInspector]
    public bool movementLocked = false;

    private Rigidbody2D rb;
    private PlayerPhysicsStateController phys;

    //ANIMACIONES
    [Header("Animation")]
    [SerializeField] private PlayerLocomotionAnimator locomotionAnim;
    [SerializeField] private PlayerStaticFX staticFX;


    private bool prevGrounded;




    [Header("Facing")]
    [SerializeField] private Transform visualRoot;   // arrastra aquí el hijo "Visual" (donde está el Animator/Sprite)
    [SerializeField] private bool faceRightByDefault = true;
    [SerializeField] private float deadzone = 0.01f;

    private bool facingRight;


    


    // En vez de un simple contador bruto, mapeamos los colliders que SON suelo ahora mismo
    private readonly Dictionary<Collider2D, bool> groundedColliders = new Dictionary<Collider2D, bool>();
    private bool isGrounded => groundedColliders.Count > 0;
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
        {
            telemetry.LogEvent("LEVEL_START", transform.position, "Lab_Tema1_CRISP_PuntosFuga");
        }

        if (visualRoot == null)
        {
            // intenta encontrar "Visual" típico
            var t = transform.Find("Visual");
            if (t != null) visualRoot = t;
        }
        facingRight = faceRightByDefault;
        ApplyFacing();

    }

    private void Update()
    {


        for (int i = 0; i < 20; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + i)))
            {
                Debug.Log("BOTÓN PULSADO: JoystickButton" + i);
            }
        }

        // Si algún sistema externo bloquea movimiento, no aplicamos input
        if (movementLocked)
        {
            if (DEBUG_MOVEMENT)
                Debug.Log("DBG -> movementLocked, no aplico input");
            return;
        }
        // Si estás en Dash o SparkAnchor, NO TOQUES el Rigidbody
        if (phys != null && (phys.IsInDash() || phys.IsInSparkAnchor()))
            return;


        // --------------------
        // MOVIMIENTO HORIZONTAL
        // --------------------
        Vector2 v = rb.linearVelocity;
        float inputX = Input.GetAxisRaw(horizontalAxis);  // teclado + stick izq
        UpdateFacing(inputX);

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
        // JUMP BUFFER (teclado o mando)
        bool jumpPressed  = Input.GetKeyDown(jumpKey) || Input.GetKeyDown(KeyCode.JoystickButton0);

        if (jumpPressed)
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
        // --------------------
        bool canJumpNow = (coyoteTimer > 0f);
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

            locomotionAnim?.NotifyJump(rb);

        }

        // --------------------
        // SALTO VARIABLE
        // --------------------
        bool jumpReleased = Input.GetKeyUp(jumpKey)   || Input.GetKeyUp(KeyCode.JoystickButton0);

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
        {
            groundedColliders[col] = true;
        }
        else
        {
            groundedColliders.Remove(col);
        }

        if (DEBUG_MOVEMENT)
            Debug.Log($"DBG -> collider {col.name} groundedNow={groundedNow}, totalGroundColliders={groundedColliders.Count}");

        // Telemetría LAND solo al pasar de NO grounded a grounded
        // Telemetría LAND solo al pasar de NO grounded a grounded
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

            // si antes estabas tocando la parte superior, pero ahora solo estás rozando el lateral,
            // HasValidGroundContact() pasará a false y se quitará ese collider del "suelo".
            bool hasGroundContact = HasValidGroundContact(collision);
            SetGroundedForCollider(collision.collider, hasGroundContact);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!collision.collider.CompareTag(groundTag))
                return;

            // al salir de la colisión, seguro que ya no es suelo
            SetGroundedForCollider(collision.collider, false);
        }

        private void UpdateFacing(float inputX)
        {
            if (visualRoot == null) return;

            if (inputX > deadzone) facingRight = true;
            else if (inputX < -deadzone) facingRight = false;
            else return; // no cambies si no hay input

            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (visualRoot == null) return;

            Vector3 s = visualRoot.localScale;
            s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
            visualRoot.localScale = s;
        }

    }
