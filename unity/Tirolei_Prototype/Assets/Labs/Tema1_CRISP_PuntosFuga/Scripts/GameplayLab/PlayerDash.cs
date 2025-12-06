using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDash : MonoBehaviour
{
    [Header("Input")]
    public KeyCode dashKey = KeyCode.C;

    [Header("Dash")]
    public float dashSpeed = 12f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.4f;

    [Header("Daño")]
    public int dashDamage = 1;
    public LayerMask enemyLayer;   // asigna Enemy en el inspector

    private Rigidbody2D rb;

    private bool isDashing = false;
    private float dashTimer = 0f;
    private float cooldownTimer = 0f;
    private int dashDir = 1;
    private float originalGravity;

    // enemigos golpeados durante ESTE dash (para no multi-golpear)
    private readonly List<EnemySimple> hitEnemiesThisDash = new List<EnemySimple>();

    public bool IsDashing => isDashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravity = rb.gravityScale;
    }

    private void Update()
    {
        // cooldown
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        // inicio del dash
        if (!isDashing && cooldownTimer <= 0f && Input.GetKeyDown(dashKey))
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            if (inputX != 0)
                dashDir = inputX > 0 ? 1 : -1;

            StartDash();
        }

        // mantenimiento del dash
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                EndDash();
            }
        }
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;

        // limpiar hits de este dash
        hitEnemiesThisDash.Clear();

        originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // velocidad fija inicial
        rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);

        // ignorar colisiones Player <-> Enemy mientras dura el dash
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (playerLayer >= 0 && enemyLayerIndex >= 0)
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, true);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = originalGravity;

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (playerLayer >= 0 && enemyLayerIndex >= 0)
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, false);
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            // mientras dura el dash, fuerzo la velocidad horizontal
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            DoDashHitCheck();
        }
    }

    private void DoDashHitCheck()
    {
        Vector2 center = rb.position;
        Vector2 halfExtents = new Vector2(0.8f, 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, halfExtents, 0f, enemyLayer);
        foreach (var h in hits)
        {
            EnemySimple enemy = h.GetComponent<EnemySimple>();
            if (enemy == null) continue;

            // si ya lo hemos golpeado en ESTE dash, lo saltamos
            if (hitEnemiesThisDash.Contains(enemy))
                continue;

            // primer impacto en este dash → aplicar daño y marcarlo
            enemy.TakeHit(dashDamage);
            hitEnemiesThisDash.Add(enemy);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        Vector2 center = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 size = new Vector2(0.8f, 0.6f);
        Gizmos.DrawWireCube(center, size);
    }
}
