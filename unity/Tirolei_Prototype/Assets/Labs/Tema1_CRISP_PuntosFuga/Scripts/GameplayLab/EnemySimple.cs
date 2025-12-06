using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemySimple : MonoBehaviour
{
    [Header("Vida (si usas dash para matarlo)")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Daño al jugador")]
    public int contactDamage = 1;
    public float knockbackForce = 12f;

    private Rigidbody2D rb;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;

        // Se cae, se apoya en el suelo, pero NO se mueve en X ni rota
        rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                        RigidbodyConstraints2D.FreezeRotation;
    }


    public void TakeHit(int dmg)
    {
        currentHealth -= dmg;
        Debug.Log($"[EnemySimple] Recibe {dmg} → vida = {currentHealth}");

        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player")) return;

        // Daño al jugador usando tu sistema actual basado en PlayerHealth
        var playerHP = collision.collider.GetComponent<PlayerHealth>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(contactDamage);
        }

        // Knockback SOLO al player
        var playerRb = collision.collider.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Vector2 dir = (collision.collider.transform.position - transform.position).normalized;
            dir.y = 0.5f;
            playerRb.linearVelocity = dir * knockbackForce;
        }
    }
}
