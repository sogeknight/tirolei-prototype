using UnityEngine;

public interface IPiercingBounceReceiver
{
    /// <summary>
    /// Aplica daño tipo "piercing".
    /// - Devuelve true si ESTE impacto rompió el objetivo (o eliminó tiles/celdas).
    /// - remainingDamage = daño sobrante tras consumir lo necesario para romper o restar HP.
    /// </summary>
    bool ApplyPiercingBounce(BounceImpactData impact, float incomingDamage, out float remainingDamage);
}
