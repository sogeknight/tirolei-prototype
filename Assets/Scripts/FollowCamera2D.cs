using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FollowCamera2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody2D targetRb;

    [Header("Dead Zone Horizontal (world units)")]
    public float deadZoneLeft = 2f;
    public float deadZoneRight = 2f;

    [Header("Dead Zone Vertical (world units)")]
    public float deadZoneUp = 1.0f;
    public float deadZoneDown = 0.7f;

    [Header("Smooth Horizontal")]
    public float smoothTimeX = 0.15f;

    [Header("Smooth Vertical (solo cuando va lento)")]
    public float smoothTimeY = 0.15f;

    [Header("Umbral de velocidad vertical para seguir instantáneo")]
    public float verticalSpeedThreshold = 5f;

    [Header("Z Offset")]
    public float offsetZ = -10f;

    private float velX;
    private float velY;

    private void Reset()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p)
            {
                target = p.transform;
                targetRb = p.GetComponent<Rigidbody2D>();
            }
        }

        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    private void LateUpdate()
    {
        if (!target) return;

        // Posición actual de la cámara
        float camX = transform.position.x;
        float camY = transform.position.y;

        // Posición del jugador
        float px = target.position.x;
        float py = target.position.y;

        float newCamX = camX;
        float newCamY = camY;

        // ---------------------------
        // DEAD ZONE HORIZONTAL
        // ---------------------------
        float leftLimit = camX - deadZoneLeft;
        float rightLimit = camX + deadZoneRight;

        if (px < leftLimit)
            newCamX = px + deadZoneLeft;
        else if (px > rightLimit)
            newCamX = px - deadZoneRight;

        // ---------------------------
        // DEAD ZONE VERTICAL
        // ---------------------------
        float downLimit = camY - deadZoneDown;
        float upLimit = camY + deadZoneUp;

        bool outsideVerticalDeadZone = false;

        if (py < downLimit)
        {
            newCamY = py + deadZoneDown;
            outsideVerticalDeadZone = true;
        }
        else if (py > upLimit)
        {
            newCamY = py - deadZoneUp;
            outsideVerticalDeadZone = true;
        }

        // ---------------------------
        // HORIZONTAL: siempre suave
        // ---------------------------
        float smoothedX = Mathf.SmoothDamp(camX, newCamX, ref velX, smoothTimeX);

        // ---------------------------
        // VERTICAL: modo híbrido
        // ---------------------------
        float smoothedY;

        float absVy = 0f;
        if (targetRb != null)
        {
            absVy = Mathf.Abs(targetRb.linearVelocity.y);
        }

        bool goingFast = absVy > verticalSpeedThreshold;

        if (outsideVerticalDeadZone || goingFast)
        {
            // SIN suavizado: la cámara engancha directamente
            smoothedY = newCamY;
        }
        else
        {
            // Movimiento suave cuando está cerca y va lento
            smoothedY = Mathf.SmoothDamp(camY, newCamY, ref velY, smoothTimeY);
        }

        transform.position = new Vector3(smoothedX, smoothedY, offsetZ);
    }
}
