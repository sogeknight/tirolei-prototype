using UnityEngine;

public class PlayerPhysicsStateController : MonoBehaviour
{
    public enum State
    {
        Normal = 0,
        SparkHold = 10,
        SparkAnchor = 15,   // NUEVO
        Dash = 20,
        BounceAiming = 100,
        BounceBouncing = 90
    }

    private bool reqSparkAnchor;

    public void RequestSparkAnchor()
    {
        reqSparkAnchor = true;
    }



    [Header("Base (se capturan en Awake)")]
    [SerializeField] private float baseGravityScale = 1f;
    [SerializeField] private RigidbodyType2D baseBodyType = RigidbodyType2D.Dynamic;

    private Rigidbody2D rb;

    private State current = State.Normal;

    // Requests (quién pide qué)
    private bool reqSparkHold;
    private float reqSparkGravityMult = 1f;

    private bool reqDash;                 // NUEVO
    private bool reqBounceAiming;
    private bool reqBounceBouncing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        baseGravityScale = rb.gravityScale;
        baseBodyType = rb.bodyType;

        Apply(State.Normal);
    }

    private void LateUpdate()
    {
        // Resolvemos al final del frame, cuando todos han pedido cosas.
        State wanted = ResolveState();
        if (wanted != current)
            Apply(wanted);

        // Reset requests para el siguiente frame (modelo “stateless”)
        reqSparkHold = false;
        reqSparkGravityMult = 1f;

        reqSparkAnchor = false;   // <- AÑADE ESTA PUTA LÍNEA

        reqDash = false;              // NUEVO
        reqBounceAiming = false;
        reqBounceBouncing = false;
    }

    private State ResolveState()
    {
        // Prioridad: bounce aiming > bounce > dash > sparkHold > normal
        if (reqBounceAiming) return State.BounceAiming;
        if (reqBounceBouncing) return State.BounceBouncing;
        if (reqDash) return State.Dash;
        if (reqSparkAnchor) return State.SparkAnchor;   // NUEVO
        if (reqSparkHold) return State.SparkHold;
        return State.Normal;

    }

    private void Apply(State s)
    {
        current = s;

        switch (s)
        {
            case State.SparkAnchor:
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            break;
            case State.Normal:
                rb.bodyType = baseBodyType;             // Dynamic (normalmente)
                rb.gravityScale = baseGravityScale;
                break;

            case State.SparkHold:
                rb.bodyType = baseBodyType;             // Dynamic
                rb.gravityScale = baseGravityScale * reqSparkGravityMult;
                break;

            case State.Dash:                             // NUEVO
                // Dash = el cuerpo sigue siendo Dynamic pero sin gravedad.
                // No uses Kinematic aquí: evita estados raros con triggers/contacts.
                rb.bodyType = baseBodyType;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                break;

            case State.BounceAiming:
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                break;

            case State.BounceBouncing:
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                break;
        }
    }

    // -------- API para otros sistemas --------

    public void RequestSparkHold(float gravityMultiplier)
    {
        reqSparkHold = true;
        reqSparkGravityMult = Mathf.Clamp(gravityMultiplier, 0.01f, 1f);
    }

    public void RequestDash() // NUEVO
    {
        reqDash = true;
    }

    public void RequestBounceAiming()
    {
        reqBounceAiming = true;
    }

    public void RequestBounceBouncing()
    {
        reqBounceBouncing = true;
    }

    public bool IsInDash() => current == State.Dash; // NUEVO
    public bool IsInSparkAnchor() => current == State.SparkAnchor;


    public bool IsInBounceAiming() => current == State.BounceAiming;
    public bool IsInBounce() => current == State.BounceAiming || current == State.BounceBouncing;
}
