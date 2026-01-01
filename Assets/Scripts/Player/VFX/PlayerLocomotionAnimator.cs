using UnityEngine;

[DisallowMultipleComponent]
public class PlayerLocomotionAnimator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;

    [Header("Animator params/state (exactos)")]
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string jumpStateName = "JumpTest";

    [Header("Timing goal")]
    [Tooltip("Queremos que el clip termine ANTES de tocar suelo con este margen (segundos).")]
    [SerializeField] private float finishMargin = 0.2f;

    [Tooltip("Máxima aceleración permitida.")]
    [SerializeField] private float maxSpeedUp = 4.0f;

    [Tooltip("Suavizado del cambio de velocidad.")]
    [SerializeField] private float speedLerp = 18f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private int jumpHash;
    private int jumpStateHash;

    private float defaultSpeed = 1f;
    private float groundYAtTakeoff;
    private bool airborneTracking = false;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null) animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("[PlayerLocomotionAnimator] No encuentro Animator.", this);
            enabled = false;
            return;
        }

        jumpHash = Animator.StringToHash(jumpTrigger);
        jumpStateHash = Animator.StringToHash(jumpStateName);

        defaultSpeed = animator.speed;
        if (defaultSpeed <= 0f) defaultSpeed = 1f;
    }

    // Llamar EXACTO cuando aplicas el salto al RB
    public void NotifyJump(Rigidbody2D rb)
    {
        if (rb == null) return;

        // Guardamos "suelo" como la Y al despegar.
        groundYAtTakeoff = rb.position.y;

        // Disparo del trigger
        animator.speed = defaultSpeed;
        animator.ResetTrigger(jumpHash);
        animator.SetTrigger(jumpHash);

        airborneTracking = true;

        if (logDebug) Debug.Log("[PlayerLocomotionAnimator] NotifyJump()", this);
    }

    // Llamar al aterrizar (cuando pasas a grounded)
    public void NotifyLanded()
    {
        animator.speed = defaultSpeed;
        airborneTracking = false;

        if (logDebug) Debug.Log("[PlayerLocomotionAnimator] NotifyLanded() reset speed", this);
    }

    // Llamar cada frame mientras el juego corre (Update) pasando rb e isGrounded
    public void TickAirborne(Rigidbody2D rb, bool isGrounded)
    {
        if (!airborneTracking) return;
        if (rb == null) return;

        if (isGrounded)
            return; // el reseteo lo haces con NotifyLanded()

        // Confirmamos que estamos realmente en el state de salto
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.shortNameHash != jumpStateHash && !st.IsName(jumpStateName))
            return;

        // Progreso 0..1 del state actual
        float frac = st.normalizedTime - Mathf.Floor(st.normalizedTime);

        // Tiempo restante de animación a speed = 1 (aprox): st.length ya está afectado por speed,
        // así que usamos una aproximación robusta: tiempoRestante = (1-frac) * (st.length * animator.speed)
        // porque st.length = duración actual ya escalada (en muchos casos). Esta fórmula estabiliza.
        float currentSpeed = Mathf.Max(0.0001f, animator.speed);
        float clipDurationApproxAtSpeed1 = st.length * currentSpeed;
        float remainingAnimSecAtSpeed1 = (1f - frac) * clipDurationApproxAtSpeed1;

        // Tiempo estimado hasta tocar "groundYAtTakeoff"
        float tImpact = EstimateTimeToReachY(
            currentY: rb.position.y,
            targetY: groundYAtTakeoff,
            vY: GetYVel(rb),
            aDown: GetGravityAbs(rb)
        );

        if (tImpact <= 0f) return;

        // Queremos terminar antes de tocar con margen
        float desired = Mathf.Max(0.01f, tImpact - finishMargin);

        // Speed requerido para que lo que queda entre en desired
        float requiredSpeed = remainingAnimSecAtSpeed1 / desired;

        // Clamp: nunca más lento que default, y nunca más rápido que max
        float targetSpeed = Mathf.Clamp(requiredSpeed, defaultSpeed, defaultSpeed * maxSpeedUp);

        animator.speed = Mathf.Lerp(animator.speed, targetSpeed, Time.deltaTime * speedLerp);

        if (logDebug)
            Debug.Log($"[PlayerLocomotionAnimator] tImpact={tImpact:F3} remaining={remainingAnimSecAtSpeed1:F3} targetSpeed={targetSpeed:F2}", this);
    }

    private static float GetYVel(Rigidbody2D rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity.y;
#else
        return rb.velocity.y;
#endif
    }

    private static float GetGravityAbs(Rigidbody2D rb)
    {
        float g = Mathf.Abs(Physics2D.gravity.y);
        return g * rb.gravityScale;
    }

    // Resuelve tiempo para llegar a targetY con aceleración hacia abajo.
    // Ecuación: y(t)=y0 + v*t - 0.5*a*t^2, con a>0.
    private static float EstimateTimeToReachY(float currentY, float targetY, float vY, float aDown)
    {
        if (aDown <= 0f) return 0f;

        float dy = currentY - targetY; // altura sobre el suelo guardado
        if (dy <= 0f) return 0f;

        // target: dy + vY*t - 0.5*a*t^2 = 0  => 0.5*a*t^2 - vY*t - dy = 0
        float A = 0.5f * aDown;
        float B = -vY;
        float C = -dy;

        float disc = B * B - 4f * A * C;
        if (disc < 0f) return 0f;

        float sqrt = Mathf.Sqrt(disc);
        float t1 = (-B + sqrt) / (2f * A);
        float t2 = (-B - sqrt) / (2f * A);

        float t = 0f;
        if (t1 > 0f && t2 > 0f) t = Mathf.Min(t1, t2);
        else if (t1 > 0f) t = t1;
        else if (t2 > 0f) t = t2;

        return t;
    }
}
