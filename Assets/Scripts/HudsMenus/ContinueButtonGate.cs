using UnityEngine;
using UnityEngine.UI;

public class ContinueButtonGate : MonoBehaviour
{
    public MainMenu menu;
    public Button continueButton;

    private void Start()
    {
        if (menu == null) menu = FindObjectOfType<MainMenu>();
        if (continueButton == null) continueButton = GetComponent<Button>();

        bool has = menu != null && menu.HasSave();
        continueButton.interactable = has;
    }
}
