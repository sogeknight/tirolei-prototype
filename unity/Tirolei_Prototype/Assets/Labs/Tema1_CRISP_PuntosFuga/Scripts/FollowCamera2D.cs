using UnityEngine;

public class FollowCamera2D : MonoBehaviour
{
    [Header("Target a seguir")]
    public Transform target;

    [Header("Offset")]
    public float offsetX = 0f;
    public float offsetY = 0f;
    public float offsetZ = -10f;

    private float fixedY;

    private void Start()
    {
        // Guardamos la altura inicial de la cámara para no marear
        fixedY = transform.position.y + offsetY;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Solo seguimos en X, Y se mantiene fija
        float targetX = target.position.x + offsetX;

        transform.position = new Vector3(
            targetX,
            fixedY,
            offsetZ
        );
    }
}
