using UnityEngine;

public enum MaterialTier
{
    MaterialTier_I_Fragile,
    MaterialTier_II_Weak,
    MaterialTier_III_Structural,
    MaterialTier_IV_Core,
    MaterialTier_S_Seal
}

public class WorldMaterial : MonoBehaviour, IBounceImpactReceiver, IPiercingBounceReceiver
{
    [Header("Tier")]
    public MaterialTier tier = MaterialTier.MaterialTier_I_Fragile;

    [Header("Estructura (HP)")]
    [Tooltip("Vida estructural del objeto. Cuando llega a 0, se rompe.")]
    public float structuralHP = 20f;

    [Tooltip("Si está activo, nunca se rompe.")]
    public bool indestructible = false;

    [Header("Debug")]
    public bool debugLogs = false;

    private float hp;
    private bool isBroken = false;
    private Collider2D cachedCol;

    private void Awake()
    {
        hp = structuralHP;
        cachedCol = GetComponent<Collider2D>();
    }

    // ================= IMPACTO NORMAL =================

    public void ReceiveBounceImpact(BounceImpactData impact)
    {
        if (tier == MaterialTier.MaterialTier_S_Seal) return;
        if (indestructible) return;

        hp -= impact.damage;

        if (debugLogs)
            Debug.Log($"[WorldMaterial] {name} -{impact.damage} => hp={hp}/{structuralHP}");

        if (hp <= 0f)
        {
            BreakImmediate(impact);
        }
    }

    // ================= PIERCING =================

    public bool ApplyPiercingBounce(BounceImpactData impact, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = 0f;

        if (tier == MaterialTier.MaterialTier_S_Seal) return false;
        if (indestructible) return false;

        if (isBroken || hp <= 0f)
        {
            remainingDamage = incomingDamage;
            return true;
        }

        float used = Mathf.Min(incomingDamage, hp);
        hp -= used;
        remainingDamage = incomingDamage - used;

        if (debugLogs)
            Debug.Log($"[WorldMaterial] {name} IN={incomingDamage:0.0} USED={used:0.0} REM={remainingDamage:0.0} hp={hp:0.0}/{structuralHP:0.0}");


        if (hp <= 0f)
        {
            BreakImmediate(impact);
            return true;
        }

        return false;
    }

    // ================= ROTURA =================

    private void BreakImmediate(BounceImpactData impact)
    {
        if (isBroken) return;
        isBroken = true;

        Vector3 pos = GetImpactWorldPoint(impact);
        SpawnDebris(pos);

        if (cachedCol != null)
            cachedCol.enabled = false;

        Destroy(gameObject);
    }

    private void BreakImmediate()
    {
        if (isBroken) return;
        isBroken = true;

        SpawnDebris(transform.position);

        if (cachedCol != null)
            cachedCol.enabled = false;

        Destroy(gameObject);
    }

    // ================= VFX (ÚNICO PUNTO) =================

    private void SpawnDebris(Vector3 worldPos)
    {
        if (DebrisSpawner.Instance == null)
        {
            Debug.LogError("[WorldMaterial] DebrisSpawner.Instance NULL");
            return;
        }

        DebrisSpawner.Instance.SpawnCustom(
            worldPos,
            24,             // count
            Color.white,    // startColor
            0.35f,          // startSize
            2.5f,           // startLifetime
            2f,             // speedMin
            6f,             // speedMax
            1f,             // spreadX
            1f              // spreadY
        );
    }

    // ================= UTILIDADES =================

    public void ResetHP()
    {
        hp = structuralHP;
        isBroken = false;
        if (cachedCol != null) cachedCol.enabled = true;
    }

    private Vector3 GetImpactWorldPoint(BounceImpactData impact)
    {
        if (cachedCol == null) return transform.position;

        Vector2 src = (impact.source != null)
            ? (Vector2)impact.source.transform.position
            : (Vector2)transform.position;

        Vector2 p = cachedCol.ClosestPoint(src);

        Vector2 inward = (impact.direction.sqrMagnitude > 0.0001f)
            ? -impact.direction.normalized * 0.05f
            : Vector2.zero;

        Vector2 world2 = p + inward;
        return new Vector3(world2.x, world2.y, 0f);
    }

    // === Estado público (solo lectura) ===
    public bool IsBroken => isBroken || hp <= 0f;
    public float CurrentHP => hp;



}
