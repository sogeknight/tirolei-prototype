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

    [Header("Dash Tuning")]
    [Range(0.5f, 1f)] public float dashDistanceScale = 1f;

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

    [Header("Preview Safety Margin")]
    [Tooltip("Multiplicador SOLO para la PREVIEW (seguridad). 0.97 = preview 3% más corta que el dash real.")]
    [Range(0.10f, 1.00f)] public float previewSafetyMultiplier = 0.99f; // empieza por 0.99

    [Header("Preview Pickup Marker")]
    public bool previewMarkNextPickup = true;
    public LineRenderer pickupPreviewCircle;
    public float pickupCircleRadius = 0.35f;
    public int pickupCircleSegments = 32;
    public float pickupCircleWidth = 0.06f;
    public string pickupCircleSortingLayer = "Default";
    public int pickupCircleSortingOrder = 10000;

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
    public float debugLogEvery = 0.12f;
    private float _debugNextLog;

    // =========================
    // DEBUG - Freeze Preview + Permanent Trail
    // =========================
    [Header("DEBUG - Freeze Preview After Dash (permanent)")]
    public bool debugFreezePreview = true;
    public bool debugFreezeOnDashStart = true;
    public int frozenLineSortingOrder = 20000;
    public float frozenLineWidth = 0.08f;

    [Header("DEBUG - Frozen Dash Trail (permanent real path)")]
    public bool debugFrozenDashTrail = true;
    public int frozenTrailMaxPoints = 512;
    public float frozenTrailMinStep = 0.03f;
    public float frozenTrailWidth = 0.06f;
    public int frozenTrailSortingOrder = 20001;

    [Header("DEBUG - Hotkeys")]
    public KeyCode debugClearAllKey = KeyCode.F8;
    public KeyCode debugClearFrozenPreviewKey = KeyCode.F9;
    public KeyCode debugClearFrozenTrailKey = KeyCode.F10;

    // =========================
    // DEBUG - Dash Trail (TrailRenderer normal - NO permanente)
    // =========================
    [Header("DEBUG - Dash Trail (ephemeral)")]
    public bool debugDashTrail = true;
    [Tooltip("Cuánto dura el rastro (segundos). (Esto NO será permanente.)")]
    [Range(0.02f, 0.5f)] public float dashTrailTime = 0.12f;
    [Range(0.001f, 0.5f)] public float dashTrailStartWidth = 0.10f;
    [Range(0.001f, 0.5f)] public float dashTrailEndWidth = 0.02f;
    public Color dashTrailColor = new Color(1f, 1f, 1f, 0.85f);
    public string dashTrailSortingLayer = "Default";
    public int dashTrailSortingOrder = 10001;

    private TrailRenderer dashTrail;

    [Header("Spark - Hold to Aim (Pause Timer)")]
    public bool holdToAimPausesTimer = true;
    public bool dashOnReleaseWhenHoldToAim = true;
    public float maxTotalHoldPauseTime = 0f;

    // =========================
    // Anti “impulso acumulado” de BounceAttack
    // =========================
    [Header("Anti BounceAttack Accumulated Impulse")]
    public bool absorbResidualImpulseFromBounceAttack = true;
    [Range(0, 12)] public int residualAbsorbFrames = 6;
    [Range(0f, 0.20f)] public float residualAbsorbTime = 0.05f;

    // =========================
    // BounceAttack Restore Guard
    // =========================
    [Header("BounceAttack Restore Guard")]
    [Range(0f, 0.30f)] public float bounceRestoreDelay = 0.12f;
    [Range(0, 20)] public int postRestoreAbsorbFrames = 8;
    [Range(0f, 0.30f)] public float postRestoreAbsorbTime = 0.10f;

    private int residualAbsorbFramesLeft = 0;
    private float residualAbsorbUntilTime = -1f;

    private bool pendingRestoreBounce;
    private float pendingRestoreBounceUntil = -1f;

    private bool postRestoreGuardActive;
    private float postRestoreGuardUntil = -1f;

    private bool isHoldingAim;
    private float holdPauseSpent;

    private Rigidbody2D rb;
    private PlayerMovementController move;
    private PlayerPhysicsStateController phys;
    private Collider2D playerCol;

    private bool sparkActive;
    private float sparkTimer;
    private float sparkTimerTotal;

    private bool anchorValid;
    private Vector2 sparkAnchorPos;

    private bool dashActive;
    private float dashTimer;
    private Vector2 dashDir;
    private float dashSpeedFinal;
    private float dashRemainingDist;
    private int dashBouncesUsed; // rebotes usados en TODO el dash (no por frame)

    private bool cachedMoveLockValid;
    private bool cachedMoveLockValue;

    private Vector2 lastAim = Vector2.up;

    private int pickupFrame = -999;
    private float noAnchorUntil = -1f;
    private float pickupInputBlockedUntil = -1f;

    private bool dashBuffered;
    private bool dashUsedThisSpark;

    private float prevGravity;
    private RigidbodyConstraints2D prevConstraints;
    private RigidbodyInterpolation2D prevInterp;
    private bool rbStateBackedUp;

    private RigidbodyConstraints2D dashPrevConstraints;
    private bool dashBackedConstraints;

    private float dashPrevGravity;
    private RigidbodyInterpolation2D dashPrevInterp;
    private bool dashBackedState;

    private RigidbodyType2D dashPrevBodyType;
    private bool dashBackedBodyType;

    private float baseGravity;
    private RigidbodyConstraints2D baseConstraints;
    private RigidbodyInterpolation2D baseInterp;
    private RigidbodyType2D baseBodyType;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[64];

    public bool IsSparkActive() => sparkActive;
    public bool IsDashing() => dashActive;

    private bool aimLocked;
    private Vector2 aimLockedDir = Vector2.up;

    private BoxCollider2D boxCol;
    private CapsuleCollider2D capsuleCol;
    private CircleCollider2D circleCol;

    private PlayerBounceAttack bounceAtk;
    private bool bounceAtkHadComponent;
    private bool bounceAtkWasEnabled;
    private bool bounceAtkStateCached;

    private bool forceUnlockOnSparkExit;

    // ========= DEBUG persistent objects =========
    private LineRenderer frozenPreviewLine;
    private bool frozenHasData;

    private LineRenderer frozenTrailLine;
    private readonly List<Vector3> frozenTrailPts = new List<Vector3>(512);
    private Vector3 lastFrozenTrailPt;
    private bool frozenTrailRecording;

    // =========================
    // FIX PREVIEW VISIBILITY (compat)
    // =========================
    [Header("Preview - Safety")]
    [Tooltip("Ya NO se usa para empujar la preview cuando el dash real no puede avanzar. Se mantiene por compatibilidad.")]
    public bool previewForceMinSegment = true;

    [Tooltip("Ya NO se usa para empujar la preview cuando el dash real no puede avanzar. Se mantiene por compatibilidad.")]
    [Range(0.01f, 0.50f)] public float previewMinSegmentLength = 0.12f;

    // =========================
    // Collider world-scale helpers (CLAVE)
    // =========================
    private Vector2 AbsLossyScale2D()
    {
        Vector3 s = transform.lossyScale;
        return new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
    }

    private Vector2 ScaleOffset(Vector2 localOffset)
    {
        Vector2 s = AbsLossyScale2D();
        return new Vector2(localOffset.x * s.x, localOffset.y * s.y);
    }

    private Vector2 ScaleSize(Vector2 localSize)
    {
        Vector2 s = AbsLossyScale2D();
        return new Vector2(localSize.x * s.x, localSize.y * s.y);
    }

    private float ScaleRadius(float localRadius)
    {
        Vector2 s = AbsLossyScale2D();
        return localRadius * Mathf.Max(s.x, s.y);
    }

    // =========================
    // BounceAttack cache / restore
    // =========================
    private void CacheBounceAttack()
    {
        bounceAtk = GetComponent<PlayerBounceAttack>();
        bounceAtkHadComponent = (bounceAtk != null);
        bounceAtkStateCached = false;
    }

    private void SendBounceHardResets()
    {
        if (!bounceAtkHadComponent || bounceAtk == null) return;

        bounceAtk.SendMessage("ForceCancel", SendMessageOptions.DontRequireReceiver);
        bounceAtk.SendMessage("Cancel", SendMessageOptions.DontRequireReceiver);
        bounceAtk.SendMessage("Abort", SendMessageOptions.DontRequireReceiver);
        bounceAtk.SendMessage("ResetAccumulation", SendMessageOptions.DontRequireReceiver);
        bounceAtk.SendMessage("ResetCharge", SendMessageOptions.DontRequireReceiver);
    }

    private void DisableBounceAttackHard()
    {
        if (!bounceAtkHadComponent || bounceAtk == null) return;

        SendBounceHardResets();

        if (!bounceAtkStateCached)
        {
            bounceAtkWasEnabled = bounceAtk.enabled;
            bounceAtkStateCached = true;
        }

        bounceAtk.enabled = false;
    }

    private void StartResidualAbsorbIfNeeded()
    {
        if (!absorbResidualImpulseFromBounceAttack) return;

        residualAbsorbFramesLeft = Mathf.Max(residualAbsorbFramesLeft, residualAbsorbFrames);
        if (residualAbsorbTime > 0f)
            residualAbsorbUntilTime = Mathf.Max(residualAbsorbUntilTime, Time.time + residualAbsorbTime);
    }

    private void StartPostRestoreGuard()
    {
        if (!absorbResidualImpulseFromBounceAttack) return;

        residualAbsorbFramesLeft = Mathf.Max(residualAbsorbFramesLeft, postRestoreAbsorbFrames);
        if (postRestoreAbsorbTime > 0f)
            residualAbsorbUntilTime = Mathf.Max(residualAbsorbUntilTime, Time.time + Mathf.Max(0.01f, postRestoreAbsorbTime));

        postRestoreGuardActive = true;
        postRestoreGuardUntil = Mathf.Max(postRestoreGuardUntil, Time.time + Mathf.Max(0.01f, postRestoreAbsorbTime));
    }

    private bool IsResidualAbsorbActive()
    {
        if (!absorbResidualImpulseFromBounceAttack) return false;
        if (residualAbsorbFramesLeft > 0) return true;
        if (residualAbsorbUntilTime > 0f && Time.time < residualAbsorbUntilTime) return true;
        return false;
    }

    private void ApplyResidualAbsorb()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void ScheduleBounceRestoreDeferred()
    {
        if (!bounceAtkHadComponent || bounceAtk == null) return;
        if (!bounceAtkStateCached) return;

        pendingRestoreBounce = true;
        pendingRestoreBounceUntil = Mathf.Max(
            pendingRestoreBounceUntil,
            Time.time + Mathf.Max(0f, bounceRestoreDelay)
        );
    }

    private void RestoreBounceAttackIfNeeded()
    {
        if (!bounceAtkHadComponent || bounceAtk == null) return;

        if (IsResidualAbsorbActive())
        {
            ScheduleBounceRestoreDeferred();
            return;
        }

        if (pendingRestoreBounce && pendingRestoreBounceUntil > 0f && Time.time < pendingRestoreBounceUntil)
            return;

        if (bounceAtkStateCached)
        {
            SendBounceHardResets();

            bounceAtk.enabled = bounceAtkWasEnabled;
            bounceAtkStateCached = false;

            SendBounceHardResets();
            StartPostRestoreGuard();
        }

        pendingRestoreBounce = false;
        pendingRestoreBounceUntil = -1f;
    }

    // =========================
    // Unity events
    // =========================
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        move = GetComponent<PlayerMovementController>();
        phys = GetComponent<PlayerPhysicsStateController>();
        playerCol = GetComponent<Collider2D>();

        CacheBounceAttack();

        boxCol = playerCol as BoxCollider2D;
        capsuleCol = playerCol as CapsuleCollider2D;
        circleCol = playerCol as CircleCollider2D;

        baseGravity = rb.gravityScale;
        baseConstraints = rb.constraints;
        baseInterp = rb.interpolation;
        baseBodyType = rb.bodyType;

        ConfigurePreviewAuto();
        ConfigureRingAuto();
        ConfigurePickupCircleAuto();

        SetupDashTrail();

        SetPreviewVisible(false);
        SetRingVisible(false);
        SetPickupCircleVisible(false);
    }

    private void OnDisable() => ForceEndAll();
    private void OnDestroy() => ForceEndAll();

    private void Update()
    {
        if (debug)
        {
            if (Input.GetKeyDown(debugClearAllKey))
            {
                DebugClearFrozenPreview();
                DebugClearFrozenTrail();
            }
            if (Input.GetKeyDown(debugClearFrozenPreviewKey))
                DebugClearFrozenPreview();
            if (Input.GetKeyDown(debugClearFrozenTrailKey))
                DebugClearFrozenTrail();
        }

        if (IsResidualAbsorbActive())
        {
            ApplyResidualAbsorb();
            if (residualAbsorbFramesLeft > 0) residualAbsorbFramesLeft--;
        }

        if (pendingRestoreBounce || bounceAtkStateCached)
            RestoreBounceAttackIfNeeded();

        if (!sparkActive) return;

        bool boostDown = Input.GetKeyDown(boostKey) || Input.GetKeyDown(boostPadKey);
        bool boostHeld = Input.GetKey(boostKey) || Input.GetKey(boostPadKey);
        bool boostUp = Input.GetKeyUp(boostKey) || Input.GetKeyUp(boostPadKey);

        if (holdToAimPausesTimer && boostDown && !dashActive)
            isHoldingAim = true;

        if (boostUp)
            isHoldingAim = false;

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

        if (sparkTimer <= 0f)
        {
            dashBuffered = false;
            isHoldingAim = false;
            holdPauseSpent = 0f;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            EndSparkWindow();
            RestoreMovementLock();
            HardRestoreRigidbodyBaseline();
            return;
        }

        UpdatePreviewWithBounces();
        UpdateRing();

        if (dashActive) return;

        if (ignoreInputOnPickupFrame && Time.frameCount == pickupFrame)
            return;

        bool tryDashNow = (holdToAimPausesTimer && dashOnReleaseWhenHoldToAim) ? boostUp : boostDown;

        if (tryDashNow)
        {
            if (IsInputTemporarilyBlocked()) dashBuffered = true;
            else { TriggerDash(); return; }
        }

        if (dashBuffered && !IsInputTemporarilyBlocked())
        {
            dashBuffered = false;
            TriggerDash();
        }
    }

    private void LateUpdate()
    {
        if (IsResidualAbsorbActive())
        {
            ApplyResidualAbsorb();
            return;
        }

        if (postRestoreGuardActive)
        {
            ApplyResidualAbsorb();

            if (postRestoreGuardUntil > 0f && Time.time >= postRestoreGuardUntil)
            {
                postRestoreGuardActive = false;
                postRestoreGuardUntil = -1f;
            }
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
        if (IsResidualAbsorbActive())
            ApplyResidualAbsorb();

        if (dashActive)
        {
            phys.RequestDash();
            DashStep(Time.fixedDeltaTime);

            // if (debug && debugFrozenDashTrail)
            //     DebugAddFrozenTrailPoint();

            return;
        }

        if (sparkActive && anchorPlayerDuringSpark && anchorValid)
        {
            if (physicalBounceOnPickup && Time.time < noAnchorUntil)
                return;

            if (forceOwnRigidbodyState) EnsureRigidbodyAnchoredState();

            move.movementLocked = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            rb.position = sparkAnchorPos;
        }
    }

    // =========================
    // Public spark API
    // =========================
    public void ActivateSpark(float duration) => ActivateSpark(duration, rb.position);

    public void ActivateSpark(float duration, Vector2 anchorWorldPos)
    {
        HardRestoreRigidbodyBaseline();

        bool bounceWasOnNow = (bounceAtkHadComponent && bounceAtk != null && bounceAtk.enabled);

        DisableBounceAttackHard();
        aimLocked = false;

        forceUnlockOnSparkExit = bounceWasOnNow;
        if (bounceWasOnNow)
            StartResidualAbsorbIfNeeded();

        float dur = (duration > 0f) ? duration : defaultWindowDuration;

        if (!cachedMoveLockValid)
        {
            cachedMoveLockValue = move.movementLocked;
            cachedMoveLockValid = true;
        }

        sparkActive = true;
        dashUsedThisSpark = false;

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
            HardRestoreRigidbodyBaseline();
            move.movementLocked = false;
        }
    }

    public void NotifyPickupBounce()
    {
        if (!physicalBounceOnPickup) return;

        HardRestoreRigidbodyBaseline();
        DisableBounceAttackHard();
        move.movementLocked = false;

        var v = rb.linearVelocity;
        v.y = Mathf.Max(v.y, bounceUpSpeed);
        rb.linearVelocity = v;

        noAnchorUntil = Time.time + Mathf.Max(0.01f, bounceNoAnchorTime);
        StartResidualAbsorbIfNeeded();
    }

    // =========================
    // Dash
    // =========================
    private void TriggerDash()
    {
        if (dashUsedThisSpark) return;
        dashUsedThisSpark = true;

        HardRestoreRigidbodyBaseline();
        DisableBounceAttackHard();
        anchorValid = false;

        float progress = 1f - (sparkTimer / sparkTimerTotal);
        float mult = ComputeMultiplier(progress);
        Color phaseColor = ComputePhaseColor(progress);

        dashDir = GetAimDirection8();
        if (dashDir.sqrMagnitude < 0.0001f) dashDir = Vector2.up;
        dashDir.Normalize();

        aimLocked = true;
        aimLockedDir = dashDir;

        dashSpeedFinal = dashSpeed * mult;
        dashRemainingDist = dashSpeedFinal * dashDuration * Mathf.Clamp(dashDistanceScale, 0.5f, 1f);

        dashBouncesUsed = 0;

        // if (debug && debugFreezePreview && debugFreezeOnDashStart)
        //     DebugFreezeCurrentPreview(phaseColor);

        // if (debug && debugFrozenDashTrail)
        //     DebugStartFrozenTrail(phaseColor);

        EndSparkWindow();

        dashActive = true;

        EnableDashTrail(true);

        dashTimer = Mathf.Max(0.01f, dashDuration);

        if (!dashBackedConstraints)
        {
            dashPrevConstraints = rb.constraints;
            dashBackedConstraints = true;
        }

        if (!dashBackedState)
        {
            dashPrevGravity = rb.gravityScale;
            dashPrevInterp = rb.interpolation;
            dashBackedState = true;
        }

        if (!dashBackedBodyType)
        {
            dashPrevBodyType = rb.bodyType;
            dashBackedBodyType = true;
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.None;

        move.movementLocked = true;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (debug && debugFrozenDashTrail)
            DebugAddFrozenTrailPoint(force: true);
    }

    private bool PickupUsable(FlameSparkPickup p)
    {
        if (p == null) return false;

        var pCol = p.GetComponent<Collider2D>();
        if (pCol != null && !pCol.enabled) return false;

        var pSr = p.GetComponent<SpriteRenderer>();
        if (pSr != null && !pSr.enabled) return false;

        return true;
    }

    // (REEMPLAZADA) CON ESCALA REAL
    private bool TryFindPickupAlong(Vector2 originPos, Vector2 dir, float dist, out FlameSparkPickup bestPickup, out float bestDist)
    {
        bestPickup = null;
        bestDist = float.MaxValue;

        if (!previewMarkNextPickup && !dashActive)
            return false;

        var cfPick = new ContactFilter2D();
        cfPick.useLayerMask = false;
        cfPick.useTriggers = true;

        int count = 0;

        if (boxCol != null)
        {
            Vector2 center = originPos + ScaleOffset(boxCol.offset);
            Vector2 sizeW = ScaleSize(boxCol.size);
            count = Physics2D.BoxCast(center, sizeW, 0f, dir, cfPick, castHits, dist);
        }
        else if (capsuleCol != null)
        {
            Vector2 center = originPos + ScaleOffset(capsuleCol.offset);
            Vector2 sizeW = ScaleSize(capsuleCol.size);
            count = Physics2D.CapsuleCast(center, sizeW, capsuleCol.direction, 0f, dir, cfPick, castHits, dist);
        }
        else if (circleCol != null)
        {
            Vector2 center = originPos + ScaleOffset(circleCol.offset);
            float rW = ScaleRadius(circleCol.radius);
            count = Physics2D.CircleCast(center, rW, dir, cfPick, castHits, dist);
        }
        else
        {
            Bounds b = playerCol.bounds;
            Vector2 center = originPos + (Vector2)(b.center - (Vector3)rb.position);
            Vector2 size = b.size;
            count = Physics2D.BoxCast(center, size, 0f, dir, cfPick, castHits, dist);
        }

        if (count <= 0) return false;

        for (int i = 0; i < count; i++)
        {
            var h = castHits[i];
            if (h.collider == null) continue;

            var p = h.collider.GetComponent<FlameSparkPickup>() ?? h.collider.GetComponentInParent<FlameSparkPickup>();
            if (p == null) continue;
            if (!PickupUsable(p)) continue;

            float d = Mathf.Max(0f, h.distance);
            if (d < 0.0015f) continue;

            if (d < bestDist)
            {
                bestDist = d;
                bestPickup = p;
            }
        }

        return bestPickup != null;
    }

    // ==========================================================
    // DASHSTEP CORREGIDO: consume distancia REAL recorrida, NO "step ideal"
    // ==========================================================
    private void DashStep(float dt)
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        Vector2 startPos = rb.position;   // <- CLAVE
        Vector2 pos = startPos;
        Vector2 dir = dashDir;

        float step = Mathf.Min(dashRemainingDist, dashSpeedFinal * dt);
        float remaining = step;

        int safety = 0;
        const int SAFETY_MAX = 32;

        while (remaining > 0f && safety++ < SAFETY_MAX)
        {
            FlameSparkPickup pickup = null;
            float pickupDist = float.MaxValue;

            float pickupCastDist = remaining + dashSkin + pickupSweepExtra;
            TryFindPickupAlong(pos, dir, pickupCastDist, out pickup, out pickupDist);

            if (pickup != null && pickupDist <= remaining + dashSkin)
            {
                if (PickupUsable(pickup))
                {
                    Vector2 anchor = pickup.GetAnchorWorld();

                    AbortDashToPickup();

                    Vector2 targetPos = snapToPickupAnchorOnDash
                        ? anchor
                        : (pos + dir * Mathf.Max(0f, pickupDist - dashSkin));

                    rb.position = targetPos;

                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;

                    pickup.Consume();

                    NotifyPickupBounce();
                    ActivateSpark(pickup.windowDuration, anchor);
                    return;
                }
            }

            if (!CastWallsFrom(pos, dir, remaining + dashSkin, out RaycastHit2D hit))
            {
                pos += dir * remaining;
                remaining = 0f;
                break;
            }

            float travel = Mathf.Max(0f, hit.distance - dashSkin);

            if (travel <= 0.0001f)
            {
                if (!dashBouncesOnWalls || dashBouncesUsed >= dashMaxBounces)
                {
                    remaining = 0f;
                    break;
                }

                dir = Vector2.Reflect(dir, hit.normal).normalized;
                dashBouncesUsed++;

                float microPush = Mathf.Max(0.0025f, dashSkin);
                float pushed = Mathf.Min(microPush, remaining);
                pos += dir * pushed;
                remaining -= pushed;

                continue;
            }

            float movedToHit = Mathf.Min(travel, remaining);
            pos += dir * movedToHit;
            remaining -= movedToHit;

            if (remaining <= 0f)
                break;

            if (!dashBouncesOnWalls || dashBouncesUsed >= dashMaxBounces)
            {
                remaining = 0f;
                break;
            }

            dir = Vector2.Reflect(dir, hit.normal).normalized;
            dashBouncesUsed++;

            float nudge = Mathf.Max(0.0025f, dashSkin);
            float nudged = Mathf.Min(nudge, remaining);
            pos += dir * nudged;
            remaining -= nudged;
        }

        rb.position = pos;
        dashDir = dir;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // ---- CLAVE: consumir distancia REAL movida ----
        float movedReal = Vector2.Distance(startPos, pos);
        dashRemainingDist -= movedReal;

        // Si no te has movido nada, estás encajado: corta ya
        if (movedReal <= 0.00001f)
        {
            EndDash();
            return;
        }

        if (dashRemainingDist <= 0.0001f)
        {
            EndDash();
            return;
        }
    }

    private void AbortDashToPickup()
    {
        EnableDashTrail(false);

        // if (debug && debugFrozenDashTrail)
        //     DebugEndFrozenTrail();

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

        move.movementLocked = false;
        cachedMoveLockValid = false;
        cachedMoveLockValue = false;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        HardRestoreRigidbodyBaseline();
        DisableBounceAttackHard();
    }

    private void EndDash()
    {
        EnableDashTrail(false);

        // if (debug && debugFrozenDashTrail)
        //     DebugEndFrozenTrail();

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

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        RestoreMovementLock();
        aimLocked = false;

        HardRestoreRigidbodyBaseline();

        RestoreBounceAttackIfNeeded();

        forceUnlockOnSparkExit = false;
    }

    private void EndSparkWindow()
    {
        sparkActive = false;
        sparkTimer = 0f;
        sparkTimerTotal = 0f;
        anchorValid = false;

        SetPreviewVisible(false);
        SetRingVisible(false);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        HardRestoreRigidbodyBaseline();

        if (forceUnlockOnSparkExit)
        {
            StartResidualAbsorbIfNeeded();
            ScheduleBounceRestoreDeferred();
        }
        else
        {
            RestoreBounceAttackIfNeeded();
        }
    }

    private void RestoreMovementLock()
    {
        if (forceUnlockOnSparkExit)
        {
            move.movementLocked = false;
            cachedMoveLockValid = false;
            forceUnlockOnSparkExit = false;
            aimLocked = false;
            return;
        }

        if (cachedMoveLockValid)
        {
            move.movementLocked = cachedMoveLockValue;
            cachedMoveLockValid = false;
        }
        else
        {
            move.movementLocked = false;
        }

        aimLocked = false;
    }

    private void ForceEndAll()
    {
        EnableDashTrail(false);

        if (debug && debugFrozenDashTrail)
            DebugEndFrozenTrail();

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
        SetPickupCircleVisible(false);

        cachedMoveLockValid = false;
        cachedMoveLockValue = false;
        move.movementLocked = false;

        aimLocked = false;

        forceUnlockOnSparkExit = false;

        residualAbsorbFramesLeft = 0;
        residualAbsorbUntilTime = -1f;

        pendingRestoreBounce = false;
        pendingRestoreBounceUntil = -1f;

        postRestoreGuardActive = false;
        postRestoreGuardUntil = -1f;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        HardRestoreRigidbodyBaseline();

        if (bounceAtkHadComponent && bounceAtk != null && bounceAtkStateCached)
        {
            SendBounceHardResets();
            bounceAtk.enabled = bounceAtkWasEnabled;
            bounceAtkStateCached = false;
        }
    }

    // =========================
    // Aim
    // =========================
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

    // =========================
    // Timing helpers
    // =========================
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
    // PREVIEW (live) - CONFIG
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
        sparkPreviewLine.startWidth = previewWidth;
        sparkPreviewLine.endWidth = previewWidth;

        Shader sh =
            Shader.Find("Universal RenderPipeline/2D/Unlit") ??
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

        SetPickupCircleVisible(on && previewMarkNextPickup);
        if (!on) SetPickupCircleVisible(false);
    }

    // ==========================================================
    // PREVIEW SOBRE EL BORDE DEL COLLIDER (NO CENTRO) - CON ESCALA REAL
    // ==========================================================
    private Vector2 GetColliderSupportOffset(Vector2 dir)
    {
        Vector2 d = dir;
        if (d.sqrMagnitude < 0.000001f) d = Vector2.up;
        d.Normalize();

        if (boxCol != null)
        {
            Vector2 sizeW = ScaleSize(boxCol.size);
            Vector2 offW = ScaleOffset(boxCol.offset);

            Vector2 e = sizeW * 0.5f;
            return offW + new Vector2(Mathf.Sign(d.x) * e.x, Mathf.Sign(d.y) * e.y);
        }

        if (circleCol != null)
        {
            Vector2 offW = ScaleOffset(circleCol.offset);
            float rW = ScaleRadius(circleCol.radius);
            return offW + d * rW;
        }

        if (capsuleCol != null)
        {
            Vector2 sizeW = ScaleSize(capsuleCol.size);
            Vector2 offW = ScaleOffset(capsuleCol.offset);

            if (capsuleCol.direction == CapsuleDirection2D.Vertical)
            {
                float r = sizeW.x * 0.5f;
                float coreY = Mathf.Max(0f, (sizeW.y * 0.5f) - r);
                Vector2 coreExt = new Vector2(r, coreY);

                Vector2 supportCore = new Vector2(Mathf.Sign(d.x) * coreExt.x, Mathf.Sign(d.y) * coreExt.y);
                return offW + supportCore + d * r;
            }
            else
            {
                float r = sizeW.y * 0.5f;
                float coreX = Mathf.Max(0f, (sizeW.x * 0.5f) - r);
                Vector2 coreExt = new Vector2(coreX, r);

                Vector2 supportCore = new Vector2(Mathf.Sign(d.x) * coreExt.x, Mathf.Sign(d.y) * coreExt.y);
                return offW + supportCore + d * r;
            }
        }

        Bounds b = playerCol.bounds;
        Vector2 ext = b.extents;
        return new Vector2(Mathf.Sign(d.x) * ext.x, Mathf.Sign(d.y) * ext.y);
    }

    // =========================
    // PREVIEW (live)
    // =========================
    private void UpdatePreviewWithBounces()
    {
        if (sparkPreviewLine == null || !sparkActive || sparkTimerTotal <= 0f) return;

        float progress = 1f - (sparkTimer / sparkTimerTotal);
        float mult = ComputeMultiplier(progress);
        Color c = ComputePhaseColor(progress);

        Vector2 dir0 = GetAimDirection8();
        if (dir0.sqrMagnitude < 0.0001f) dir0 = Vector2.up;
        dir0.Normalize();

        float scale = Mathf.Clamp(dashDistanceScale, 0.5f, 1f);

        float dashSpeedFinalPreview = dashSpeed * mult;
        float totalDist = dashSpeedFinalPreview * Mathf.Max(0.01f, dashDuration) * scale;

        float safeMul = Mathf.Clamp(previewSafetyMultiplier, 0.10f, 1.00f);
        totalDist *= safeMul;

        SetPickupCircleVisible(false);

        Vector2 simPos = rb.position;
        Vector2 simDir = dir0;

        float distLeft = totalDist;

        int maxBounces = dashBouncesOnWalls ? dashMaxBounces : 0;
        int simBounces = 0;

        var centerPts = new List<Vector2>(previewSegments + 16) { simPos };
        var dirPts = new List<Vector2>(previewSegments + 16) { simDir };

        float dt = Mathf.Max(0.0005f, Time.fixedDeltaTime);

        int safetyTicks = 0;
        const int SAFETY_TICKS_MAX = 512;

        while (distLeft > 0.00001f && safetyTicks++ < SAFETY_TICKS_MAX)
        {
            float step = Mathf.Min(distLeft, dashSpeedFinalPreview * dt);
            float remaining = step;

            int safety = 0;
            const int SAFETY_MAX = 32;

            while (remaining > 0f && safety++ < SAFETY_MAX)
            {
                if (previewMarkNextPickup)
                {
                    FlameSparkPickup pickup = null;
                    float pickupDist = float.MaxValue;

                    float castDist = remaining + dashSkin + pickupSweepExtra;
                    TryFindPickupAlong(simPos, simDir, castDist, out pickup, out pickupDist);

                    if (pickup != null && pickupDist <= remaining + dashSkin)
                    {
                        float travelToPickup = Mathf.Max(0f, pickupDist - dashSkin);
                        float moved = Mathf.Min(travelToPickup, remaining);

                        simPos += simDir * moved;
                        centerPts.Add(simPos);
                        dirPts.Add(simDir);

                        Vector2 anchor = pickup.GetAnchorWorld();
                        SetPickupCircleVisible(true);
                        DrawPickupCircle(anchor, pickupCircleRadius, c);

                        distLeft = 0f;
                        remaining = 0f;
                        break;
                    }
                }

                if (!CastWallsFrom(simPos, simDir, remaining + dashSkin, out RaycastHit2D hit))
                {
                    simPos += simDir * remaining;
                    remaining = 0f;

                    centerPts.Add(simPos);
                    dirPts.Add(simDir);
                    break;
                }

                float travel = Mathf.Max(0f, hit.distance - dashSkin);

                if (travel <= 0.0001f)
                {
                    if (!dashBouncesOnWalls || simBounces >= maxBounces)
                    {
                        remaining = 0f;
                        distLeft = 0f;
                        centerPts.Add(simPos);
                        dirPts.Add(simDir);
                        break;
                    }

                    simDir = Vector2.Reflect(simDir, hit.normal).normalized;
                    simBounces++;

                    float microPush = Mathf.Max(0.0025f, dashSkin);
                    float pushed = Mathf.Min(microPush, remaining);
                    simPos += simDir * pushed;
                    remaining -= pushed;

                    centerPts.Add(simPos);
                    dirPts.Add(simDir);
                    continue;
                }

                float movedToHit = Mathf.Min(travel, remaining);
                simPos += simDir * movedToHit;
                remaining -= movedToHit;

                centerPts.Add(simPos);
                dirPts.Add(simDir);

                if (remaining <= 0f) break;

                if (!dashBouncesOnWalls || simBounces >= maxBounces)
                {
                    remaining = 0f;
                    distLeft = 0f;
                    break;
                }

                simDir = Vector2.Reflect(simDir, hit.normal).normalized;
                simBounces++;

                float nudge = Mathf.Max(0.0025f, dashSkin);
                float nudged = Mathf.Min(nudge, remaining);
                simPos += simDir * nudged;
                remaining -= nudged;

                centerPts.Add(simPos);
                dirPts.Add(simDir);
            }

            distLeft -= step;
        }

        if (centerPts.Count < 2)
        {
            centerPts.Add(centerPts[0]);
            dirPts.Add(dir0);
        }

        int nPts = centerPts.Count;
        var renderPts = new Vector3[nPts];

        for (int i = 0; i < nPts; i++)
            renderPts[i] = centerPts[i];

        renderPts[0] = (Vector3)(centerPts[0] + GetColliderSupportOffset(dir0));

        Vector2 finalDir = dirPts[dirPts.Count - 1];
        if (finalDir.sqrMagnitude < 0.000001f) finalDir = dir0;
        renderPts[nPts - 1] = (Vector3)(centerPts[nPts - 1] + GetColliderSupportOffset(finalDir));

        int maxPts = Mathf.Max(4, previewSegments + 2);
        if (renderPts.Length > maxPts)
        {
            var dec = new Vector3[maxPts];
            dec[0] = renderPts[0];
            dec[maxPts - 1] = renderPts[renderPts.Length - 1];

            float stepIdx = (renderPts.Length - 1) / (float)(maxPts - 1);
            for (int i = 1; i < maxPts - 1; i++)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt(i * stepIdx), 1, renderPts.Length - 2);
                dec[i] = renderPts[idx];
            }
            renderPts = dec;
        }

        sparkPreviewLine.positionCount = renderPts.Length;
        sparkPreviewLine.SetPositions(renderPts);

        sparkPreviewLine.startColor = c;
        sparkPreviewLine.endColor = c;
        if (sparkPreviewLine.material != null) sparkPreviewLine.material.color = c;
    }

    // =========================
    // RING
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
        sparkRingLine.startWidth = ringWidth;
        sparkRingLine.endWidth = ringWidth;

        Shader sh =
            Shader.Find("Universal RenderPipeline/2D/Unlit") ??
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
    // Wall cast FROM SIMULATED POSITION (CON ESCALA REAL)
    // =========================
    private bool CastWallsFrom(Vector2 originPos, Vector2 dir, float dist, out RaycastHit2D bestHit)
    {
        bestHit = default;

        var cfWall = new ContactFilter2D();
        cfWall.useLayerMask = true;
        cfWall.layerMask = dashCollisionMask;
        cfWall.useTriggers = false;

        int count = 0;

        if (boxCol != null)
        {
            Vector2 center = originPos + ScaleOffset(boxCol.offset);
            Vector2 sizeW = ScaleSize(boxCol.size);
            count = Physics2D.BoxCast(center, sizeW, 0f, dir, cfWall, castHits, dist);
        }
        else if (capsuleCol != null)
        {
            Vector2 center = originPos + ScaleOffset(capsuleCol.offset);
            Vector2 sizeW = ScaleSize(capsuleCol.size);
            CapsuleDirection2D capDir = capsuleCol.direction;
            count = Physics2D.CapsuleCast(center, sizeW, capDir, 0f, dir, cfWall, castHits, dist);
        }
        else if (circleCol != null)
        {
            Vector2 center = originPos + ScaleOffset(circleCol.offset);
            float rW = ScaleRadius(circleCol.radius);
            count = Physics2D.CircleCast(center, rW, dir, cfWall, castHits, dist);
        }
        else
        {
            Bounds b = playerCol.bounds;
            Vector2 center = originPos + (Vector2)(b.center - (Vector3)rb.position);
            Vector2 size = b.size;
            count = Physics2D.BoxCast(center, size, 0f, dir, cfWall, castHits, dist);
        }

        if (count <= 0) return false;

        float best = float.MaxValue;
        bool has = false;

        for (int i = 0; i < count; i++)
        {
            var h = castHits[i];
            if (h.collider == null) continue;
            if (h.collider.isTrigger) continue;
            if (h.collider == playerCol) continue;

            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
                has = true;
            }
        }

        return has;
    }

    // =========================
    // RB state
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

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void HardRestoreRigidbodyBaseline()
    {
        if (rbStateBackedUp)
        {
            rb.gravityScale = prevGravity;
            rb.interpolation = prevInterp;
            rb.constraints = prevConstraints;
            rbStateBackedUp = false;
        }

        rb.bodyType = baseBodyType;
        rb.gravityScale = baseGravity;
        rb.interpolation = baseInterp;
        rb.constraints = baseConstraints;

        rb.angularVelocity = 0f;
    }

    // =========================
    // DEBUG - Dash Trail (ephemeral TrailRenderer)
    // =========================
    private void SetupDashTrail()
    {
        if (!debugDashTrail) return;

        dashTrail = GetComponent<TrailRenderer>();
        if (dashTrail == null) dashTrail = gameObject.AddComponent<TrailRenderer>();

        dashTrail.enabled = false;
        dashTrail.emitting = false;

        dashTrail.time = Mathf.Max(0.02f, dashTrailTime);
        dashTrail.startWidth = Mathf.Max(0.001f, dashTrailStartWidth);
        dashTrail.endWidth = Mathf.Max(0.001f, dashTrailEndWidth);

        Shader sh =
            Shader.Find("Universal RenderPipeline/2D/Unlit") ??
            Shader.Find("Universal Render Pipeline/2D/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        dashTrail.material = new Material(sh);

        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(dashTrailColor, 0f), new GradientColorKey(dashTrailColor, 1f), },
            new GradientAlphaKey[] { new GradientAlphaKey(dashTrailColor.a, 0f), new GradientAlphaKey(0f, 1f), }
        );
        dashTrail.colorGradient = grad;

        dashTrail.sortingLayerName = dashTrailSortingLayer;
        dashTrail.sortingOrder = dashTrailSortingOrder;

        dashTrail.minVertexDistance = 0.02f;
        dashTrail.Clear();
    }

    private void EnableDashTrail(bool on)
    {
        if (!debugDashTrail || dashTrail == null) return;

        if (on)
        {
            dashTrail.time = Mathf.Max(0.02f, dashTrailTime);
            dashTrail.Clear();
            dashTrail.enabled = true;
            dashTrail.emitting = true;
        }
        else
        {
            dashTrail.emitting = false;
            dashTrail.enabled = false;
            dashTrail.Clear();
        }
    }

    // =========================
    // Pickup Circle
    // =========================
    private void ConfigurePickupCircleAuto()
    {
        if (pickupPreviewCircle == null)
        {
            var go = new GameObject("PickupPreviewCircle");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            pickupPreviewCircle = go.AddComponent<LineRenderer>();
        }

        pickupPreviewCircle.useWorldSpace = true;
        pickupPreviewCircle.startWidth = pickupCircleWidth;
        pickupPreviewCircle.endWidth = pickupCircleWidth;

        Shader sh =
            Shader.Find("Universal RenderPipeline/2D/Unlit") ??
            Shader.Find("Universal Render Pipeline/2D/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        pickupPreviewCircle.material = new Material(sh);
        pickupPreviewCircle.sortingLayerName = pickupCircleSortingLayer;
        pickupPreviewCircle.sortingOrder = pickupCircleSortingOrder;

        pickupPreviewCircle.numCapVertices = 4;
        pickupPreviewCircle.numCornerVertices = 2;

        pickupPreviewCircle.positionCount = 0;
        pickupPreviewCircle.loop = true;
        pickupPreviewCircle.enabled = false;
    }

    private void SetPickupCircleVisible(bool on)
    {
        if (pickupPreviewCircle == null) return;
        pickupPreviewCircle.enabled = on;
        if (!on) pickupPreviewCircle.positionCount = 0;
    }

    private void DrawPickupCircle(Vector2 center, float radius, Color c)
    {
        if (pickupPreviewCircle == null) return;

        int segs = Mathf.Max(8, pickupCircleSegments);
        var pts = new Vector3[segs];

        for (int i = 0; i < segs; i++)
        {
            float a = (i / (float)segs) * Mathf.PI * 2f;
            pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        pickupPreviewCircle.positionCount = pts.Length;
        pickupPreviewCircle.SetPositions(pts);

        pickupPreviewCircle.startColor = c;
        pickupPreviewCircle.endColor = c;
        if (pickupPreviewCircle.material != null) pickupPreviewCircle.material.color = c;
    }

    // =========================
    // DEBUG - Frozen Preview (permanent)
    // =========================
    private void EnsureFrozenLine()
    {
        if (frozenPreviewLine != null) return;

        var go = new GameObject("FrozenSparkPreviewLine");
        go.transform.SetParent(null);

        frozenPreviewLine = go.AddComponent<LineRenderer>();
        frozenPreviewLine.useWorldSpace = true;
        frozenPreviewLine.startWidth = frozenLineWidth;
        frozenPreviewLine.endWidth = frozenLineWidth;

        Shader sh =
            Shader.Find("Universal RenderPipeline/2D/Unlit") ??
            Shader.Find("Universal Render Pipeline/2D/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        frozenPreviewLine.material = new Material(sh);
        frozenPreviewLine.sortingLayerName = previewSortingLayer;
        frozenPreviewLine.sortingOrder = frozenLineSortingOrder;

        frozenPreviewLine.numCapVertices = 4;
        frozenPreviewLine.numCornerVertices = 2;
        frozenPreviewLine.positionCount = 0;
        frozenPreviewLine.enabled = false;
    }

    private void DebugFreezeCurrentPreview(Color c)
    {
        if (sparkPreviewLine == null) return;
        if (sparkPreviewLine.positionCount < 2) return;

        EnsureFrozenLine();

        int n = sparkPreviewLine.positionCount;
        var pts = new Vector3[n];
        sparkPreviewLine.GetPositions(pts);

        frozenPreviewLine.positionCount = n;
        frozenPreviewLine.SetPositions(pts);

        frozenPreviewLine.startColor = c;
        frozenPreviewLine.endColor = c;
        if (frozenPreviewLine.material != null) frozenPreviewLine.material.color = c;

        frozenPreviewLine.enabled = true;
        frozenHasData = true;
    }

    private void DebugClearFrozenPreview()
    {
        if (frozenPreviewLine == null) return;
        frozenPreviewLine.positionCount = 0;
        frozenPreviewLine.enabled = false;
        frozenHasData = false;
    }

    // =========================
    // DEBUG - Frozen Real Trail (permanent)
    // =========================
    private void EnsureFrozenTrailLine()
    {
        if (frozenTrailLine != null) return;

        var go = new GameObject("FrozenDashTrailLine");
        go.transform.SetParent(null);

        frozenTrailLine = go.AddComponent<LineRenderer>();
        frozenTrailLine.useWorldSpace = true;
        frozenTrailLine.startWidth = frozenTrailWidth;
        frozenTrailLine.endWidth = frozenTrailWidth;

        Shader sh =
            Shader.Find("Universal RenderPipeline/2D/Unlit") ??
            Shader.Find("Universal Render Pipeline/2D/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        frozenTrailLine.material = new Material(sh);
        frozenTrailLine.sortingLayerName = dashTrailSortingLayer;
        frozenTrailLine.sortingOrder = frozenTrailSortingOrder;

        frozenTrailLine.numCapVertices = 4;
        frozenTrailLine.numCornerVertices = 2;
        frozenTrailLine.positionCount = 0;
        frozenTrailLine.enabled = true;
    }

    private void DebugStartFrozenTrail(Color c)
    {
        EnsureFrozenTrailLine();

        frozenTrailPts.Clear();
        Vector2 d0 = dashDir.sqrMagnitude > 0.0001f ? dashDir : Vector2.up;
        Vector2 edge = (Vector2)rb.position + GetColliderSupportOffset(d0);

        Vector3 p = edge;

        frozenTrailPts.Add(p);
        lastFrozenTrailPt = p;

        frozenTrailLine.startColor = c;
        frozenTrailLine.endColor = c;
        if (frozenTrailLine.material != null) frozenTrailLine.material.color = c;

        frozenTrailLine.positionCount = 1;
        frozenTrailLine.SetPosition(0, p);

        frozenTrailRecording = true;
    }

    private void DebugAddFrozenTrailPoint(bool force = false)
    {
        if (!frozenTrailRecording) return;

        Vector2 d = dashDir.sqrMagnitude > 0.0001f ? dashDir : Vector2.up;
        Vector2 edge = (Vector2)rb.position + GetColliderSupportOffset(d);
        Vector3 p = edge;

        float minStepSqr = frozenTrailMinStep * frozenTrailMinStep;

        if (!force && (p - lastFrozenTrailPt).sqrMagnitude < minStepSqr)
            return;

        lastFrozenTrailPt = p;

        if (frozenTrailPts.Count >= frozenTrailMaxPoints)
            return;

        frozenTrailPts.Add(p);
        frozenTrailLine.positionCount = frozenTrailPts.Count;
        frozenTrailLine.SetPositions(frozenTrailPts.ToArray());
    }

    private void DebugEndFrozenTrail()
    {
        frozenTrailRecording = false;
    }

    private void DebugClearFrozenTrail()
    {
        frozenTrailPts.Clear();
        frozenTrailRecording = false;

        if (frozenTrailLine != null)
        {
            frozenTrailLine.positionCount = 0;
            frozenTrailLine.enabled = true;
        }
    }
}
