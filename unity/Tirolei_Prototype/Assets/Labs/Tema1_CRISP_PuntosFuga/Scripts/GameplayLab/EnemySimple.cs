using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemySimple : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Daño al jugador")]
    public int contactDamage = 1;
    public float knockbackForce = 12f;

    [Header("Saltos hacia el jugador")]
    public Transform target;            // Player
    public float moveSpeed = 3f;        // velocidad horizontal del salto
    public float jumpForce = 8f;        // fuerza vertical del salto
    public float hopInterval = 0.6f;    // tiempo entre saltos
    public float maxChaseDistance = 20f;

    [Header("Suelo (simple)")]
    public LayerMask groundLayers;      // normalmente Ground
    public float groundRayDistance = 0.2f;

    private Rigidbody2D rb;
    private Vector3 baseScale;
    private float hopTimer = 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        baseScale = transform.localScale; // respeta tu tamaño
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        hopTimer += Time.fixedDeltaTime;

        float dx = target.position.x - transform.position.x;
        float absDx = Mathf.Abs(dx);

        // Si está lejísimos, no hace nada
        if (absDx > maxChaseDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        bool grounded = IsGroundedSimple();

        // --- EN EL AIRE: no matar vel.x, solo dejar que la gravedad actúe ---
        if (!grounded)
        {
            // solo volteamos visual según la vel actual
            FlipVisual(rb.linearVelocity.x);
            return;
        }

        // --- EN EL SUELO PERO AÚN NO TOCA SALTAR ---
        if (hopTimer < hopInterval)
        {
            // en suelo, quieto horizontalmente
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            FlipVisual(rb.linearVelocity.x);
            return;
        }

        // --- NUEVO SALTO HACIA EL PLAYER ---
        float dirX = Mathf.Sign(dx); // -1 izquierda, +1 derecha

        Vector2 newVel = new Vector2(dirX * moveSpeed, jumpForce);
        rb.linearVelocity = newVel;

        FlipVisual(newVel.x);

        hopTimer = 0f;
    }

    private bool IsGroundedSimple()
    {
        // Raycast cortito hacia abajo desde el centro del enemigo
        Vector2 origin = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundRayDistance, groundLayers);
        return hit.collider != null;
    }

    private void FlipVisual(float velX)
    {
        if (velX > 0.01f)
        {
            transform.localScale = new Vector3(
                Mathf.Abs(baseScale.x),
                baseScale.y,
                baseScale.z
            );
        }
        else if (velX < -0.01f)
        {
            transform.localScale = new Vector3(
                -Mathf.Abs(baseScale.x),
                baseScale.y,
                baseScale.z
            );
        }
    }

    // --- Daño y muerte ---

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

        var playerHP = collision.collider.GetComponent<PlayerHealth>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(contactDamage);
        }

        var playerRb = collision.collider.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Vector2 dir = (collision.collider.transform.position - transform.position).normalized;
            dir.y = 0.5f;
            playerRb.linearVelocity = dir * knockbackForce;
        }
    }
}
