using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(PlayerPhysicsStateController))]
[RequireComponent(typeof(Collider2D))]
public class PlayerSparkBoost : MonoBehaviour
{
    [Header("Input (Spark Boost)")]
    public KeyCode boostKey = KeyCode.X;
    public KeyCode boostPadKey = KeyCode.JoystickButton2;

    [Header("Aim (8-dir from Horizontal+Vertical)")]
    [Range(0f, 0.5f)] public float aimDeadzone = 0.25f;
    public bool keepLastAimWhenNoInput = true;

    [Header("Spark - Ventana")]
    public float defaultWindowDuration = 1.2f;
    [Range(0f, 1f)] public float goodStart = 0.60f;
    [Range(0f, 1f)] public float perfectStart = 0.85f;

    [Header("Anchor")]
    public bool anchorPlayerDuringSpark = true;
    public bool forceOwnRigidbodyState = true;

    [Header("Anti auto-dash")]
    public bool ignoreInputOnPickupFrame = true;
    [Range(0f, 0.2f)] public float pickupInputBlockTime = 0.06f;

    [Header("Pickup Bounce (rebote físico del pickup)")]
    public bool physicalBounceOnPickup = true;
    public float bounceUpSpeed = 10f;
    public float bounceNoAnchorTime = 0.10f;

    [Header("Dash (Spark)")]
    public float dashSpeed = 22f;
    [Range(0.02f, 0.60f)] public float dashDuration = 0.16f;
    public LayerMask dashCollisionMask = ~0;
    [Range(0f, 0.06f)] public float dashSkin = 0.015f;

    [Header("Dash Rebotes")]
    public bool dashBouncesOnWalls = true;
    [Range(0, 12)] public int dashMaxBounces = 6;

    [Header("Dash - Pickups (priority)")]
    [Range(0f, 0.25f)] public float pickupSweepExtra = 0.08f;
    public bool snapToPickupAnchorOnDash = true;

    [Header("Timing")]
    public float goodMultiplier = 1.15f;
    public float perfectMultiplier = 1.35f;
    public bool continuousTiming = false;

    [Header("Consume")]
    public bool consumeSparkOnUse = true;

    [Header("Preview Trajectory (AUTO)")]
    public LineRenderer sparkPreviewLine;
    public float previewWidth = 0.08f;
    public int previewSegments = 32;
    public int previewMaxBounces = 8;
    public string previewSortingLayer = "Default";
    public int previewSortingOrder = 9999;

    public Color previewColorMiss = new Color(1f, 0.8f, 0.2f, 0.95f);
    public Color previewColorGood = new Color(0.4f, 0.9f, 1f, 0.95f);
    public Color previewColorPerfect = new Color(1f, 0.2f, 0.2f, 0.95f);

    [Header("Ring Preview (Spark Window)")]
    public LineRenderer sparkRingLine;
    public float ringRadius = 0.60f;
    public int ringSegments = 48;
    public float ringWidth = 0.06f;
    public string ringSortingLayer = "Default";
    public int ringSortingOrder = 9998;

    [Header("Debug")]
    public bool debug = false;

    [Header("Debug Aim")]
    public bool debugAim = true;
    public float debugRayLength = 2.0f;
    public float debugLogEvery = 0.12f; // segundos (0 = cada frame)
    private float _debugNextLog;


    [Header("Spark - Hold to Aim (Pause Timer)")]
    public bool holdToAimPausesTimer = true;

    [Tooltip("Si true, el dash se ejecuta al SOLTAR la tecla (KeyUp) en lugar de al pulsar (KeyDown).")]
    public bool dashOnReleaseWhenHoldToAim = true;

    [Tooltip("Máximo tiempo total (seg) que puedes congelar el timer manteniendo X. 0 = infinito.")]
    public float maxTotalHoldPauseTime = 0f;

    private bool isHoldingAim;
    private float holdPauseSpent;



    private Rigidbody2D rb;
    private PlayerMovementController move;
    private PlayerPhysicsStateController phys;

    private bool sparkActive;
    private float sparkTimer;
    private float sparkTimerTotal;

    private bool anchorValid;
    private Vector2 sparkAnchorPos;

    private bool dashActive;
    private float dashTimer;
    private Vector2 dashDir;
    private float dashSpeedFinal;

    // Dash por distancia total (no por tiempo)
    private Vector2 dashStartPos;
    private float dashRemainingDist;


