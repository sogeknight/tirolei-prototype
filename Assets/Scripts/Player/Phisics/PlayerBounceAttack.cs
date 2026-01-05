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
    public bool debugFlame = false;
    public bool debugOnScreen = true;

    [HideInInspector] public float flameSpentTotal = 0f;
    [HideInInspector] public float flameSpentThisBounce = 0f;

    [Header("Invencibilidad")]
    public bool invincibleDuringBounce = true;
    [HideInInspector] public bool isInvincible = false;

    [Header("Colisión (cast)")]
    [Tooltip("Capas que quieres 'golpear' con el CircleCast durante bounce + preview (suelo/pared, enemigos, rompibles...).")]
    public LayerMask bounceLayers;

    [Header("Mundo sólido (solo suelo/pared). NO enemigos.")]
    [Tooltip("Esto se usa SOLO para evitar meterte en sólidos al hacer post-pierce. Debe ser SOLO ground/walls.")]
    public LayerMask worldSolidLayers;

    [Tooltip("Skin para evitar stuck / casts a 0.")]
    public float skin = 0.02f;

    [Header("Evitar bloqueo por colisión con Enemy/Hazard durante Aim/Bounce")]
    public LayerMask enemyLayers;
    public LayerMask hazardLayers;

    public bool ignoreEnemyCollisionWhileAiming = true;
    public bool ignoreEnemyCollisionWhileBouncing = true;
    public bool ignoreHazardDuringAimBounce = true;

    private Collider2D playerCol;
    private int playerLayer;
    private readonly List<int> enemyLayerIds = new List<int>();
    private readonly List<int> hazardLayerIds = new List<int>();
    private bool ignoringAimBounceCollisions = false;

    [Header("Daño")]
    public int bounceDamage = 20;

    [Header("Preview: pierce prediction")]
    [Tooltip("Si es true, la preview intenta predecir si un bloque se rompe con 1 golpe (HP <= bounceDamage).")]
    public bool previewPredictPierceUsingWorldMaterialHP = true;

    [Tooltip("Si no se puede predecir (no hay WorldMaterial), qué hacemos en preview: true=asumir pierce, false=asumir rebote.")]
    public bool previewAssumePierceWhenUnknown = true;


    [Header("Preview trayectoria")]
    public LineRenderer previewLine;
    [Min(2)] public int previewSegments = 30;
    [Min(0)] public int previewMaxBounces = 5;

    [Header("Aim Snapping (solo + y X)")]
    public bool snapAimTo8Dirs = true;

    [Tooltip("Si el input está cerca de 0, mantenemos la última dirección válida.")]
    [Range(0.01f, 0.6f)] public float aimInputDeadzone = 0.20f;

    [Tooltip("Anti-drift: si un eje es muy pequeño, se anula (evita diagonales raras por drift del stick).")]
    [Range(0f, 0.25f)] public float driftCancel = 0.08f;

    [Header("Preview: visual")]
    public Color previewColor = Color.cyan;

    [Header("Rebote: corrección de normal (anti diagonales raras)")]
    [Range(0.5f, 0.99f)]
    public float normalSnapThreshold = 0.85f; // si |ny| > esto, tratamos como suelo/techo


    [Tooltip("Longitud visual de la preview (no afecta al ataque real). Si 0, usa maxDistance.")]
    public float previewDistance = 0f;

    [Header("Suavizado fin de trayecto (REAL bounce)")]
    [Range(0f, 1f)] public float slowDownFraction = 0.30f;
    [Range(0.05f, 1f)] public float minStepFactor = 0.20f;

    [Header("Pierce: avance seguro post-impacto")]
    public float pierceSeparationMin = 0.06f;
    public float pierceSeparationRadiusFactor = 0.7f;
    public float pierceSeparationMax = 0.25f;

    private Rigidbody2D rb;
    private PlayerMovementController movement;
    private CircleCollider2D circle;
    private PlayerPhysicsStateController phys;
    private PlayerSparkBoost spark;

    private bool isAiming = false;
    private bool isBouncing = false;

    private Vector2 lastAimDir = Vector2.right; // dirección “bloqueada” (8 dirs)
    private float remainingDistance = 0f;
    private float fixedStepSize = 0f;
    private float ballRadius = 0f;

    private Vector2 aimStartPosition;

    // Para evitar multi-hits / loops
    private readonly HashSet<Collider2D> impactedThisBounce = new HashSet<Collider2D>();
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

        playerCol = GetComponent<Collider2D>();
        playerLayer = gameObject.layer;

        if (circle == null) Debug.LogError("[PlayerBounceAttack] Falta CircleCollider2D.");
        if (phys == null) Debug.LogError("[PlayerBounceAttack] Falta PlayerPhysicsStateController en el Player.");

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
            if (((mask.value >> i) & 1) == 1)
                outList.Add(i);
    }

    /// <summary>
    /// CORTE DURO: se llama desde SparkBoost cuando el player recoge un Spark.
    /// Elimina cualquier trayecto pendiente del bounce y evita que continúe.
    /// </summary>
    public void ForceCancelFromSpark()
    {
        remainingDistance = 0f;

        if (isAiming) EndAiming();
        if (isBouncing) EndBounce();

        impactedThisBounce.Clear();
        piercedThisBounce.Clear();

        SetAimBounceCollisionIgnore(false);
        ClearPreview();

        movement.movementLocked = false;
        isInvincible = false;
    }

    private void Update()
    {
        // Si Spark está activo/dashing => bounce fuera inmediato.
        if (spark != null && (spark.IsSparkActive() || spark.IsDashing()))
        {
            ForceCancelFromSpark();
            return;
        }

        bool attackDown = Input.GetKeyDown(attackKey) || Input.GetKeyDown(attackPadKey);
        bool attackUp = Input.GetKeyUp(attackKey) || Input.GetKeyUp(attackPadKey);
        bool jumpDown = Input.GetKeyDown(movement.jumpKey) || Input.GetKeyDown(KeyCode.JoystickButton0);

        if (isAiming) phys.RequestBounceAiming();
        if (isBouncing) phys.RequestBounceBouncing();

        if (!isAiming && !isBouncing && attackDown)
        {
            if (!CanStartAiming()) return;
            StartAiming();

            if (ignoreEnemyCollisionWhileAiming)
                SetAimBounceCollisionIgnore(true);
        }

        if (isAiming)
            HandleAiming();

        if (isAiming && attackUp)
            StartBounce();

        if (isBouncing && (jumpDown || attackDown))
            EndBounce();
    }

    private void FixedUpdate()
    {
        // Corte duro también en FixedUpdate por si Spark se activa entre ticks
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

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

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

        // MOVIMIENTO REAL: usa exactamente la misma simulación que el preview (mismo cast, mismas reglas)
        SimStepResult step = SimulateOneStep(
            rb.position,
            lastAimDir,
            moveDist,
            bounceLayers,
            piercedThisBounce,
            isPreview: false,
            out Vector2 newPos,
            out Vector2 newDir,
            out Collider2D hitCol,
            out bool didBounce,
            out bool didPierce,
            out float traveled);

        // Mueve
        rb.MovePosition(newPos);

        // Consume distancia (lo realmente recorrido)
        remainingDistance -= traveled;
        if (useFlame && flameCostPerUnit > 0f && traveled > 0f)
            SpendFlame(traveled * flameCostPerUnit);

        if (didPierce)
        {
            // ya se añadió piercedThisBounce dentro de ApplyPiercingImpact
            // Mantiene dirección
        }
        else if (didBounce)
        {
            lastAimDir = newDir; // actualiza dir tras rebote (ya cuantizada)
            if (useFlame && flameCostPerBounce > 0f)
                SpendFlame(flameCostPerBounce);
        }

        CheckOutOfFlameAndEndIfNeeded();
        if (!isBouncing) return;

        if (remainingDistance <= 0f)
            EndBounce();
    }

    // ======================================================================
    // SIMULACIÓN UNIFICADA (REAL + PREVIEW)
    // ======================================================================

    private enum SimStepResult
    {
        NoHitMoved,
        HitPierced,
        HitBounced,
        HitBlockedNoMove
    }

    private SimStepResult SimulateOneStep(
        Vector2 startPos,
        Vector2 dir,
        float requestedMove,
        LayerMask mask,
        HashSet<Collider2D> previewOrRuntimePierced,
        bool isPreview,
        out Vector2 outPos,
        out Vector2 outDir,
        out Collider2D outHitCol,
        out bool didBounce,
        out bool didPierce,
        out float traveled)
    {
        outPos = startPos;
        outDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        outHitCol = null;
        didBounce = false;
        didPierce = false;
        traveled = 0f;

        float castDist = Mathf.Max(0f, requestedMove);
        if (castDist <= 0f)
            return SimStepResult.HitBlockedNoMove;

        // Cast
        if (!TryCircleCastFiltered(startPos, ballRadius, outDir, castDist + skin, mask, previewOrRuntimePierced, out RaycastHit2D hit) || hit.collider == null)
        {
            // no hit: avanzamos full
            outPos = startPos + outDir * castDist;
            traveled = castDist;
            return SimStepResult.NoHitMoved;
        }

        outHitCol = hit.collider;

        // Distancia hasta el contacto “útil”
        float travelToHit = Mathf.Max(0f, hit.distance - skin);

        // 1) Avanza hasta el hit (si hay espacio)
        if (travelToHit > 0f)
        {
            outPos = startPos + outDir * travelToHit;
            traveled = travelToHit;
        }
        else
        {
            outPos = startPos;
            traveled = 0f;
        }

        // 2) Contacto inmediato (ground / pegado): resolver de forma determinista
        bool inContact = (hit.distance <= skin * 1.25f);

        // En PREVIEW no aplicamos daño real; en REAL sí.
        bool pierceable = IsPierceable(hit.collider);

        if (pierceable)
        {
            bool pierced = false;

            if (isPreview)
            {
                pierced = PredictPiercePreview(hit.collider, bounceDamage);
            }
            else
            {
                pierced = ApplyPiercingImpact(hit.collider, outDir, bounceDamage, out _);
            }


            if (pierced)
            {
                didPierce = true;

                // Evita loops: marca como “pierced” en ambos modos para que el cast lo ignore después
                if (previewOrRuntimePierced != null)
                    previewOrRuntimePierced.Add(hit.collider);

                // Empuje post-pierce (salir del collider)
                float desiredPush = Mathf.Max(pierceSeparationMin, ballRadius * pierceSeparationRadiusFactor);
                desiredPush = Mathf.Min(desiredPush, pierceSeparationMax);

                float pushed = isPreview
                    ? PreviewSafeAdvanceWithoutEnteringWorldSolids(outPos, outDir, desiredPush, hit.collider)
                    : SafeAdvanceWithoutEnteringWorldSolids(outDir, desiredPush, hit.collider);

                if (pushed < 0.0001f)
                    pushed = Mathf.Min(desiredPush, pierceSeparationMin);

                outPos = outPos + outDir * pushed;
                traveled += pushed;

                // Dirección no cambia
                return SimStepResult.HitPierced;
            }
        }

        // 3) Si no pierce: rebote
        didBounce = true;

        outDir = ComputeBouncedDir(outDir, hit.normal);


        // Si estamos pegados (hit.distance ~ 0), NO lo trates como "bloqueado":
        // empuja fuera y consume un pelín de longitud para que la preview pueda continuar
        if (inContact && traveled <= 0.0001f)
        {
            float pushOut = Mathf.Max(skin * 3f, 0.02f);

            // empuja fuera del suelo
            outPos = startPos + hit.normal * pushOut;

            // consume algo para evitar loops infinitos en la simulación de preview
            traveled = pushOut;
        }
        else if (inContact)
        {
            // caso normal: ya avanzaste algo, pero igual conviene sacar un pelo fuera
            outPos = outPos + hit.normal * Mathf.Max(skin * 3f, 0.02f);
        }

        return SimStepResult.HitBounced;

    }

    private bool TryCircleCastFiltered(
        Vector2 origin,
        float radius,
        Vector2 dir,
        float distance,
        LayerMask mask,
        HashSet<Collider2D> ignoreSet,
        out RaycastHit2D bestHit)
    {
        bestHit = default;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, dir, distance, mask);

        float bestDist = float.PositiveInfinity;
        bool found = false;

        const float TIE_EPS = 0.01f;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;
            if (h.collider.isTrigger) continue;
            if (playerCol != null && h.collider == playerCol) continue;

            if (ignoreSet != null && ignoreSet.Contains(h.collider)) continue;

            float d = h.distance;

            if (!found)
            {
                bestDist = d;
                bestHit = h;
                found = true;
                continue;
            }

            if (d + TIE_EPS < bestDist)
            {
                bestDist = d;
                bestHit = h;
            }
        }

        return found;
    }

    // ======================================================================
    // RUNTIME PIERCE + POST-PUSH
    // ======================================================================

    private bool IsPierceable(Collider2D col)
    {
        return col != null && col.GetComponentInParent<IPiercingBounceReceiver>() != null;
    }

    private bool ApplyPiercingImpact(Collider2D col, Vector2 direction, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = incomingDamage;
        if (col == null) return false;

        // Si ya lo atravesaste en este bounce, ignóralo como sólido
        if (piercedThisBounce.Contains(col))
            return true;

        // Evita micro-hits repetidos sobre el mismo collider
        if (impactedThisBounce.Contains(col))
            return false;

        var receiverPierce = col.GetComponentInParent<IPiercingBounceReceiver>();
        if (receiverPierce != null)
        {
            impactedThisBounce.Add(col);

            var impact = new BounceImpactData((int)Mathf.Ceil(incomingDamage), direction, gameObject);
            bool pierced = receiverPierce.ApplyPiercingBounce(impact, incomingDamage, out float rem);
            remainingDamage = rem;

            if (pierced)
                piercedThisBounce.Add(col);

            return pierced;
        }

        // Impacto normal (no pierce)
        var receiver = col.GetComponentInParent<IBounceImpactReceiver>();
        if (receiver != null)
        {
            receiver.ReceiveBounceImpact(new BounceImpactData(bounceDamage, direction, gameObject));
            impactedThisBounce.Add(col);
        }

        remainingDamage = 0f;
        return false;
    }

    private bool PredictPiercePreview(Collider2D col, float incomingDamage)
    {
        if (!previewPredictPierceUsingWorldMaterialHP)
            return previewAssumePierceWhenUnknown;

        if (col == null) return previewAssumePierceWhenUnknown;

        // Caso "ideal": WorldMaterial define HP real
        WorldMaterial wm = col.GetComponentInParent<WorldMaterial>();
        if (wm != null)
        {
            if (wm.indestructible) return false;
            return incomingDamage >= wm.structuralHP;
        }

        // Si no hay WorldMaterial, no podemos saber.
        return previewAssumePierceWhenUnknown;
    }


    private float SafeAdvanceWithoutEnteringWorldSolids(Vector2 dir, float distance, Collider2D ignoreCol)
    {
        if (distance <= 0f) return 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        Vector2 origin = rb.position;

        int mask = worldSolidLayers.value;
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
                if (h.collider.isTrigger) continue;

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
            // Importante: este método se usa dentro del tick de bounce (ya movimos), así que NO hacemos MovePosition aquí.
            // Solo devolvemos “cuánto se puede empujar” y el caller ajusta la posición virtual.
            return allowed;
        }

        float tiny = Mathf.Min(pierceSeparationMin, distance);
        if (tiny > 0f)
        {
            RaycastHit2D block = Physics2D.CircleCast(origin, ballRadius, dir, tiny + skin, mask);
            if (!block.collider)
                return tiny;
        }

        return 0f;
    }

    private float PreviewSafeAdvanceWithoutEnteringWorldSolids(Vector2 origin, Vector2 dir, float distance, Collider2D ignoreCol)
    {
        if (distance <= 0f) return 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        int mask = worldSolidLayers.value;
        if (mask == 0) mask = bounceLayers.value;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, ballRadius, dir, distance + skin, mask);

        float allowed = distance;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;
            if (h.collider.isTrigger) continue;
            if (ignoreCol != null && h.collider == ignoreCol) continue;

            allowed = Mathf.Min(allowed, Mathf.Max(0f, h.distance - skin));
        }

        return allowed;
    }

    // ======================================================================
    // LLAMA
    // ======================================================================

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
        if (flameCostStart > 0f) return flame >= flameCostStart;
        return flame > 0f;
    }

    private void CheckOutOfFlameAndEndIfNeeded()
    {
        if (!useFlame || !endBounceWhenOutOfFlame) return;
        if (flame > 0f) return;
        EndBounce();
    }

    // ======================================================================
    // ESTADOS AIM / BOUNCE
    // ======================================================================

    private void StartAiming()
    {
        isAiming = true;

        lastAimDir = Vector2.right;

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

        // Deadzone real
        if (input.sqrMagnitude < (aimInputDeadzone * aimInputDeadzone))
        {
            // mantener
            input = lastAimDir;
        }
        else
        {
            input.Normalize();
        }

        if (snapAimTo8Dirs)
            input = Quantize8Dirs(input);

        lastAimDir = (input.sqrMagnitude > 0.0001f) ? input : Vector2.right;

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

        movement.movementLocked = true;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        impactedThisBounce.Clear();
        piercedThisBounce.Clear();

        flameSpentThisBounce = 0f;
        remainingDistance = maxDistance;

        ClearPreview();

        lastAimDir = (lastAimDir.sqrMagnitude > 0.0001f) ? lastAimDir.normalized : Vector2.right;
        if (snapAimTo8Dirs) lastAimDir = Quantize8Dirs(lastAimDir);

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

        OnBounceEnd?.Invoke();
    }

    // ======================================================================
    // QUANTIZE (solo 8 dirs: + y X)
    // ======================================================================

    private Vector2 Quantize8Dirs(Vector2 v)
    {
        float x = v.x;
        float y = v.y;

        // anti drift
        float d = Mathf.Max(0.001f, driftCancel);
        if (Mathf.Abs(x) < d) x = 0f;
        if (Mathf.Abs(y) < d) y = 0f;

        if (Mathf.Abs(x) < 0.0001f && Mathf.Abs(y) < 0.0001f)
            return Vector2.right;

        // 4 card + 4 diag
        if (Mathf.Abs(x) < 0.0001f)
            return (y >= 0f) ? Vector2.up : Vector2.down;

        if (Mathf.Abs(y) < 0.0001f)
            return (x >= 0f) ? Vector2.right : Vector2.left;

        float sx = (x >= 0f) ? 1f : -1f;
        float sy = (y >= 0f) ? 1f : -1f;
        return new Vector2(sx, sy).normalized;
    }

    // ======================================================================
    // PREVIEW (usa la MISMA simulación que FixedUpdate)
    // ======================================================================

    private void UpdatePreview()
    {
        if (!isAiming || previewLine == null)
        {
            ClearPreview();
            return;
        }

        Vector2 dir = (lastAimDir.sqrMagnitude > 0.001f) ? lastAimDir.normalized : Vector2.right;
        if (snapAimTo8Dirs) dir = Quantize8Dirs(dir);

        float previewLen = (previewDistance > 0.01f) ? previewDistance : maxDistance;
        float remaining = previewLen;

        float stepDist = fixedStepSize;
        if (stepDist <= 0f) stepDist = previewLen / Mathf.Max(1, previewSegments);

        // Conjunto “pierced” del preview para evitar loops
        HashSet<Collider2D> previewPierced = new HashSet<Collider2D>();

        List<Vector3> points = new List<Vector3>(previewSegments + 2);
        Vector2 pos = rb.position;
        points.Add(pos);

        int bounces = 0;
        int safety = 0;
        const int MAX_ITERS = 512;

        while (remaining > 0.0001f && bounces <= previewMaxBounces && safety++ < MAX_ITERS)
        {
            float move = Mathf.Min(stepDist, remaining);

            SimStepResult res = SimulateOneStep(
                pos,
                dir,
                move,
                bounceLayers,
                previewPierced,
                isPreview: true,
                out Vector2 newPos,
                out Vector2 newDir,
                out _,
                out bool didBounce,
                out bool didPierce,
                out float traveled);

            // Si no se movió nada y está bloqueado, rompe el loop
            if (traveled <= 0.0001f && res == SimStepResult.HitBlockedNoMove)
            {
                // añade un pelín visual para no “desaparecer”
                Vector2 tiny = pos + dir * Mathf.Min(remaining, Mathf.Max(0.05f, skin * 3f));
                points.Add(tiny);
                pos = tiny;
                remaining = 0f;
                break;
            }

            points.Add(newPos);

            remaining -= traveled;
            pos = newPos;

            if (didPierce)
            {
                // dir igual
            }
            else if (didBounce)
            {
                dir = newDir;
                bounces++;
            }

            if (remaining <= 0f) break;
        }

        // Si aún queda, extiende recto para longitud exacta
        if (remaining > 0.0001f)
        {
            Vector2 end = pos + dir * remaining;
            points.Add(end);
        }

        // Resample fijo para que la línea NO cambie de “larga/corta” por la cantidad de puntos
        int targetCount = Mathf.Max(2, previewSegments + 1);
        var sampled = ResamplePolylineFixedCount(points, targetCount, previewLen);

        previewLine.positionCount = sampled.Count;
        previewLine.SetPositions(sampled.ToArray());
        previewLine.enabled = true;
    }

    private List<Vector3> ResamplePolylineFixedCount(List<Vector3> src, int targetCount, float totalLength)
    {
        List<Vector3> outPts = new List<Vector3>(Mathf.Max(2, targetCount));
        if (src == null || src.Count == 0) return outPts;
        if (targetCount < 2) targetCount = 2;

        // Longitudes acumuladas
        List<float> cum = new List<float>(src.Count);
        cum.Add(0f);

        float acc = 0f;
        for (int i = 1; i < src.Count; i++)
        {
            acc += Vector3.Distance(src[i - 1], src[i]);
            cum.Add(acc);
        }

        float maxLen = Mathf.Max(0.0001f, Mathf.Min(totalLength, acc));
        float step = maxLen / (targetCount - 1);

        int seg = 0;
        for (int k = 0; k < targetCount; k++)
        {
            float d = Mathf.Min(maxLen, step * k);

            while (seg < cum.Count - 2 && cum[seg + 1] < d)
                seg++;

            float d0 = cum[seg];
            float d1 = cum[seg + 1];
            float t = (d1 <= d0) ? 0f : (d - d0) / (d1 - d0);

            Vector3 p = Vector3.Lerp(src[seg], src[seg + 1], t);
            outPts.Add(p);
        }

        return outPts;
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

        previewLine.startColor = previewColor;
        previewLine.endColor = previewColor;

        previewLine.sortingLayerName = "Default";
        previewLine.sortingOrder = 100;

        previewLine.positionCount = 0;
        previewLine.enabled = false;
    }

    // ======================================================================
    // IGNORE COLLISIONS DURING AIM/BOUNCE
    // ======================================================================

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


    private Vector2 ComputeBouncedDir(Vector2 incomingDir, Vector2 normal)
    {
        incomingDir = (incomingDir.sqrMagnitude > 0.0001f) ? incomingDir.normalized : Vector2.right;
        normal = (normal.sqrMagnitude > 0.0001f) ? normal.normalized : Vector2.up;

        // Mantén coherencia con tu sistema: decide si el input era cardinal o diagonal
        Vector2 qIn = Quantize8Dirs(incomingDir);

        bool inputIsCardinal =
            (Mathf.Abs(qIn.x) < 0.0001f && Mathf.Abs(qIn.y) > 0.0001f) ||   // up/down
            (Mathf.Abs(qIn.y) < 0.0001f && Mathf.Abs(qIn.x) > 0.0001f);     // left/right

        // Suelo/techo: solo fuerza vertical PERFECTO si el input era cardinal (UP o DOWN)
        if (Mathf.Abs(normal.y) >= normalSnapThreshold && Mathf.Abs(normal.y) >= Mathf.Abs(normal.x))
        {
            if (inputIsCardinal)
            {
                if (qIn.y < 0f) return Vector2.up;    // down -> up
                if (qIn.y > 0f) return Vector2.down;  // up -> down
            }

            // Si era diagonal, conserva la lógica reflect (y luego cuantiza a 8 dirs)
            return Quantize8Dirs(Vector2.Reflect(qIn, normal));
        }

        // Pared: solo fuerza horizontal PERFECTO si el input era cardinal (LEFT o RIGHT)
        if (Mathf.Abs(normal.x) >= normalSnapThreshold && Mathf.Abs(normal.x) >= Mathf.Abs(normal.y))
        {
            if (inputIsCardinal)
            {
                if (qIn.x < 0f) return Vector2.right; // left -> right
                if (qIn.x > 0f) return Vector2.left;  // right -> left
            }

            return Quantize8Dirs(Vector2.Reflect(qIn, normal));
        }

        // Caso general
        return Quantize8Dirs(Vector2.Reflect(qIn, normal));
    }


}
