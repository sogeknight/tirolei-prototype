using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerPhysicsStateController))]
public class PlayerBounceAttack : MonoBehaviour
{
    [Header("Ataque")]
    public KeyCode attackKey = KeyCode.X;
    public KeyCode attackPadKey = KeyCode.JoystickButton2;
    public float maxDistance = 10f;

    [Header("Llama / Costes (prototipo)")]
    public bool useFlame = true;
    public float maxFlame = 100f;
    public float flame = 100f;

    [Tooltip("Coste al INICIAR el bounce.")]
    public float flameCostStart = 8f;

    [Tooltip("Coste por cada REBOTE real.")]
    public float flameCostPerBounce = 3f;

    [Tooltip("Coste por unidad de distancia recorrida durante el bounce (opcional).")]
    public float flameCostPerUnit = 0f;

    [Tooltip("Si no hay llama suficiente para iniciar, no permite bounce.")]
    public bool blockBounceIfNoFlame = true;

    [Tooltip("Si se queda sin llama durante el bounce, termina automáticamente.")]
    public bool endBounceWhenOutOfFlame = true;

    [Header("Debug")]
    public bool debugFlame = true;
    public bool debugOnScreen = true;

    [HideInInspector] public float flameSpentTotal = 0f;
    [HideInInspector] public float flameSpentThisBounce = 0f;

    [Header("Invencibilidad")]
    public bool invincibleDuringBounce = true;
    [HideInInspector] public bool isInvincible = false;

    [Header("Colisión de rebote")]
    public LayerMask bounceLayers;
    public float skin = 0.02f;

    [Header("Daño")]
    public int bounceDamage = 20;

    [Header("Preview trayectoria")]
    public LineRenderer previewLine;
    public int previewSegments = 30;
    public int previewMaxBounces = 5;
    public Color previewColor = Color.cyan;

    [Header("Suavizado fin de trayecto")]
    [Range(0f, 1f)] public float slowDownFraction = 0.3f;
    [Range(0.05f, 1f)] public float minStepFactor = 0.2f;

    private Rigidbody2D rb;
    private PlayerMovementController movement;
    private CircleCollider2D circle;
    private PlayerPhysicsStateController phys;

    private bool isAiming = false;
    private bool isBouncing = false;

    private Vector2 aimDirection = Vector2.right;
    private Vector2 bounceDir = Vector2.right;
    private Vector2 lastPreviewDir = Vector2.right;

    private float remainingDistance = 0f;
    private float fixedStepSize = 0f;
    private float ballRadius = 0f;

    private Vector2 aimStartPosition;

    private PlayerSparkBoost spark;

    private readonly HashSet<Collider2D> impactedThisBounce = new HashSet<Collider2D>();

    public bool IsAiming => isAiming;
    public bool IsBouncing => isBouncing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovementController>();
        circle = GetComponent<CircleCollider2D>();
        phys = GetComponent<PlayerPhysicsStateController>();
        spark = GetComponent<PlayerSparkBoost>();

        if (phys == null) Debug.LogError("[PlayerBounceAttack] Falta PlayerPhysicsStateController en el Player.");
        if (circle == null) Debug.LogError("[PlayerBounceAttack] Falta CircleCollider2D.");

        ballRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        ConfigurePreview();
    }

    private void Update()
    {
        if (spark != null && (spark.IsSparkActive() || spark.IsDashing()))
        {
            if (isAiming) EndAiming();
            if (isBouncing) EndBounce();
            ClearPreview();
            return;
        }

        bool attackDown = Input.GetKeyDown(attackKey) || Input.GetKeyDown(attackPadKey);
        bool attackUp   = Input.GetKeyUp(attackKey)   || Input.GetKeyUp(attackPadKey);
        bool jumpDown   = Input.GetKeyDown(movement.jumpKey) || Input.GetKeyDown(KeyCode.JoystickButton0);

        if (isAiming)   phys.RequestBounceAiming();
        if (isBouncing) phys.RequestBounceBouncing();

        if (!isAiming && !isBouncing && attackDown)
        {
            if (!CanStartAiming())
            {
                Debug.LogWarning($"[BounceAttack] BLOQUEADO: flame={flame:0.##}, costStart={flameCostStart:0.##}, useFlame={useFlame}, blockIfNoFlame={blockBounceIfNoFlame}");
                return;
            }

            Debug.Log($"[BounceAttack] OK: StartAiming (flame={flame:0.##})");
            StartAiming();
        }

        if (isAiming)
        {
            HandleAiming();
        }

        if (isAiming && attackUp)
        {
            if (aimDirection.sqrMagnitude < 0.1f)
                aimDirection = Vector2.right;

            StartBounce();
        }

        if (isBouncing && (jumpDown || attackDown))
        {
            EndBounce();
        }
    }

    private void FixedUpdate()
    {
        if (spark != null && (spark.IsSparkActive() || spark.IsDashing()))
            return;

        if (isAiming)
        {
            if (useFlame && blockBounceIfNoFlame && flameCostStart > 0f && flame < flameCostStart)
            {
                EndAiming();
                return;
            }

            rb.MovePosition(aimStartPosition);
            return;
        }

        if (!isBouncing) return;

        float baseStep = fixedStepSize;
        float moveDist = baseStep;

        if (slowDownFraction > 0f)
        {
            float slowZone = maxDistance * slowDownFraction;
            if (remainingDistance < slowZone)
            {
                float t = remainingDistance / Mathf.Max(0.0001f, slowZone);
                float factor = Mathf.Lerp(minStepFactor, 1f, t);
                moveDist = baseStep * factor;
            }
        }

        if (moveDist > remainingDistance) moveDist = remainingDistance;

        if (moveDist <= 0f)
        {
            EndBounce();
            return;
        }

        Vector2 dir = (bounceDir.sqrMagnitude > 0.001f ? bounceDir : aimDirection).normalized;

        // ============================================================
        // NUEVO: bucle interno "piercing" dentro del step del FixedUpdate
        // ============================================================
        float damagePool = bounceDamage;
        int safety = 0;
        const int MAX_INTERNAL_HITS = 12;
        bool brokeSomethingThisStep = false;


        while (moveDist > 0f && remainingDistance > 0f && safety++ < MAX_INTERNAL_HITS)
        {
            Vector2 origin = rb.position;

            RaycastHit2D hit = Physics2D.CircleCast(origin, ballRadius, dir, moveDist + skin, bounceLayers);

            if (hit.collider == null)
            {
                // Sin colisión: avanza libre
                rb.MovePosition(origin + dir * moveDist);
                remainingDistance -= moveDist;

                if (useFlame && flameCostPerUnit > 0f)
                    SpendFlame(moveDist * flameCostPerUnit);

                CheckOutOfFlameAndEndIfNeeded();
                if (!isBouncing) return;

                if (remainingDistance <= 0f)
                    EndBounce();

                return;
            }

            // Hay impacto
            float travel = Mathf.Max(0f, hit.distance - skin);

            if (travel > 0f)
            {
                rb.MovePosition(origin + dir * travel);
                remainingDistance -= travel;

                if (useFlame && flameCostPerUnit > 0f)
                    SpendFlame(travel * flameCostPerUnit);
            }

            // Consumimos el step que quedaba
            float consumed = travel;
            moveDist = Mathf.Max(0f, moveDist - consumed);

            // Si no queda daño, a partir de aquí esto se comporta como "sólido"
            bool broke = false;
            float remainingDmg = 0f;

            if (damagePool > 0.0001f)
            {
                broke = ApplyPiercingImpact(hit.collider, dir, damagePool, out remainingDmg);
                damagePool = remainingDmg;
            }

        if (broke)
        {
            brokeSomethingThisStep = true;
            rb.MovePosition(rb.position + dir * 0.02f);
            continue;
        }

        if (brokeSomethingThisStep)
        {
            // Si ya rompiste algo en este step, NO hagas rebote sorpresa.
            EndBounce();
            return;
        }



            // =========
            // NO SE ROMPIÓ (o no era pierceable / o no había daño):
            // rebote normal y se acaba el step.
            // =========
            bounceDir = Vector2.Reflect(dir, hit.normal).normalized;

            if (useFlame && flameCostPerBounce > 0f)
                SpendFlame(flameCostPerBounce);

            CheckOutOfFlameAndEndIfNeeded();
            if (!isBouncing) return;

            if (remainingDistance <= 0f)
                EndBounce();

            return;
        }

        // Si salimos del while porque se acabó el step, terminamos normal.
        CheckOutOfFlameAndEndIfNeeded();
        if (!isBouncing) return;

        if (remainingDistance <= 0f)
            EndBounce();
    }

    // Devuelve true si ese impacto rompió el objeto/celda.
    private bool ApplyPiercingImpact(Collider2D col, Vector2 direction, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = incomingDamage;

        if (col == null) return false;

        // Evita spamear el mismo collider mil veces si NO se rompe.
        if (impactedThisBounce.Contains(col)) return false;

        var receiverPierce = col.GetComponentInParent<IPiercingBounceReceiver>();
        if (receiverPierce != null)
        {
            // OJO: en piercing, si se rompe, queremos permitir seguir, así que solo marcamos como "impactado"
            // cuando NO se rompe.
            var impact = new BounceImpactData((int)Mathf.Ceil(incomingDamage), direction, gameObject);

            bool broke = receiverPierce.ApplyPiercingBounce(impact, incomingDamage, out float rem);
            remainingDamage = rem;

            if (!broke)
                impactedThisBounce.Add(col);

            return broke;
        }

        // Fallback: sistema antiguo sin feedback => lo tratamos como sólido (rebota)
        var receiver = col.GetComponentInParent<IBounceImpactReceiver>();
        if (receiver != null)
        {
            receiver.ReceiveBounceImpact(new BounceImpactData(bounceDamage, direction, gameObject));
            impactedThisBounce.Add(col);
        }

        remainingDamage = 0f;
        return false;
    }

    // ================== LLAMA ==================

    private bool HasFlame(float amount) => (!useFlame) || flame >= amount;

    private bool SpendFlame(float amount)
    {
        if (!useFlame) return true;
        if (amount <= 0f) return true;
        if (flame < amount) return false;

        flame -= amount;
        if (flame < 0f) flame = 0f;

        flameSpentTotal += amount;
        if (isBouncing) flameSpentThisBounce += amount;

        if (debugFlame)
        {
            Debug.Log($"[Flame] -{amount:0.00} | flame={flame:0.00}/{maxFlame:0.00} | spentTotal={flameSpentTotal:0.00} | spentBounce={flameSpentThisBounce:0.00}");
        }

        return true;
    }

    private bool CanStartAiming()
    {
        if (!useFlame) return true;

        if (flameCostStart > 0f)
            return flame >= flameCostStart;

        return flame > 0f;
    }

    private void CheckOutOfFlameAndEndIfNeeded()
    {
        if (!useFlame || !endBounceWhenOutOfFlame) return;
        if (flame > 0f) return;

        EndBounce();
    }

    // ================== ESTADOS ==================

    private void StartAiming()
    {
        isAiming = true;

        aimDirection = Vector2.right;
        lastPreviewDir = aimDirection;

        movement.movementLocked = true;

        aimStartPosition = rb.position;

        fixedStepSize = maxDistance / Mathf.Max(1, previewSegments);
        UpdatePreview();
    }

    private void HandleAiming()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(x, y);
        if (input.sqrMagnitude < 0.01f)
            input = lastPreviewDir;

        input.Normalize();

        aimDirection = input;
        lastPreviewDir = input;

        UpdatePreview();
    }

    private void StartBounce()
    {
        if (useFlame && flameCostStart > 0f)
        {
            if (blockBounceIfNoFlame && !HasFlame(flameCostStart))
            {
                EndAiming();
                return;
            }

            if (!SpendFlame(flameCostStart))
            {
                EndAiming();
                return;
            }
        }

        isAiming = false;
        isBouncing = true;

        impactedThisBounce.Clear();

        flameSpentThisBounce = 0f;
        remainingDistance = maxDistance;

        ClearPreview();

        bounceDir = lastPreviewDir.sqrMagnitude > 0.001f ? lastPreviewDir.normalized : Vector2.right;

        if (invincibleDuringBounce)
            isInvincible = true;
    }

    private void EndAiming()
    {
        isAiming = false;
        ClearPreview();

        movement.movementLocked = false;
    }

    private void EndBounce()
    {
        isBouncing = false;
        ClearPreview();

        movement.movementLocked = false;
        isInvincible = false;
    }

    // ================== PREVIEW ==================

    private void UpdatePreview()
    {
        if (!isAiming || previewLine == null)
        {
            ClearPreview();
            return;
        }

        Vector2 origin = rb.position;
        Vector2 dir = lastPreviewDir.sqrMagnitude > 0.001f ? lastPreviewDir.normalized : Vector2.right;

        float remaining = maxDistance;
        int bounceCount = 0;

        List<Vector3> points = new List<Vector3> { origin };
        int maxPoints = Mathf.Max(2, previewSegments + 1);

        while (remaining > 0f && points.Count < maxPoints && bounceCount <= previewMaxBounces)
        {
            float stepDist = fixedStepSize;
            if (stepDist <= 0f) break;

            RaycastHit2D hit = Physics2D.CircleCast(origin, ballRadius, dir, stepDist + skin, bounceLayers);

            if (hit.collider != null)
            {
                float travel = Mathf.Max(0f, hit.distance - skin);
                Vector2 hitPos = origin + dir * travel;

                points.Add(hitPos);
                remaining -= travel;
                origin = hitPos;

                dir = Vector2.Reflect(dir, hit.normal).normalized;
                bounceCount++;

                if (travel <= 0.001f)
                    origin += dir * 0.01f;
            }
            else
            {
                Vector2 nextPos = origin + dir * stepDist;
                points.Add(nextPos);
                remaining -= stepDist;
                origin = nextPos;
            }
        }

        previewLine.positionCount = points.Count;
        previewLine.SetPositions(points.ToArray());
        previewLine.enabled = true;
    }

    private void ClearPreview()
    {
        if (previewLine == null) return;
        previewLine.enabled = false;
        previewLine.positionCount = 0;
    }

    private void ConfigurePreview()
    {
        if (previewLine == null)
        {
            previewLine = GetComponent<LineRenderer>();
            if (previewLine == null)
                previewLine = gameObject.AddComponent<LineRenderer>();
        }

        previewLine.useWorldSpace = true;
        previewLine.startWidth = 0.08f;
        previewLine.endWidth = 0.08f;

        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = previewColor;
        previewLine.material = mat;

        previewLine.startColor = Color.white;
        previewLine.endColor = Color.white;
        previewLine.sortingLayerName = "Default";
        previewLine.sortingOrder = 100;

        previewLine.positionCount = 0;
        previewLine.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // No se usa. Impacto via CircleCast en FixedUpdate.
    }
}