    private bool cachedMoveLockValid;
    private bool cachedMoveLockValue;

    private Vector2 lastAim = Vector2.up;

    private int pickupFrame = -999;
    private float noAnchorUntil = -1f;
    private float pickupInputBlockedUntil = -1f;

    private bool dashBuffered;

    // IMPORTANT: 1 dash por ventana
    private bool dashUsedThisSpark;

    // Backup del RB para no pelear con freezes
    private float prevGravity;
    private RigidbodyConstraints2D prevConstraints;
    private RigidbodyInterpolation2D prevInterp;
    private bool rbStateBackedUp;

    // Backup constraints durante dash (por si alguien te deja FreezeAll)
    private RigidbodyConstraints2D dashPrevConstraints;
    private bool dashBackedConstraints;

    // Cast buffers
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[32];

    public bool IsSparkActive() => sparkActive;
    public bool IsDashing() => dashActive;

    // Backup extra durante dash (para evitar curvatura por gravedad/interp)
    private float dashPrevGravity;
    private RigidbodyInterpolation2D dashPrevInterp;
    private bool dashBackedState;

    // Backup bodyType durante dash (para evitar que otros scripts metan física)
    private RigidbodyType2D dashPrevBodyType;
    private bool dashBackedBodyType;


    // Aim lock durante dash (para que la cruceta NO afecte el recorrido)
    private bool aimLocked;
    private Vector2 aimLockedDir = Vector2.up;



    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        move = GetComponent<PlayerMovementController>();
        phys = GetComponent<PlayerPhysicsStateController>();

        ConfigurePreviewAuto();
        ConfigureRingAuto();

