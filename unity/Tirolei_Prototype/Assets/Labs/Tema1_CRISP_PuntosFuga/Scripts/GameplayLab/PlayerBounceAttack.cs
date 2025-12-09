using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(Collider2D))]
public class PlayerBounceAttack : MonoBehaviour
{
    [Header("Ataque")]
    public KeyCode attackKey = KeyCode.X;
    public string attackButton = "Fire1";
    public float maxDistance = 10f;

    [Header("Invencibilidad")]
    public bool invincibleDuringBounce = true;
    [HideInInspector] public bool isInvincible = false;

    [Header("Colisión de rebote")]
    public LayerMask bounceLayers;
    public float skin = 0.02f;

    [Header("Preview trayectoria")]
    public LineRenderer previewLine;
    public int previewSegments = 30;
    public int previewMaxBounces = 5;
    public Color previewColor = Color.cyan;

    [Header("Suavizado fin de trayecto")]
    [Range(0f, 1f)]
    public float slowDownFraction = 0.3f;   // % final donde frena
    [Range(0.05f, 1f)]
    public float minStepFactor = 0.2f;      // factor mínimo de paso

    private Rigidbody2D rb;
    private PlayerMovementController movement;
    private CircleCollider2D circle;

    private bool isAiming   = false;
    private bool isBouncing = false;

    private Vector2 aimDirection   = Vector2.right;
    private Vector2 bounceDir      = Vector2.right;
    private Vector2 lastPreviewDir = Vector2.right;

    private float remainingDistance = 0f;
    private float fixedStepSize;
    private float ballRadius;

    private float originalGravityScale;
    private RigidbodyType2D originalBodyType;
    private bool originalPhysicsStored = false;

    // >>> NUEVO: posición donde empiezas a apuntar (para clavar al player)
    private Vector2 aimStartPosition;

    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovementController>();
        circle   = GetComponent<CircleCollider2D>();

        if (rb == null)       Debug.LogError("[PlayerBounceAttack] Falta Rigidbody2D.");
        if (movement == null) Debug.LogError("[PlayerBounceAttack] Falta PlayerMovementController.");
        if (circle == null)   Debug.LogError("[PlayerBounceAttack] Falta CircleCollider2D.");

        ballRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        ConfigurePreview();
    }

    private void Update()
    {
        // ATAQUE: teclado X + mando X (JoystickButton2)
        bool attackDown = Input.GetKeyDown(attackKey) || Input.GetKeyDown(KeyCode.JoystickButton2);
        bool attackUp   = Input.GetKeyUp(attackKey)   || Input.GetKeyUp(KeyCode.JoystickButton2);

        // SALTO: el que ya tengas en PlayerMovementController (probablemente Jump o JoystickButton0)
        bool jumpDown = Input.GetKeyDown(movement.jumpKey) || Input.GetKeyDown(KeyCode.JoystickButton0);

        if (!isAiming && !isBouncing && attackDown)
        {
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
        // >>> NUEVO: mientras estás apuntando, clava al player en la posición inicial
        if (isAiming)
        {
            rb.MovePosition(aimStartPosition);
            return;
        }

        if (!isBouncing) return;

        // -------- Suavizado de fin de trayecto --------
        float baseStep = fixedStepSize;
        float moveDist = baseStep;

        if (slowDownFraction > 0f)
        {
            float slowZone = maxDistance * slowDownFraction;

            if (remainingDistance < slowZone)
            {
                float t      = remainingDistance / Mathf.Max(0.0001f, slowZone);
                float factor = Mathf.Lerp(minStepFactor, 1f, t);
                moveDist     = baseStep * factor;
            }
        }

        if (moveDist > remainingDistance)
            moveDist = remainingDistance;

        if (moveDist <= 0f)
        {
            EndBounce();
            return;
        }

        Vector2 origin = rb.position;
        Vector2 dir    = bounceDir.sqrMagnitude > 0.001f
                        ? bounceDir.normalized
                        : aimDirection.normalized;

        Vector2 targetPos = origin;

        RaycastHit2D hit = Physics2D.CircleCast(
            origin,
            ballRadius,
            dir,
            moveDist + skin,
            bounceLayers
        );

        if (hit.collider != null)
        {
            float travel = Mathf.Max(0f, hit.distance - skin);

            if (travel > 0f)
            {
                targetPos = origin + dir * travel;
                rb.MovePosition(targetPos);
                remainingDistance -= travel;
            }
            else
            {
                targetPos = origin;
            }

            bounceDir = Vector2.Reflect(dir, hit.normal).normalized;

            if (remainingDistance <= 0f)
            {
                EndBounce();
            }

            return;
        }

        targetPos = origin + dir * moveDist;
        rb.MovePosition(targetPos);
        remainingDistance -= moveDist;

        if (remainingDistance <= 0f)
        {
            EndBounce();
        }
    }

    // ================== ESTADOS ==================

    private void StartAiming()
    {
        isAiming = true;

        aimDirection   = Vector2.right;
        lastPreviewDir = aimDirection;

        movement.movementLocked = true;

        if (!originalPhysicsStored)
        {
            originalGravityScale  = rb.gravityScale;
            originalBodyType      = rb.bodyType;
            originalPhysicsStored = true;
        }

        // Guardamos posición al entrar en modo apuntado
        aimStartPosition = rb.position;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale   = 0f;
        rb.bodyType       = RigidbodyType2D.Kinematic;

        fixedStepSize = maxDistance / Mathf.Max(1, previewSegments);

        UpdatePreview();
    }

    // Dirección de apuntado (la que ya te funcionaba hacia abajo)
    private void HandleAiming()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(x, y);

        if (input.sqrMagnitude < 0.01f)
            input = lastPreviewDir;

        input.Normalize();

        aimDirection   = input;
        lastPreviewDir = input;

        UpdatePreview();
    }

    private void StartBounce()
    {
        isAiming   = false;
        isBouncing = true;
        remainingDistance = maxDistance;

        ClearPreview();

        rb.linearVelocity = Vector2.zero;

        bounceDir = lastPreviewDir.normalized;

        if (invincibleDuringBounce)
            isInvincible = true;
    }

    private void EndBounce()
    {
        isBouncing = false;

        ClearPreview();

        if (originalPhysicsStored)
        {
            rb.bodyType     = originalBodyType;
            rb.gravityScale = originalGravityScale;
        }

        rb.linearVelocity = Vector2.zero;
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
        Vector2 dir    = lastPreviewDir.sqrMagnitude > 0.001f
                        ? lastPreviewDir.normalized
                        : Vector2.right;

        float remaining   = maxDistance;
        int   bounceCount = 0;

        List<Vector3> points = new List<Vector3>();
        points.Add(origin);

        int maxPoints = Mathf.Max(2, previewSegments + 1);

        while (remaining > 0f &&
               points.Count < maxPoints &&
               bounceCount <= previewMaxBounces)
        {
            float stepDist = fixedStepSize;
            if (stepDist <= 0f) break;

            RaycastHit2D hit = Physics2D.CircleCast(
                origin,
                ballRadius,
                dir,
                stepDist + skin,
                bounceLayers
            );

            if (hit.collider != null)
            {
                float travel = Mathf.Max(0f, hit.distance - skin);
                Vector2 hitPos = origin + dir * travel;

                points.Add(hitPos);
                remaining -= travel;
                origin     = hitPos;

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
                origin     = nextPos;
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
        previewLine.endWidth   = 0.08f;

        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = previewColor;
        previewLine.material = mat;

        previewLine.startColor = Color.white;
        previewLine.endColor   = Color.white;
        previewLine.sortingLayerName = "Default";
        previewLine.sortingOrder     = 100;

        previewLine.positionCount = 0;
        previewLine.enabled = false;
    }
}
