using UnityEngine;
using TMPro;

public class PlayerHealthHUD : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public TMP_Text uiText;

    private void Awake()
    {
        if (uiText == null)
            uiText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (playerHealth == null || uiText == null) return;

        uiText.text = $"HP: {playerHealth.currentHealth}/{playerHealth.maxHealth}";
    }
}



