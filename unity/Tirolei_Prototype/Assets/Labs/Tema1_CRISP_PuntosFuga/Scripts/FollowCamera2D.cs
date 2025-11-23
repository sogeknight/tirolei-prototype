using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FollowCamera2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Dead Zone (world units)")]
    [Tooltip("How far the player can move left from camera center before camera moves.")]
    public float deadZoneLeft = 2f;

    [Tooltip("How far the player can move right from camera center before camera moves.")]
    public float deadZoneRight = 2f;

    [Header("Vertical / Depth")]
    [Tooltip("If 0, the current camera Y will be used on Start().")]
    public float fixedY = 0f;

    public float offsetZ = -10f;

    [Header("Camera Smoothing")]
    [Tooltip("Time for the camera to reach the target X position.")]
    public float smoothTimeX = 0.2f;

    private float currentVelocityX;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("FollowCamera2D: target is not assigned.");
            enabled = false;
            return;
        }

        // Fijamos Y si no está definida
        if (Mathf.Approximately(fixedY, 0f))
        {
            fixedY = transform.position.y;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float camX = transform.position.x;
        float playerX = target.position.x;

        float newCamX = camX;

        // Si el jugador se va demasiado a la derecha del centro de la cámara
        if (playerX > camX + deadZoneRight)
        {
            newCamX = playerX - deadZoneRight;
        }
        // Si el jugador se va demasiado a la izquierda del centro de la cámara
        else if (playerX < camX - deadZoneLeft)
        {
            newCamX = playerX + deadZoneLeft;
        }
        // Si está dentro de la dead zone, newCamX = camX (no se mueve)

        // Suavizamos el movimiento de X para evitar "PAM"
        float smoothedX = Mathf.SmoothDamp(
            camX,
            newCamX,
            ref currentVelocityX,
            smoothTimeX
        );

        transform.position = new Vector3(smoothedX, fixedY, offsetZ);
    }
}
