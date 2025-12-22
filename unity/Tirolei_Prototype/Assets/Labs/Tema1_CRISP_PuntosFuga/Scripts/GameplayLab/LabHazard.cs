using UnityEngine;
using System.Collections.Generic;

public class LabHazard : MonoBehaviour
{
    [Header("Daño")]
    public int damage = 1;

    [Tooltip("Cada cuántos segundos puede volver a dañar al mismo player si sigue dentro.")]
    public float tickInterval = 0.35f;

    [Header("Filtros")]
    public string playerTag = "Player";

    private readonly Dictionary<int, float> nextAllowedTimeByInstance = new Dictionary<int, float>();

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health == null) return;

        int id = other.gameObject.GetInstanceID();
        float now = Time.time;

        if (!nextAllowedTimeByInstance.TryGetValue(id, out float nextTime))
            nextTime = 0f;

        if (now < nextTime) return;

        // Intentar daño (PlayerHealth bloqueará si está invulnerable por bounce/iframes)
        bool applied = health.TryTakeDamage(damage);

        // Solo avanzamos el tick si ha intentado "tickear"
        // (si prefieres que el hazard "espere" aunque no aplique daño, deja esto igual)
        nextAllowedTimeByInstance[id] = now + tickInterval;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        int id = other.gameObject.GetInstanceID();
        if (nextAllowedTimeByInstance.ContainsKey(id))
            nextAllowedTimeByInstance.Remove(id);
    }
}
