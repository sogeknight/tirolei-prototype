using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BouncingBall : MonoBehaviour
{
    public float speed = 15f;

    private Rigidbody2D rb;
    private Vector2 dir;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // dirección inicial no simétrica
        dir = new Vector2(0.8f, 0.6f).normalized;
    }

    private void Start()
    {
        rb.linearVelocity = dir * speed;
    }

    private void FixedUpdate()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.001f)
        {
            rb.linearVelocity = dir * speed;
        }
        else
        {
            dir = rb.linearVelocity.normalized;
            rb.linearVelocity = dir * speed;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // velocidad de entrada
        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.001f)
            v = dir * speed;

        Vector2 vNorm = v.normalized;

        // elegir la mejor normal
        var contacts = collision.contacts;
        if (contacts.Length == 0) return;

        Vector2 bestN = contacts[0].normal;
        float bestDot = Vector2.Dot(-vNorm, bestN);

        for (int i = 1; i < contacts.Length; i++)
        {
            Vector2 n = contacts[i].normal;
            float d = Vector2.Dot(-vNorm, n);
            if (d > bestDot)
            {
                bestDot = d;
                bestN = n;
            }
        }

        bestN.Normalize();

        // reflejo especular
        Vector2 newDir = Vector2.Reflect(vNorm, bestN);

        // --- clamp de ángulo + jitter ---
        float angle = Mathf.Atan2(newDir.y, newDir.x) * Mathf.Rad2Deg;

        float minFromHorizontal = 15f;
        float absAngle = Mathf.Abs(angle);
        float sign = angle >= 0f ? 1f : -1f;

        if (absAngle < minFromHorizontal)
            angle = sign * minFromHorizontal;
        else if (absAngle > 180f - minFromHorizontal)
            angle = sign * (180f - minFromHorizontal);

        float jitter = 5f; // ruido para romper bucles perfectos
        angle += Random.Range(-jitter, jitter);

        float rad = angle * Mathf.Deg2Rad;
        newDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

        dir = newDir;

        // empujón fuera de la pared
        transform.position += (Vector3)(bestN * 0.03f);

        rb.linearVelocity = dir * speed;
    }
}
