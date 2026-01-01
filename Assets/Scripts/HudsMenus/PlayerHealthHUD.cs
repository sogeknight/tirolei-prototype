using UnityEngine;
using TMPro;

public class PlayerHealthHUD : MonoBehaviour
{
    [Header("Referencias")]
    public PlayerHealth playerHealth;
    public TMP_Text uiText;

    private void Awake()
    {
        // Asegurar referencia al texto
        if (uiText == null)
            uiText = GetComponent<TMP_Text>();

        // Si no has arrastrado nada en el inspector, busca el Player en la escena
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth == null)
            Debug.LogError("[PlayerHealthHUD] No se ha encontrado PlayerHealth en escena.");
        if (uiText == null)
            Debug.LogError("[PlayerHealthHUD] No se ha encontrado TMP_Text en este objeto.");
    }

    private void Start()
    {
        ActualizarHUD();
    }

    private void Update()
    {
        ActualizarHUD();
    }

    private void ActualizarHUD()
    {
        if (playerHealth == null || uiText == null)
            return;

        uiText.text = $"HP: {playerHealth.currentHealth}/{playerHealth.maxHealth}";
    }
}
