using UnityEngine;
using System;
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

    [Header("Colisión de rebote (capas que quieres 'golpear' con el CircleCast)")]
    [Tooltip("Aquí normalmente van suelo/pared + enemigos/rompibles si quieres impactarlos con el cast.")]
    public LayerMask bounceLayers;

    [Header("Mundo sólido (solo suelo/pared). NO enemigos.")]
    [Tooltip("Esto se usa SOLO para evitar atravesar suelo/pared en el 'post-pierce'. Debe ser SOLO ground/walls.")]
    public LayerMask worldSolidLayers;

    public float skin = 0.02f;

    [Header("Evitar bloqueo por colisión con Enemy/Hazard durante Aim/Bounce")]
    [Tooltip("Marca aquí la layer Enemy (collider sólido del enemigo).")]
    public LayerMask enemyLayers;
    [Tooltip("Marca aquí la layer Hazard si tu hitbox/hazard te molesta durante Aim/Bounce.")]
    public LayerMask hazardLayers;

    public bool ignoreEnemyCollisionWhileAiming = true;
    public bool ignoreEnemyCollisionWhileBouncing = true;

    [Tooltip("Si true, también ignoramos Hazard durante Aim/Bounce (si tu Hitbox está en Hazard y molesta).")]
    public bool ignoreHazardDuringAimBounce = true;

    private Collider2D playerCol;
    private int playerLayer;
    private readonly List<int> enemyLayerIds = new List<int>();
    private readonly List<int> hazardLayerIds = new List<int>();
    private bool ignoringAimBounceCollisions = false;

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

    [Header("Pierce: avance seguro post-impacto")]
    [Tooltip("Distancia mínima para separarte del collider tras romperlo.")]
    public float pierceSeparationMin = 0.06f;

    [Tooltip("Multiplica el radio para calcular avance post-pierce (antes se hacía a ciegas).")]
    public float pierceSeparationRadiusFactor = 0.7f;

    [Tooltip("Máximo avance post-pierce para evitar empujones absurdos si el collider es grande.")]
    public float pierceSeparationMax = 0.25f;

    private Rigidbody2D rb;
    private PlayerMovementController movement;
    private CircleCollider2D circle;
    private PlayerPhysicsStateController phys;
    private PlayerSparkBoost spark;

    private bool isAiming = false;
    private bool isBouncing = false;

    private Vector2 aimDirection = Vector2.right;
    private Vector2 bounceDir = Vector2.right;
    private Vector2 lastPreviewDir = Vector2.right;

    private float remainingDistance = 0f;
    private float fixedStepSize = 0f;
    private float ballRadius = 0f;

    private Vector2 aimStartPosition;

    // Sólidos NO-pierce: evita spamear impactos infinitos
    private readonly HashSet<Collider2D> impactedThisBounce = new HashSet<Collider2D>();
    // Pierce: evita multi-hit dentro del mismo bounce, pero sigue atravesando
    private readonly HashSet<Collider2D> piercedThisBounce = new HashSet<Collider2D>();

    public bool IsAiming => isAiming;
    public bool IsBouncing => isBouncing;

    public event Action OnBounceStart;
    public event Action OnBounceEnd;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovementController>();
        circle = GetComponent<CircleCollider2D>();
        phys = GetComponent<PlayerPhysicsStateController>();
        spark = GetComponent<PlayerSparkBoost>();

        if (phys == null) Debug.LogError("[PlayerBounceAttack] Falta PlayerPhysicsStateController en el Player.");
        if (circle == null) Debug.LogError("[PlayerBounceAttack] Falta CircleCollider2D.");

        playerCol = GetComponent<Collider2D>();
        playerLayer = gameObject.layer;

        ballRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        fixedStepSize = maxDistance / Mathf.Max(1, previewSegments);
        ConfigurePreview();

        CacheLayerIds(enemyLayers, enemyLayerIds);
        CacheLayerIds(hazardLayers, hazardLayerIds);
    }

    private void CacheLayerIds(LayerMask mask, List<int> outList)
    {
        outList.Clear();
        for (int i = 0; i < 32; i++)
        {
            if (((mask.value >> i) & 1) == 1)
                outList.Add(i);
        }
    }

    private void Update()
    {
        if (spark != null && (spark.IsSparkActive() || spark.IsDashing()))
        {
            if (isAiming) EndAiming();
            if (isBouncing) EndBounce();
            SetAimBounceCollisionIgnore(false);
            ClearPreview();
            return;
        }

        bool attackDown = Input.GetKeyDown(attackKey) || Input.GetKeyDown(attackPadKey);
        bool attackUp = Input.GetKeyUp(attackKey) || Input.GetKeyUp(attackPadKey);
        bool jumpDown = Input.GetKeyDown(movement.jumpKey) || Input.GetKeyDown(KeyCode.JoystickButton0);

        if (isAiming) phys.RequestBounceAiming();
        if (isBouncing) phys.RequestBounceBouncing();

        if (!isAiming && !isBouncing && attackDown)
        {
            if (!CanStartAiming())
                return;

            StartAiming();

            if (ignoreEnemyCollisionWhileAiming)
                SetAimBounceCollisionIgnore(true);
        }

        if (isAiming)
            HandleAiming();

        if (isAiming && attackUp)
        {
            if (aimDirection.sqrMagnitude < 0.1f)
                aimDirection = Vector2.right;

            StartBounce();
        }

        if (isBouncing && (jumpDown || attackDown))
            EndBounce();
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

        float damagePool = bounceDamage;

        int safety = 0;
        const int MAX_INTERNAL_HITS = 24;

        while (moveDist > 0f && remainingDistance > 0f && safety++ < MAX_INTERNAL_HITS)
        {
            Vector2 origin = rb.position;

            // CLAVE: filtramos hits para NO BLOQUEARNOS con colliders ya "pierced"
            if (!TryCircleCastFiltered(origin, ballRadius, dir, moveDist + skin, bounceLayers, out RaycastHit2D hit))
            {
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

            float travel = Mathf.Max(0f, hit.distance - skin);

            if (travel > 0f)
            {
                rb.MovePosition(origin + dir * travel);
                remainingDistance -= travel;

                if (useFlame && flameCostPerUnit > 0f)
                    SpendFlame(travel * flameCostPerUnit);
            }

            moveDist = Mathf.Max(0f, moveDist - travel);

            bool broke = false;
            float remainingDmg = damagePool;

            if (damagePool > 0.0001f)
            {
                broke = ApplyPiercingImpact(hit.collider, dir, damagePool, out remainingDmg);
                damagePool = remainingDmg;
            }

            // ---------------- FIX: Pierce NO debe bloquearse por enemy, pero NO debe entrar en suelo ----------------
            if (broke)
            {
                float desiredPush = Mathf.Max(pierceSeparationMin, ballRadius * pierceSeparationRadiusFactor);
                desiredPush = Mathf.Min(desiredPush, pierceSeparationMax);

                // Solo mundo sólido. Además ignoramos el collider atravesado (enemy/rompible) para no quedarnos "pegados".
                float pushed = SafeAdvanceWithoutEnteringWorldSolids(dir, desiredPush, hit.collider);

                if (pushed > 0f)
                {
                    remainingDistance -= pushed;
                    moveDist = Mathf.Max(0f, moveDist - pushed);

                    if (useFlame && flameCostPerUnit > 0f)
                        SpendFlame(pushed * flameCostPerUnit);

                    CheckOutOfFlameAndEndIfNeeded();
                    if (!isBouncing) return;
                }

                if (remainingDistance <= 0f)
                {
                    EndBounce();
                    return;
                }

                // Seguimos recto (no rebote)
                if (moveDist <= 0f)
                    return;

                continue;
            }
            // ------------------------------------------------------------------------------------------------------

            // Rebote real (no pierced): reflect con la normal del collider
            bounceDir = Vector2.Reflect(dir, hit.normal).normalized;

            if (useFlame && flameCostPerBounce > 0f)
                SpendFlame(flameCostPerBounce);

            CheckOutOfFlameAndEndIfNeeded();
            if (!isBouncing) return;

            if (remainingDistance <= 0f)
                EndBounce();

            return;
        }

        CheckOutOfFlameAndEndIfNeeded();
        if (!isBouncing) return;

        if (remainingDistance <= 0f)
            EndBounce();
    }

    /// <summary>
    /// CircleCast filtrado: ignora colliders ya marcados como piercedThisBounce para que NO bloqueen el avance.
    /// </summary>
    private bool TryCircleCastFiltered(Vector2 origin, float radius, Vector2 dir, float distance, LayerMask mask, out RaycastHit2D bestHit)
    {
        bestHit = default;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, dir, distance, mask);

        float bestDist = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;

            // Si ya fue pierced en este bounce, NO debe bloquear el movimiento
            if (piercedThisBounce.Contains(h.collider))
                continue;

            // Elegimos el más cercano
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Avanza en dir una distancia como máximo "distance" SIN entrar en sólidos de mundo (worldSolidLayers).
    /// Ignora el collider recién atravesado para evitar quedarse pegado.
    /// Devuelve lo realmente avanzado.
    /// </summary>
    private float SafeAdvanceWithoutEnteringWorldSolids(Vector2 dir, float distance, Collider2D ignoreCol)
    {
        if (distance <= 0f) return 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        Vector2 origin = rb.position;

        int mask = worldSolidLayers.value;

        // fallback si no lo configuraste (NO recomendado), quitando enemy/hazard del bounceLayers
        if (mask == 0)
            mask = (bounceLayers.value & ~enemyLayers.value & ~hazardLayers.value);

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, ballRadius, dir, distance + skin, mask);

        float allowed = distance;

        if (hits != null && hits.Length > 0)
        {
            float best = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (!h.collider) continue;
                if (ignoreCol != null && h.collider == ignoreCol) continue;

                if (h.distance < best)
                {
                    best = h.distance;
                    found = true;
                }
            }

            if (found)
                allowed = Mathf.Max(0f, best - skin);
        }

        if (allowed > 0f)
        {
            rb.MovePosition(origin + dir * allowed);
            return allowed;
        }

        // micro-nudge si no podemos avanzar nada, para evitar quedarnos pegados al borde del enemy
        float tiny = Mathf.Min(pierceSeparationMin, distance);
        if (tiny > 0f)
        {
            RaycastHit2D block = Physics2D.CircleCast(origin, ballRadius, dir, tiny + skin, mask);
            if (!block.collider)
            {
                rb.MovePosition(origin + dir * tiny);
                return tiny;
            }
        }

        return 0f;
    }

    // Devuelve true si ese impacto se rompe / atraviesa (pierce)
    private bool ApplyPiercingImpact(Collider2D col, Vector2 direction, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = incomingDamage;
        if (col == null) return false;

        var receiverPierce = col.GetComponentInParent<IPiercingBounceReceiver>();
        if (receiverPierce != null)
        {
            // ya atravesado en ESTE bounce => no daño, pero atraviesa
            if (piercedThisBounce.Contains(col))
            {
                remainingDamage = incomingDamage;
                return true;
            }

            var impact = new BounceImpactData((int)Mathf.Ceil(incomingDamage), direction, gameObject);
            bool broke = receiverPierce.ApplyPiercingBounce(impact, incomingDamage, out float rem);
            remainingDamage = rem;

            // marcamos SIEMPRE para evitar multi-hit en el mismo bounce
            piercedThisBounce.Add(col);

            return broke;
        }

        // fallback sólido (no pierce)
        if (impactedThisBounce.Contains(col))
            return false;

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
            Debug.Log($"[Flame] -{amount:0.00} | flame={flame:0.00}/{maxFlame:0.00}");

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
        piercedThisBounce.Clear();

        flameSpentThisBounce = 0f;
        remainingDistance = maxDistance;

        ClearPreview();

        bounceDir = lastPreviewDir.sqrMagnitude > 0.001f ? lastPreviewDir.normalized : Vector2.right;

        if (invincibleDuringBounce)
            isInvincible = true;

        if (ignoreEnemyCollisionWhileBouncing)
            SetAimBounceCollisionIgnore(true);

        OnBounceStart?.Invoke();

    }

    private void EndAiming()
    {
        isAiming = false;
        ClearPreview();
        movement.movementLocked = false;

        if (!isBouncing)
            SetAimBounceCollisionIgnore(false);

    }

    private void EndBounce()
    {
        isBouncing = false;
        ClearPreview();

        movement.movementLocked = false;
        isInvincible = false;

        SetAimBounceCollisionIgnore(false);

        OnBounceEnd?.Invoke();   // <-- AÑADE ESTO
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

    // ================== IGNORE COLLISIONS DURING AIM/BOUNCE ==================

    private void SetAimBounceCollisionIgnore(bool ignore)
    {
        if (ignore == ignoringAimBounceCollisions) return;
        ignoringAimBounceCollisions = ignore;

        for (int i = 0; i < enemyLayerIds.Count; i++)
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIds[i], ignore);

        if (ignoreHazardDuringAimBounce)
        {
            for (int i = 0; i < hazardLayerIds.Count; i++)
                Physics2D.IgnoreLayerCollision(playerLayer, hazardLayerIds[i], ignore);
        }
    }
}
