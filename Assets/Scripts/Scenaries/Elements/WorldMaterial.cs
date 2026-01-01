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

    [Tooltip("Si está activo, nunca se rompe (equivale a WorldBase_Solid fuera del sistema).")]
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


    // Compatibilidad: el sistema antiguo no tenía feedback.
    public void ReceiveBounceImpact(BounceImpactData impact)
    {
        if (tier == MaterialTier.MaterialTier_S_Seal) return;
        if (indestructible) return;

        hp -= impact.damage;

        if (debugLogs)
            Debug.Log($"[WorldMaterial] {name} tier={tier} -{impact.damage} => hp={hp:0.0}/{structuralHP:0.0}");

        if (hp <= 0f)
            Break();
    }

    // NUEVO: piercing con daño sobrante y feedback de rotura.
    public bool ApplyPiercingBounce(BounceImpactData impact, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = 0f;

        if (tier == MaterialTier.MaterialTier_S_Seal) return false;
        if (indestructible) return false;

        // Si ya está roto (o hp <= 0), no debe volver a "parar" el golpe ni generar rebotes raros.
        if (isBroken || hp <= 0f)
        {
            remainingDamage = incomingDamage;
            return true;
        }

        // Evita valores negativos/raros
        float hpPos = Mathf.Max(0f, hp);
        float used = Mathf.Min(incomingDamage, hpPos);
        hp -= used;

        remainingDamage = Mathf.Max(0f, incomingDamage - used);

        if (debugLogs)
            Debug.Log($"[WorldMaterial] {name} IN={incomingDamage:0.0} USED={used:0.0} REM={remainingDamage:0.0} hp={hp:0.0}/{structuralHP:0.0}");

        if (hp <= 0f)
        {
            BreakImmediate();
            return true;
        }

        return false;
    }


    private void BreakImmediate()
    {
        if (isBroken) return;
        isBroken = true;

        if (debugLogs)
            Debug.Log($"[WorldMaterial] BREAK => {name} (tier={tier})");

        // CLAVE: desactiva el collider en este frame para que el siguiente cast no re-impacte.
        if (cachedCol != null) cachedCol.enabled = false;

        Destroy(gameObject);
    }

    // Mantengo Break() por compatibilidad con ReceiveBounceImpact()
    private void Break()
    {
        BreakImmediate();
    }

    public void ResetHP()
    {
        hp = structuralHP;
    }
}