        SetPreviewVisible(false);
        SetRingVisible(false);
    }

    private void OnDisable() => ForceEndAll();
    private void OnDestroy() => ForceEndAll();

    private void Update()
    {
        if (!sparkActive) return;

        // --- INPUT ---
        bool boostDown = Input.GetKeyDown(boostKey) || Input.GetKeyDown(boostPadKey);
        bool boostHeld = Input.GetKey(boostKey) || Input.GetKey(boostPadKey);
        bool boostUp   = Input.GetKeyUp(boostKey) || Input.GetKeyUp(boostPadKey);

        DebugAimSample("UPDATE");


        // --- HOLD MODE STATE ---
        if (holdToAimPausesTimer && boostDown && !dashActive)
            isHoldingAim = true;

        if (boostUp)
            isHoldingAim = false;

        // --- TIMER (pause while holding) ---
        bool pauseTimerNow = false;
        if (holdToAimPausesTimer && isHoldingAim && boostHeld && !dashActive)
        {
            if (maxTotalHoldPauseTime <= 0f) pauseTimerNow = true;
            else if (holdPauseSpent < maxTotalHoldPauseTime) pauseTimerNow = true;
        }

        if (pauseTimerNow)
            holdPauseSpent += Time.deltaTime;
        else
            sparkTimer -= Time.deltaTime;

        // --- EXPIRE ---
        if (sparkTimer <= 0f)
        {
            EndSparkWindow();
            RestoreMovementLock();
            RestoreRigidbodyStateIfNeeded();
            return;
        }

        // --- VISUALS ---
        UpdatePreviewWithBounces();
        UpdateRing();

        if (dashActive) return;

        if (ignoreInputOnPickupFrame && Time.frameCount == pickupFrame)
            return;

        // --- DASH TRIGGER ---
        // En hold-to-aim: dash al SOLTAR. Si no: dash al PULSAR como antes.
        bool tryDashNow;
        if (holdToAimPausesTimer && dashOnReleaseWhenHoldToAim)
            tryDashNow = boostUp;
        else
            tryDashNow = boostDown;

        if (tryDashNow)
        {
            if (IsInputTemporarilyBlocked())
            {
                dashBuffered = true;
                if (debug) Debug.Log("[SPARK] Dash buffered (blocked)");
            }
            else
            {
                TriggerDash();
                return;
            }
        }

        if (dashBuffered && !IsInputTemporarilyBlocked())
        {
            dashBuffered = false;
            TriggerDash();
        }
    }


    private bool IsInputTemporarilyBlocked()
    {
        if (Time.time < pickupInputBlockedUntil) return true;
        if (physicalBounceOnPickup && Time.time < noAnchorUntil) return true;
        return false;
    }

    private void FixedUpdate()
    {
        if (dashActive)
        {
            phys.RequestDash();
            DashStep(Time.fixedDeltaTime);
            return;
        }

        // ANCLA SOLO SI ESTÁS EN HOLD (cuando holdToAimPausesTimer está activo)
        bool allowAnchorNow = true;
        if (holdToAimPausesTimer)
            allowAnchorNow = isHoldingAim;

        if (sparkActive && anchorPlayerDuringSpark && anchorValid && allowAnchorNow)
        {
            // Durante el rebote del pickup, NO anclamos
            if (physicalBounceOnPickup && Time.time < noAnchorUntil)
                return;

            if (forceOwnRigidbodyState) EnsureRigidbodyAnchoredState();

            move.movementLocked = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.MovePosition(sparkAnchorPos);
        }
    }


    public void ActivateSpark(float duration) => ActivateSpark(duration, rb.position);

    public void ActivateSpark(float duration, Vector2 anchorWorldPos)
    {
        float dur = (duration > 0f) ? duration : defaultWindowDuration;

        // Si venimos de estados transitorios (dash / bounce / lo que sea), NO cachees el lock como "original".
        bool transientLock = dashActive; // añade aquí más condiciones si tienes flags (ej. bounceAttack.IsActive)

        if (!cachedMoveLockValid)
        {
            cachedMoveLockValue = transientLock ? false : move.movementLocked;
            cachedMoveLockValid = true;
        }

        // Guardarraíl: si alguien te trae aquí con el player bloqueado (bounce/dash), no aceptes ese lock heredado.
        if (transientLock)
            move.movementLocked = false;



        sparkActive = true;
        dashUsedThisSpark = false; // RESET: 1 dash por ventana
        sparkTimerTotal = Mathf.Max(0.05f, dur);
        sparkTimer = sparkTimerTotal;
        isHoldingAim = false;
        holdPauseSpent = 0f;


        sparkAnchorPos = anchorWorldPos;
        anchorValid = true;

        pickupFrame = Time.frameCount;
        pickupInputBlockedUntil = Time.time + Mathf.Max(0f, pickupInputBlockTime);
        dashBuffered = false;

        SetPreviewVisible(true);
        SetRingVisible(true);

        bool inBounceWindow = physicalBounceOnPickup && Time.time < noAnchorUntil;
        if (!inBounceWindow)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = sparkAnchorPos;

            if (forceOwnRigidbodyState)
                EnsureRigidbodyAnchoredState();

            move.movementLocked = true;
        }
        else
        {
            RestoreRigidbodyStateIfNeeded();
            move.movementLocked = false;
        }

        if (debug) Debug.Log($"[SPARK] ON dur={sparkTimerTotal:0.00}");
    }

    public void NotifyPickupBounce()
    {
        if (!physicalBounceOnPickup) return;

        RestoreRigidbodyStateIfNeeded();
        move.movementLocked = false;

        var v = rb.linearVelocity;
        v.y = Mathf.Max(v.y, bounceUpSpeed);
        rb.linearVelocity = v;

        noAnchorUntil = Time.time + Mathf.Max(0.01f, bounceNoAnchorTime);
    }

    private void TriggerDash()
    {
        // BLOQUEO: 1 dash por Spark
        if (dashUsedThisSpark) return;
        dashUsedThisSpark = true;

        RestoreRigidbodyStateIfNeeded();
        anchorValid = false;

        float progress = 1f - (sparkTimer / sparkTimerTotal);
        float mult = ComputeMultiplier(progress);

        DebugAimSample("BEFORE_DASH");

        dashDir = GetAimDirection8();
        if (dashDir.sqrMagnitude < 0.0001f) dashDir = Vector2.up;
        dashDir.Normalize();

        aimLocked = true;
        aimLockedDir = dashDir;

        DebugAimSample("AFTER_LOCK");


        dashSpeedFinal = dashSpeed * mult;

        dashStartPos = rb.position;
        dashRemainingDist = dashSpeedFinal * dashDuration;


        EndSparkWindow(); // apaga preview + ring, pero el dash empieza ya

        dashActive = true;
        dashTimer = Mathf.Max(0.01f, dashDuration);

        if (!dashBackedConstraints)
        {
            dashPrevConstraints = rb.constraints;
            dashBackedConstraints = true;
        }

        if (!dashBackedState)
        {
            dashPrevGravity = rb.gravityScale;
            dashPrevInterp  = rb.interpolation;
            dashBackedState = true;
        }

        if (!dashBackedBodyType)
        {
            dashPrevBodyType = rb.bodyType;
            dashBackedBodyType = true;
        }

        // Durante dash: kinematic (nadie te mete gravedad/velocidad)
        rb.bodyType = RigidbodyType2D.Kinematic;


        // DURANTE DASH: 0 gravedad y sin interpolation para que sea recto y coincida con la preview
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;

        rb.constraints = RigidbodyConstraints2D.None;


        move.movementLocked = true;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (debug) Debug.Log($"[SPARK] DASH speed={dashSpeedFinal:0.00} dur={dashTimer:0.00}");
    }

    private void DashStep(float dt)
    {
        // Defensa dura: durante dash no permitimos que nada meta velocidad
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        Vector2 pos = rb.position;
        Vector2 dir = dashDir;

        DebugAimSample("DASHSTEP");

        // Distancia de este tick, clamped a lo que queda del dash
        float step = Mathf.Min(dashRemainingDist, dashSpeedFinal * dt);
        float remaining = step;

        // =========================
        // PRIORIDAD PICKUP (rb.Cast)
        // =========================
        float pickupDist = float.MaxValue;
        FlameSparkPickup pickup = null;

        float pickupCastDist = remaining + dashSkin + pickupSweepExtra;

        var cfPick = new ContactFilter2D();
        cfPick.useLayerMask = false;
        cfPick.useTriggers = true;

        int pickCount = rb.Cast(dir, cfPick, castHits, pickupCastDist);
        for (int i = 0; i < pickCount; i++)
        {
            var h = castHits[i];
            if (h.collider == null) continue;

            var p = h.collider.GetComponent<FlameSparkPickup>() ?? h.collider.GetComponentInParent<FlameSparkPickup>();
            if (p == null) continue;

            float d = Mathf.Max(0f, h.distance);
            if (d < pickupDist)
            {
                pickupDist = d;
                pickup = p;
            }
        }

        int bounces = 0;

        while (remaining > 0f)
        {
            // ---- PICKUP hit dentro del remaining de este tick ----
            if (pickup != null && pickupDist <= remaining + dashSkin)
            {
                Vector2 anchor = pickup.GetAnchorWorld();

                // Cierra el dash BIEN (restaurando RB state) antes de entrar en Spark otra vez
                AbortDashToPickup();

                rb.position = snapToPickupAnchorOnDash
                    ? anchor
                    : (pos + dir * Mathf.Max(0f, pickupDist - dashSkin));

                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;

                pickup.Consume();

                NotifyPickupBounce();
                ActivateSpark(pickup.windowDuration, anchor);
                return;
            }


            // =========================
            // COLISIÓN MUNDO (rb.Cast)
            // =========================
            var cfWall = new ContactFilter2D();
            cfWall.useLayerMask = true;
            cfWall.layerMask = dashCollisionMask;
            cfWall.useTriggers = false;

            int hitCount = rb.Cast(dir, cfWall, castHits, remaining + dashSkin);

            RaycastHit2D hit = default;
            bool hasHit = false;
            float best = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var h = castHits[i];
                if (h.collider == null) continue;
                if (h.collider.isTrigger) continue;

                if (h.distance < best)
                {
                    best = h.distance;
                    hit = h;
                    hasHit = true;
                }
            }

            if (!hasHit)
            {
                pos += dir * remaining;
                remaining = 0f;
                break;
            }

            float travel = Mathf.Max(0f, hit.distance - dashSkin);

            // si está demasiado cerca, fuerza avance mínimo para no colapsar el rebote
            if (travel < 0.005f)
                travel = 0.02f;

            pos += dir * travel;
            remaining -= travel;

            if (!dashBouncesOnWalls || bounces >= dashMaxBounces)
            {
                remaining = 0f;
                break;
            }

            dir = Vector2.Reflect(dir, hit.normal).normalized;
            bounces++;

            // micro push para no quedarnos pegados
            pos += dir * 0.01f;

            // OJO: al cambiar de dirección, la búsqueda de pickup ya no vale
            pickup = null;
        }

        rb.position = pos;
        dashDir = dir;

        // Defensa dura otra vez
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // ======= FIN por distancia (NO por timer) =======
        dashRemainingDist -= step;
        if (dashRemainingDist <= 0f)
        {
            EndDash();
            return;
        }
    }

    private void AbortDashToPickup()
    {
        dashActive = false;
        dashTimer = 0f;
        dashRemainingDist = 0f;

        aimLocked = false;

        if (dashBackedConstraints)
        {
            rb.constraints = dashPrevConstraints;
            dashBackedConstraints = false;
        }

        if (dashBackedState)
        {
            rb.gravityScale = dashPrevGravity;
            rb.interpolation = dashPrevInterp;
            dashBackedState = false;
        }

        if (dashBackedBodyType)
        {
            rb.bodyType = dashPrevBodyType;
            dashBackedBodyType = false;
        }

        // CLAVE: el dash puso esto en true; suéltalo aquí sí o sí.
        move.movementLocked = false;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }





    private void EndDash()
    {
        dashActive = false;
        dashTimer = 0f;

        if (dashBackedConstraints)
        {
            rb.constraints = dashPrevConstraints;
            dashBackedConstraints = false;
        }

        if (dashBackedState)
        {
            rb.gravityScale = dashPrevGravity;
            rb.interpolation = dashPrevInterp;
            dashBackedState = false;
        }

        if (dashBackedBodyType)
        {
            rb.bodyType = dashPrevBodyType;
            dashBackedBodyType = false;
        }


        RestoreMovementLock();
        aimLocked = false;

    }


    private void EndSparkWindow()
    {
        sparkActive = false;
        sparkTimer = 0f;
        sparkTimerTotal = 0f;
        anchorValid = false;

        SetPreviewVisible(false);
        SetRingVisible(false);
    }

    private void RestoreMovementLock()
    {
        if (cachedMoveLockValid)
        {
            cachedMoveLockValid = false;
            RestoreMovementLock();
            aimLocked = false;

        }
        else
        {
            move.movementLocked = false;
        }
    }

    private void ForceEndAll()
    {
        sparkActive = false;
        dashActive = false;

        dashTimer = 0f;
        sparkTimer = 0f;
        sparkTimerTotal = 0f;
        anchorValid = false;
        dashBuffered = false;
        dashUsedThisSpark = false;

        SetPreviewVisible(false);
        SetRingVisible(false);

        RestoreMovementLock();
        RestoreRigidbodyStateIfNeeded();

        if (dashBackedState)
        {
            rb.gravityScale = dashPrevGravity;
            rb.interpolation = dashPrevInterp;
            dashBackedState = false;
        }


        if (dashBackedConstraints)
        {
            rb.constraints = dashPrevConstraints;
            dashBackedConstraints = false;
        }

        if (dashBackedBodyType)
        {
            rb.bodyType = dashPrevBodyType;
            dashBackedBodyType = false;
        }

        aimLocked = false;

    }

    private Vector2 GetAimDirection8()
    {
        if (dashActive && aimLocked)
            return aimLockedDir;

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(x) < aimDeadzone) x = 0f;
        if (Mathf.Abs(y) < aimDeadzone) y = 0f;

        Vector2 v = new Vector2(x, y);
        if (v.sqrMagnitude < 0.0001f)
            return keepLastAimWhenNoInput ? lastAim : Vector2.up;

        if (x != 0f && y != 0f)
            v = new Vector2(Mathf.Sign(x), Mathf.Sign(y)).normalized;
        else
            v = (x != 0f) ? new Vector2(Mathf.Sign(x), 0f) : new Vector2(0f, Mathf.Sign(y));

        lastAim = v;
        return v;
    }

    private float ComputeMultiplier(float progress)
    {
        if (!continuousTiming)
        {
            if (progress >= perfectStart) return perfectMultiplier;
            if (progress >= goodStart) return goodMultiplier;
            return 1f;
        }

        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        return Mathf.Lerp(1f, perfectMultiplier, t);
    }

    private Color ComputePhaseColor(float progress)
    {
        if (progress >= perfectStart) return previewColorPerfect;
        if (progress >= goodStart) return previewColorGood;
        return previewColorMiss;
    }

    // =========================
    // PREVIEW: Trayectoria rebotes
    // =========================

    private void ConfigurePreviewAuto()
    {
        if (sparkPreviewLine == null)
        {
            var go = new GameObject("SparkPreviewLine");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            sparkPreviewLine = go.AddComponent<LineRenderer>();
        }

        sparkPreviewLine.useWorldSpace = true;
        sparkPreviewLine.widthCurve = AnimationCurve.Constant(0f, 1f, previewWidth);

        Shader sh =
            Shader.Find("Universal Render Pipeline/2D/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        sparkPreviewLine.material = new Material(sh);

        sparkPreviewLine.sortingLayerName = previewSortingLayer;
        sparkPreviewLine.sortingOrder = previewSortingOrder;

        sparkPreviewLine.numCapVertices = 4;
        sparkPreviewLine.numCornerVertices = 2;

        sparkPreviewLine.positionCount = 0;
        sparkPreviewLine.enabled = false;
    }

    private void SetPreviewVisible(bool on)
    {
        if (sparkPreviewLine == null) return;
        sparkPreviewLine.enabled = on;
        if (!on) sparkPreviewLine.positionCount = 0;
    }

    private void UpdatePreviewWithBounces()
    {
        if (sparkPreviewLine == null || !sparkActive || sparkTimerTotal <= 0f) return;

        float progress = 1f - (sparkTimer / sparkTimerTotal);
        float mult = ComputeMultiplier(progress);
        Color c = ComputePhaseColor(progress);

        Vector2 dir = GetAimDirection8();
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();

        // Distancia total que recorrerá el dash (misma lógica conceptual que el movimiento)
        float totalDist = (dashSpeed * mult) * Mathf.Max(0.01f, dashDuration);

        Vector2 pos = rb.position;
        float remaining = totalDist;
        int bounces = 0;

        var pts = new List<Vector3>(previewSegments + 2) { pos };

        // Cast SOLO contra mundo (NO triggers), usando el mismo mask del dash
        var cfWall = new ContactFilter2D();
        cfWall.useLayerMask = true;
        cfWall.layerMask = dashCollisionMask;
        cfWall.useTriggers = false;

        // Recorremos como el dash: consumir "remaining" con rebotes reales
        while (remaining > 0f && bounces <= previewMaxBounces && pts.Count < (previewSegments + 2))
        {
            int hitCount = rb.Cast(dir, cfWall, castHits, remaining + dashSkin);

            RaycastHit2D hit = default;
            bool hasHit = false;
            float best = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var h = castHits[i];
                if (h.collider == null) continue;
                if (h.collider.isTrigger) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    hit = h;
                    hasHit = true;
                }
            }

            if (!hasHit)
            {
                // No hay colisión: recta final completa
                pos += dir * remaining;
                pts.Add(pos);
                remaining = 0f;
                break;
            }

            // Viaje hasta el impacto (skin)
            float travel = Mathf.Max(0f, hit.distance - dashSkin);

            // Si el impacto está pegado, forzamos avance mínimo para que el rebote se vea
            if (travel < 0.005f)
                travel = 0.02f;

            // Nos movemos al punto de impacto
            pos += dir * travel;
            pts.Add(pos);

            remaining -= travel;
            if (remaining <= 0f) break;

            if (!dashBouncesOnWalls) break;

            // Rebote real
            dir = Vector2.Reflect(dir, hit.normal).normalized;
            bounces++;

            // Micro push para no quedarnos “pegados” a la pared en preview
            pos += dir * 0.01f;
            pts.Add(pos);
        }

        sparkPreviewLine.positionCount = pts.Count;
        sparkPreviewLine.SetPositions(pts.ToArray());

        sparkPreviewLine.startColor = c;
        sparkPreviewLine.endColor = c;
        if (sparkPreviewLine.material != null) sparkPreviewLine.material.color = c;
    }


    // =========================
    // RING: arco de tiempo
    // =========================

    private void ConfigureRingAuto()
    {
        if (sparkRingLine == null)
        {
            var go = new GameObject("SparkRingLine");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            sparkRingLine = go.AddComponent<LineRenderer>();
        }

        sparkRingLine.useWorldSpace = true;
        sparkRingLine.widthCurve = AnimationCurve.Constant(0f, 1f, ringWidth);

        Shader sh =
            Shader.Find("Universal Render Pipeline/2D/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        sparkRingLine.material = new Material(sh);

        sparkRingLine.sortingLayerName = ringSortingLayer;
        sparkRingLine.sortingOrder = ringSortingOrder;

        sparkRingLine.numCapVertices = 4;
        sparkRingLine.numCornerVertices = 2;

        sparkRingLine.positionCount = 0;
        sparkRingLine.enabled = false;
    }

    private void SetRingVisible(bool on)
    {
        if (sparkRingLine == null) return;
        sparkRingLine.enabled = on;
        if (!on) sparkRingLine.positionCount = 0;
    }

    private void UpdateRing()
    {
        if (sparkRingLine == null || !sparkActive || sparkTimerTotal <= 0f) return;

        float progress = 1f - (sparkTimer / sparkTimerTotal);
        Color c = ComputePhaseColor(progress);

        float remaining01 = Mathf.Clamp01(sparkTimer / sparkTimerTotal);
        if (remaining01 <= 0f)
        {
            sparkRingLine.positionCount = 0;
            return;
        }

        int segs = Mathf.Max(8, Mathf.RoundToInt(ringSegments * remaining01));
        segs = Mathf.Clamp(segs, 8, ringSegments);

        Vector2 center = rb.position;

        float a0 = 0f;
        float a1 = Mathf.PI * 2f * remaining01;

        var pts = new Vector3[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float a = Mathf.Lerp(a0, a1, t);
            pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringRadius;
        }

        sparkRingLine.positionCount = pts.Length;
        sparkRingLine.SetPositions(pts);

        sparkRingLine.startColor = c;
        sparkRingLine.endColor = c;
        if (sparkRingLine.material != null) sparkRingLine.material.color = c;
    }

    // =========================
    // RB freeze / restore
    // =========================

    private void EnsureRigidbodyAnchoredState()
    {
        if (!rbStateBackedUp)
        {
            prevGravity = rb.gravityScale;
            prevInterp = rb.interpolation;
            prevConstraints = rb.constraints;
            rbStateBackedUp = true;
        }

        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void RestoreRigidbodyStateIfNeeded()
    {
        if (!rbStateBackedUp) return;

        rb.gravityScale = prevGravity;
        rb.interpolation = prevInterp;
        rb.constraints = prevConstraints;

        rbStateBackedUp = false;
    }

    private void DebugAimSample(string tag)
    {
        if (!debug || !debugAim) return;

        // Throttle
        if (debugLogEvery > 0f && Time.time < _debugNextLog) return;
        _debugNextLog = Time.time + Mathf.Max(0f, debugLogEvery);

        float rawX = Input.GetAxisRaw("Horizontal");
        float rawY = Input.GetAxisRaw("Vertical");

        // Aplicamos deadzone igual que en GetAimDirection8
        float dx = (Mathf.Abs(rawX) < aimDeadzone) ? 0f : rawX;
        float dy = (Mathf.Abs(rawY) < aimDeadzone) ? 0f : rawY;

        Vector2 raw = new Vector2(rawX, rawY);
        Vector2 dz  = new Vector2(dx, dy);

        // Cuantización 8-dir igual que tu GetAimDirection8
        Vector2 q;
        if (dz.sqrMagnitude < 0.0001f)
        {
            q = keepLastAimWhenNoInput ? lastAim : Vector2.up;
        }
        else if (dx != 0f && dy != 0f)
        {
            q = new Vector2(Mathf.Sign(dx), Mathf.Sign(dy)).normalized;
        }
        else
        {
            q = (dx != 0f) ? new Vector2(Mathf.Sign(dx), 0f) : new Vector2(0f, Mathf.Sign(dy));
        }

        Vector2 locked = aimLocked ? aimLockedDir : Vector2.zero;

        Debug.Log(
            $"[AIM {tag}] raw=({rawX:0.00},{rawY:0.00}) dz=({dx:0.00},{dy:0.00}) " +
            $"q=({q.x:0.00},{q.y:0.00}) last=({lastAim.x:0.00},{lastAim.y:0.00}) " +
            $"aimLocked={aimLocked} locked=({locked.x:0.00},{locked.y:0.00}) dashDir=({dashDir.x:0.00},{dashDir.y:0.00})"
        );

        // RAYs EN ESCENA
        Vector3 p = rb.position;
        Debug.DrawRay(p, (Vector3)(q.normalized * debugRayLength), Color.green, debugLogEvery > 0f ? debugLogEvery : 0f);
        if (aimLocked)
            Debug.DrawRay(p, (Vector3)(aimLockedDir.normalized * debugRayLength), Color.red, debugLogEvery > 0f ? debugLogEvery : 0f);
    }

}

