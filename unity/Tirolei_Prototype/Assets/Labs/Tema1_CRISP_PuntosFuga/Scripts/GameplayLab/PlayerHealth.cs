using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 5;
    public int currentHealth = 5;

    public float invincibleTime = 0.5f;
    private float invTimer = 0f;

    private void Update()
    {
        if (invTimer > 0f)
            invTimer -= Time.deltaTime;
    }

    public void TakeDamage(int dmg)
    {
        if (invTimer > 0f) return;

        currentHealth -= dmg;
        invTimer = invincibleTime;

        Debug.Log("[PlayerHealth] Daño recibido. Vida = " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = maxHealth;   // RESETEA VIDA
            // NO LLAMES RESPAWN AQUÍ
            // PlayerRespawn YA lo hace cuando pisas hazard o trigger
        }
    }
}
