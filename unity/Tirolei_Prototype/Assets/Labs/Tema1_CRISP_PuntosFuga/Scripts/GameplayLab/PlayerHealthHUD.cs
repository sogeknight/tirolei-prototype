using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthHUD : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public Text uiText;

    private void Awake()
    {
        if (uiText == null)
            uiText = GetComponent<Text>();
    }

    private void Update()
    {
        if (playerHealth == null || uiText == null) return;

        uiText.text = "HP: " + playerHealth.currentHealth + "/" + playerHealth.maxHealth;
    }
}
