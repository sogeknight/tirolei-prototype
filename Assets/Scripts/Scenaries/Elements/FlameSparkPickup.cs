using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlameSparkPickup : MonoBehaviour
{
    [Header("Spark Pickup")]
    public float windowDuration = 1.0f;
    public Transform anchorPoint;
    public bool destroyOnPickup = true;
    public float respawnSeconds = 0f;

    [Header("Spark Dash Combat (per-pickup)")]
    [Tooltip("Si true, el dash del Spark aplica daño y 'piercea' igual que BounceAttack.")]
    public bool sparkDashUsesBounceCombat = false;

    [Tooltip("Daño que aplica el dash cuando sparkDashUsesBounceCombat = true.")]
    public int sparkDashCombatDamage = 20;


    [Header("Blocked by Ground (auto)")]
    [Tooltip("Si está activado, el pickup se deshabilita mientras detecte Ground encima/solapando.")]
    public bool disableWhileBlockedByGround = true;

    [Tooltip("LayerMask del Ground/Wall que bloquea el pickup (TilemapCollider2D normalmente).")]
    public LayerMask groundMask;

    [Tooltip("Radio de chequeo solapando el pickup (si está dentro de un collider de ground lo detecta).")]
    [Range(0.01f, 1.0f)] public float groundCheckRadius = 0.18f;

    [Tooltip("Raycast hacia arriba adicional (por si el overlap no detecta bien).")]
    public bool useUpRaycast = true;

    [Tooltip("Distancia del raycast hacia arriba.")]
    [Range(0.01f, 2.0f)] public float upRayDistance = 0.35f;

    [Tooltip("Offset vertical del raycast (para empezar desde el centro visual).")]
    [Range(-1.0f, 1.0f)] public float upRayStartOffsetY = 0.0f;

    [Header("Blocked Visual (dashed outline)")]
    public bool showDashedOutlineWhenBlocked = true;

    [Tooltip("Color del contorno dashed cuando está bloqueado.")]
    public Color blockedOutlineColor = new Color(1f, 1f, 1f, 0.95f);

    [Tooltip("Radio del círculo dashed (outline).")]
    [Range(0.05f, 2.0f)] public float outlineRadius = 0.30f;

    [Tooltip("Grosor del dashed.")]
    [Range(0.01f, 0.25f)] public float outlineWidth = 0.06f;

    [Tooltip("Segmentos del círculo (más = más redondo).")]
    [Range(8, 128)] public int outlineSegments = 40;

    [Tooltip("Orden de sorting del outline (para que se vea encima).")]
    public int outlineSortingOrder = 20050;

    [Tooltip("Sorting layer del outline.")]
    public string outlineSortingLayer = "Default";

    [Tooltip("Cuántas repeticiones del patrón dash alrededor del círculo.")]
    [Range(1, 64)] public int dashTiling = 12;

    [Tooltip("Porcentaje de “dash visible” dentro del patrón (0.5 = mitad visible, mitad hueco).")]
    [Range(0.05f, 0.95f)] public float dashFill = 0.55f;

    [Tooltip("Frecuencia de chequeo (segundos). 0 = cada frame.")]
    [Range(0f, 0.5f)] public float blockedCheckInterval = 0.05f;

    [Header("Anti-Flicker (Hysteresis)")]
    [Tooltip("Tiempo mínimo que debe permanecer 'sin ground' para pasar a usable.")]
    public float minClearTime = 0.12f;

    [Tooltip("Tiempo mínimo que debe permanecer 'con ground' para pasar a bloqueado.")]
    public float minBlockedTime = 0.05f;

    private float clearTimer = 0f;
    private float blockedTimer = 0f;


    private SpriteRenderer sr;
    private Collider2D col;

    private bool available = true;

    // Trigger-wait-exit (solo para consumo por caminar)
    private bool waitingForExit = false;
    private Collider2D holderCol;
    private Coroutine respawnCo;

    // Blocked state
    private bool blockedByGround;
    private float nextBlockedCheckTime;

    // Dashed outline
    private LineRenderer dashed;
    private Material dashedMat;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        SetupDashedOutline();

        // Estado inicial
        EvaluateBlockedState(forceApply: true);
    }

    private void Update()
    {
        if (!disableWhileBlockedByGround) return;

        if (blockedCheckInterval <= 0f)
        {
            EvaluateBlockedState(forceApply: false);
        }
        else
        {
            if (Time.time >= nextBlockedCheckTime)
            {
                nextBlockedCheckTime = Time.time + blockedCheckInterval;
                EvaluateBlockedState(forceApply: false);
            }
        }
    }

    public Vector2 GetAnchorWorld()
    {
        return anchorPoint != null
            ? (Vector2)anchorPoint.position
            : (Vector2)transform.position;
    }

    // =====================================================
    // BLOQUEO POR GROUND (HYSTERESIS REAL, 2 sentidos)
    // =====================================================
    private void EvaluateBlockedState(bool forceApply)
    {
        bool sensedBlocked = IsBlockedByGroundNow();

        if (forceApply)
        {
            blockedByGround = sensedBlocked;
            clearTimer = 0f;
            blockedTimer = 0f;
            ApplyBlockedVisualAndUsability(blockedByGround);
            return;
        }

        float dt = Time.deltaTime;

        if (sensedBlocked)
        {
            // Estamos detectando ground: acumulamos blockedTimer
            blockedTimer += dt;
            clearTimer = 0f;

            // Solo pasamos a "blocked" si lleva bloqueado el tiempo mínimo
            if (!blockedByGround && blockedTimer >= Mathf.Max(0f, minBlockedTime))
            {
                blockedByGround = true;
                blockedTimer = 0f;
                ApplyBlockedVisualAndUsability(true);
            }
        }
        else
        {
            // No detecta ground: acumulamos clearTimer
            clearTimer += dt;
            blockedTimer = 0f;

            // Solo pasamos a "clear/usable" si lleva clear el tiempo mínimo
            if (blockedByGround && clearTimer >= Mathf.Max(0f, minClearTime))
            {
                blockedByGround = false;
                clearTimer = 0f;
                ApplyBlockedVisualAndUsability(false);
            }
        }
    }



    private bool IsBlockedByGroundNow()
    {
        if (!available) return false;

        // Si no hay máscara bien puesta, no vas a detectar nada:
        if (groundMask.value == 0) return false;

        Bounds b;
        if (col != null) b = col.bounds;
        else if (sr != null) b = sr.bounds;
        else b = new Bounds(transform.position, Vector3.one * 0.25f);

        // 1) OverlapCircle en el centro del pickup: detecta solape directo (pickup “dentro” del ground).
        Vector2 center = b.center;
        if (Physics2D.OverlapCircle(center, groundCheckRadius, groundMask) != null)
            return true;

        // 2) OverlapBox justo encima del pickup: detecta “ground encima” aunque no lo solape.
        float w = Mathf.Max(0.05f, b.size.x * 0.9f);
        float h = Mathf.Max(0.05f, b.size.y * 0.35f);
        Vector2 boxCenter = new Vector2(b.center.x, b.max.y + (h * 0.5f));

        if (Physics2D.OverlapBox(boxCenter, new Vector2(w, h), 0f, groundMask) != null)
            return true;

        // 3) Raycast hacia arriba (opcional) como redundancia.
        if (useUpRaycast)
        {
            Vector2 rayStart = new Vector2(b.center.x, b.center.y + upRayStartOffsetY);
            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.up, upRayDistance, groundMask);
            if (hit.collider != null) return true;
        }

        return false;
    }

    private void ApplyBlockedVisualAndUsability(bool blocked)
    {
        // Visual: dashed ON cuando bloqueado
        if (showDashedOutlineWhenBlocked && dashed != null)
        {
            dashed.enabled = blocked;
            if (blocked)
            {
                dashed.startColor = blockedOutlineColor;
                dashed.endColor = blockedOutlineColor;
                if (dashedMat != null) dashedMat.color = blockedOutlineColor;
            }
        }

        if (!disableWhileBlockedByGround) return;

        // Usabilidad:
        // - Bloqueado: SIN relleno (sprite off) y collider off
        // - Desbloqueado: sprite on y collider on (si está available)
        if (sr != null) sr.enabled = !blocked && available;
        if (col != null) col.enabled = !blocked && available;

        if (blocked)
        {
            waitingForExit = false;
            holderCol = null;
        }
    }

    private void SetupDashedOutline()
    {
        if (!showDashedOutlineWhenBlocked) return;

        GameObject go = new GameObject("BlockedOutline_Dashed");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        dashed = go.AddComponent<LineRenderer>();
        dashed.useWorldSpace = false;
        dashed.loop = true;
        dashed.positionCount = Mathf.Max(8, outlineSegments);

        dashed.startWidth = outlineWidth;
        dashed.endWidth = outlineWidth;

        // Material: usa shader de sprites con textura (evita Unlit/Color que NO tiene _MainTex)
        Shader sh =
            Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Universal RenderPipeline/2D/Sprite-Unlit-Default");

        if (sh == null)
        {
            // Último recurso: pero si llegas aquí, algo está muy mal en tu proyecto
            sh = Shader.Find("Sprites/Default");
        }

        dashedMat = new Material(sh);
        dashed.material = dashedMat;

        dashed.sortingLayerName = outlineSortingLayer;
        dashed.sortingOrder = outlineSortingOrder;

        dashed.textureMode = LineTextureMode.Tile;
        dashed.alignment = LineAlignment.View;

        // Genera textura 1D: dash / gap
        Texture2D tex = GenerateDashTexture(dashFill);

        // Asignar textura de forma compatible (_MainTex o _BaseMap)
        ApplyLineTexture(tex);

        // Tiling alrededor del círculo
        SetTextureScaleSafe(new Vector2(Mathf.Max(1, dashTiling), 1f));

        // Geometría círculo local
        RebuildCircleLocal();

        dashed.enabled = false;
    }

    private void ApplyLineTexture(Texture2D tex)
    {
        if (dashedMat == null || tex == null) return;

        tex.wrapMode = TextureWrapMode.Repeat;

        if (dashedMat.HasProperty("_MainTex"))
        {
            dashedMat.SetTexture("_MainTex", tex);
        }
        else if (dashedMat.HasProperty("_BaseMap"))
        {
            dashedMat.SetTexture("_BaseMap", tex);
        }
        else
        {
            // fallback: mainTexture (puede fallar si shader no soporta)
            dashedMat.mainTexture = tex;
        }
    }

    private void SetTextureScaleSafe(Vector2 scale)
    {
        if (dashedMat == null) return;

        if (dashedMat.HasProperty("_MainTex"))
            dashedMat.SetTextureScale("_MainTex", scale);
        else if (dashedMat.HasProperty("_BaseMap"))
            dashedMat.SetTextureScale("_BaseMap", scale);
        else
            dashedMat.mainTextureScale = scale;
    }

    private void RebuildCircleLocal()
    {
        if (dashed == null) return;

        int segs = Mathf.Max(8, outlineSegments);
        dashed.positionCount = segs;

        float r = Mathf.Max(0.01f, outlineRadius);

        for (int i = 0; i < segs; i++)
        {
            float t = i / (float)segs;
            float a = t * Mathf.PI * 2f;
            dashed.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
    }

    private Texture2D GenerateDashTexture(float fill01)
    {
        int w = 64;
        int on = Mathf.Clamp(Mathf.RoundToInt(w * Mathf.Clamp01(fill01)), 1, w - 1);

        Texture2D tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < w; x++)
        {
            bool isOn = x < on;
            tex.SetPixel(x, 0, isOn ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f));
        }
        tex.Apply(false, true);
        return tex;
    }

    private void OnValidate()
    {
        if (dashed != null)
        {
            dashed.startWidth = outlineWidth;
            dashed.endWidth = outlineWidth;
            RebuildCircleLocal();

            if (dashedMat != null)
            {
                SetTextureScaleSafe(new Vector2(Mathf.Max(1, dashTiling), 1f));
            }
        }
    }

    // =====================================================
    // TRIGGER NORMAL (caminar) -> espera Exit
    // =====================================================
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!available) return;

        // Bloqueo duro: si AHORA está bloqueado por ground, no se puede coger (da igual hysteresis)
        if (disableWhileBlockedByGround && IsBlockedByGroundNow())
            return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        var spark = rb.GetComponent<PlayerSparkBoost>();
        if (spark == null) return;

        if (spark.IsDashing() || spark.IsSparkActive()) return;

        holderCol = other;

        // Pasa configuración per-instance
        spark.dashUsesBounceCombat = sparkDashUsesBounceCombat;
        spark.dashCombatDamage = sparkDashCombatDamage;

        Vector2 rawAnchor = GetAnchorWorld();
        Vector2 safeAnchor = spark.ResolveSafeAnchor(rawAnchor);

        spark.ActivateSpark(windowDuration, safeAnchor);
        Consume(waitForExit: true);

    }



    private void OnTriggerExit2D(Collider2D other)
    {
        if (!waitingForExit) return;
        if (other != holderCol) return;

        waitingForExit = false;
        holderCol = null;

        StartRespawn();
    }

    // =====================================================
    // DASH/X (rb.Cast) -> TU CÓDIGO LLAMA A ESTO
    // =====================================================
    public void Consume()
    {
        if (!available) return;
        Consume(waitForExit: false);
    }

    // =====================================================
    // NÚCLEO
    // =====================================================
    private void Consume(bool waitForExit)
    {
        if (!destroyOnPickup) return;

        available = false;

        if (sr != null) sr.enabled = false;
        if (dashed != null) dashed.enabled = false;

        if (respawnSeconds <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (waitForExit)
        {
            waitingForExit = true;
            return;
        }

        waitingForExit = false;
        holderCol = null;

        StartRespawn();
    }


    public bool IsUsableNow()
    {
        if (!available) return false;
        if (waitingForExit) return false;

        if (col != null && !col.enabled) return false;

        if (disableWhileBlockedByGround)
        {
            if (blockedByGround) return false;
            if (minClearTime > 0f && clearTimer < minClearTime) return false;

            // Si quieres todavía más duro, también puedes exigir:
            // if (IsBlockedByGroundNow()) return false;
        }

        return true;
    }


    // =====================================================
    // RESPAWN
    // =====================================================
    private void StartRespawn()
    {
        if (respawnCo != null)
            StopCoroutine(respawnCo);

        respawnCo = StartCoroutine(RespawnAfterTime());
    }

    private IEnumerator RespawnAfterTime()
    {
        if (col != null) col.enabled = false;

        yield return new WaitForSeconds(respawnSeconds);

        available = true;

        EvaluateBlockedState(forceApply: true);

        respawnCo = null;
    }

#if UNITY_EDITOR
    // Gizmos opcionales para depurar el “blocked check”
    private void OnDrawGizmosSelected()
    {
        if (groundMask.value == 0) return;

        Bounds b;
        var c = GetComponent<Collider2D>();
        var s = GetComponent<SpriteRenderer>();

        if (c != null) b = c.bounds;
        else if (s != null) b = s.bounds;
        else b = new Bounds(transform.position, Vector3.one * 0.25f);

        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(b.center, groundCheckRadius);

        float w = Mathf.Max(0.05f, b.size.x * 0.9f);
        float h = Mathf.Max(0.05f, b.size.y * 0.35f);
        Vector2 boxCenter = new Vector2(b.center.x, b.max.y + (h * 0.5f));
        Gizmos.DrawWireCube(boxCenter, new Vector3(w, h, 0f));

        if (useUpRaycast)
        {
            Vector2 rayStart = new Vector2(b.center.x, b.center.y + upRayStartOffsetY);
            Gizmos.DrawLine(rayStart, rayStart + Vector2.up * upRayDistance);
        }
    }
#endif
}
