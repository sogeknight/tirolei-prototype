using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStaticFX : MonoBehaviour
{
    [Header("Refs (en StaticFX)")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("States (exactos en Static.controller)")]
    [SerializeField] private string emptyState = "00_Empty";
    [SerializeField] private string entryState = "00_Static";
    [SerializeField] private string loopState  = "01_Static";

    [Header("Condición (por INPUT, no por velocidad)")]
    [SerializeField] private float inputDeadzone = 0.02f;

    [Header("Timing")]
    [SerializeField] private float idleDelay = 1.0f;

    private float timer;
    private bool inEntry;
    private bool inLoop;
    private bool hidden = true;

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        if (!animator || !spriteRenderer)
        {
            Debug.LogError("[PlayerStaticFX] Falta Animator o SpriteRenderer en StaticFX.", this);
            enabled = false;
            return;
        }

        // Estado determinista al arrancar
        HardHide(resetTimer: true, forceEmpty: true);
    }

    /// <summary> Llamar 1 vez por frame desde PlayerMovementController. </summary>
    public void Tick(bool isGrounded, float inputX)
    {
        bool wantsMove = Mathf.Abs(inputX) > inputDeadzone;
        bool shouldIdle = isGrounded && !wantsMove;

        if (!shouldIdle)
        {
            HardHide(resetTimer: true, forceEmpty: false);
            return;
        }

        timer += Time.deltaTime;

        if (timer < idleDelay)
        {
            SoftHide();
            return;
        }

        // ENTRY una vez
        if (!inEntry && !inLoop)
        {
            Show();
            animator.Play(entryState, 0, 0f);
            inEntry = true;
            return;
        }

        // Cuando termina ENTRY -> LOOP
        if (inEntry)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(entryState) && st.normalizedTime >= 1f)
            {
                inEntry = false;
                inLoop = true;
                animator.Play(loopState, 0, 0f);
            }
        }
        // Si está en LOOP: NO TOCAR NADA.
    }

    private void Show()
    {
        if (!hidden) return;
        hidden = false;
        spriteRenderer.enabled = true;
    }

    private void SoftHide()
    {
        if (hidden) return;
        hidden = true;
        spriteRenderer.enabled = false;
        inEntry = false;
        inLoop = false;
    }

    private void HardHide(bool resetTimer, bool forceEmpty)
    {
        inEntry = false;
        inLoop = false;
        if (resetTimer) timer = 0f;

        if (!hidden)
        {
            hidden = true;
            spriteRenderer.enabled = false;
        }

        if (forceEmpty)
            animator.Play(emptyState, 0, 0f);
    }
}
