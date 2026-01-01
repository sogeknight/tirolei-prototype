using UnityEngine;

public class BounceImpulseVFX : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerBounceAttack bounce;
    [SerializeField] private SpriteRenderer[] renderersToToggle;
    [SerializeField] private Animator[] animatorsToToggle;

    [Header("Behaviour")]
    [SerializeField] private bool restartAnimOnShow = true;
    [SerializeField] private bool hideOnStart = true;

    private void Awake()
    {
        if (hideOnStart) SetVisible(false);
    }

    private void OnEnable()
    {
        if (bounce == null) return;
        bounce.OnBounceStart += HandleBounceStart;
        bounce.OnBounceEnd += HandleBounceEnd;
    }

    private void OnDisable()
    {
        if (bounce == null) return;
        bounce.OnBounceStart -= HandleBounceStart;
        bounce.OnBounceEnd -= HandleBounceEnd;
    }

    private void HandleBounceStart()
    {
        SetVisible(true);

        if (restartAnimOnShow && animatorsToToggle != null)
        {
            for (int i = 0; i < animatorsToToggle.Length; i++)
            {
                var a = animatorsToToggle[i];
                if (a == null) continue;

                a.enabled = true;
                a.Rebind();
                a.Update(0f);
                a.Play(0, 0, 0f); // reinicia estado 0 desde 0
            }
        }
    }

    private void HandleBounceEnd()
    {
        SetVisible(false);
    }

    private void SetVisible(bool v)
    {
        if (renderersToToggle != null)
        {
            for (int i = 0; i < renderersToToggle.Length; i++)
                if (renderersToToggle[i] != null) renderersToToggle[i].enabled = v;
        }

        if (animatorsToToggle != null)
        {
            for (int i = 0; i < animatorsToToggle.Length; i++)
                if (animatorsToToggle[i] != null) animatorsToToggle[i].enabled = v;
        }
    }
}
