using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(Collider2D))]
public class TilemapWorldMaterial : MonoBehaviour, IBounceImpactReceiver, IPiercingBounceReceiver
{
    [Header("Tier")]
    public MaterialTier tier = MaterialTier.MaterialTier_I_Fragile;

    [Header("Per-hit breaking")]
    [Tooltip("Si está activo, cada impacto rompe 1 subtile (o más con radius).")]
    public bool breakOnEveryImpact = true;

    [Tooltip("Radio en celdas alrededor del impacto (0 = solo 1 celda).")]
    [Range(0, 5)] public int breakRadiusCells = 0;

    [Header("HP (opcional, si no rompes por hit)")]
    public bool useHP = false;
    public float structuralHP = 20f;

    [Header("Flags")]
    public bool indestructible = false;
    public bool debugLogs = false;

    private float hp;
    private Tilemap tilemap;
    private Collider2D col2D;

    private void Awake()
    {
        tilemap = GetComponent<Tilemap>();
        col2D = GetComponent<Collider2D>();
        hp = structuralHP;
    }

    // Compatibilidad
    public void ReceiveBounceImpact(BounceImpactData impact)
    {
        if (tier == MaterialTier.MaterialTier_S_Seal) return;
        if (indestructible) return;

        Vector3Int cell = GetImpactCell(impact);

        if (breakOnEveryImpact && !useHP)
        {
            BreakCells(cell, breakRadiusCells);
            return;
        }

        hp -= impact.damage;

        if (debugLogs)
            Debug.Log($"[TilemapWorldMaterial] {name} -{impact.damage} hp={hp:0.0}/{structuralHP:0.0} cell={cell}");

        if (hp <= 0f)
        {
            BreakCells(cell, breakRadiusCells);
            hp = structuralHP;
        }
    }

    // NUEVO: piercing
    public bool ApplyPiercingBounce(BounceImpactData impact, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = 0f;

        if (tier == MaterialTier.MaterialTier_S_Seal) return false;
        if (indestructible) return false;

        Vector3Int cell = GetImpactCell(impact);

        // Modo "rompe por golpe": asumimos que romper consume 1 "unidad" de daño
        // (si quieres que consuma más según radius o nº de celdas, se ajusta).
        if (breakOnEveryImpact && !useHP)
        {
            bool brokeAny = BreakCells(cell, breakRadiusCells);
            float used = brokeAny ? 1f : 0f;
            remainingDamage = Mathf.Max(0f, incomingDamage - used);

            if (debugLogs)
                Debug.Log($"[TilemapWorldMaterial] PIERCE breakOnHit brokeAny={brokeAny} IN={incomingDamage:0.0} REM={remainingDamage:0.0} cell={cell}");

            return brokeAny;
        }

        // Modo HP
        float usedHp = Mathf.Min(incomingDamage, hp);
        hp -= usedHp;
        remainingDamage = Mathf.Max(0f, incomingDamage - usedHp);

        if (debugLogs)
            Debug.Log($"[TilemapWorldMaterial] PIERCE HP IN={incomingDamage:0.0} USED={usedHp:0.0} REM={remainingDamage:0.0} hp={hp:0.0}/{structuralHP:0.0} cell={cell}");

        if (hp <= 0f)
        {
            bool brokeAny = BreakCells(cell, breakRadiusCells);
            hp = structuralHP;
            return brokeAny;
        }

        return false;
    }

    private Vector3Int GetImpactCell(BounceImpactData impact)
    {
        Vector2 p = col2D.ClosestPoint(impact.source != null ? (Vector2)impact.source.transform.position : (Vector2)transform.position);
        Vector2 inward = (impact.direction.sqrMagnitude > 0.0001f) ? -impact.direction.normalized * 0.02f : Vector2.zero;
        Vector3 world = (Vector3)(p + inward);
        return tilemap.WorldToCell(world);
    }

    private bool BreakCells(Vector3Int center, int radius)
    {
        if (tilemap == null) return false;

        bool brokeAny = false;

        if (radius <= 0)
        {
            if (tilemap.HasTile(center))
            {
                tilemap.SetTile(center, null);
                tilemap.RefreshTile(center);
                brokeAny = true;
                if (debugLogs) Debug.Log($"[TilemapWorldMaterial] Break cell {center}");
            }
            return brokeAny;
        }

        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            Vector3Int c = new Vector3Int(center.x + x, center.y + y, center.z);
            if (!tilemap.HasTile(c)) continue;
            tilemap.SetTile(c, null);
            tilemap.RefreshTile(c);
            brokeAny = true;
        }

        if (debugLogs) Debug.Log($"[TilemapWorldMaterial] Break radius {radius} at {center} brokeAny={brokeAny}");
        return brokeAny;
    }
}
