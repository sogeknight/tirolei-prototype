using UnityEngine;

public class BounceChargeVFX : MonoBehaviour
{
    public PlayerBounceAttack bounceAttack;

    [Tooltip("Arrastra aquí el GO que tiene SpriteRenderer+Animator. Puede ser este mismo GO.")]
    public GameObject fireVisual;

    public bool showWhileAiming = true;
    public bool hideOnBounceStart = true;

    SpriteRenderer sr;
    Animator anim;

    void Awake()
    {
        if (fireVisual == null) fireVisual = gameObject;

        sr = fireVisual.GetComponent<SpriteRenderer>();
        anim = fireVisual.GetComponent<Animator>();

        SetVisible(false);
    }

    void Update()
    {
        if (bounceAttack == null) return;

        bool shouldShow = false;

        if (showWhileAiming && bounceAttack.IsAiming) shouldShow = true;
        if (hideOnBounceStart && bounceAttack.IsBouncing) shouldShow = false;

        SetVisible(shouldShow);
    }

    void SetVisible(bool v)
    {
        // Si apagas el GO, te cargas el Update. No lo hagas.
        if (sr != null) sr.enabled = v;
        if (anim != null) anim.enabled = v; // opcional: si quieres congelar anim cuando se oculta
    }
}
