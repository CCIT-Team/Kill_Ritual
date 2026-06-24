using UnityEngine;

public class KREnemyVisualFeedback : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private Renderer bodyRenderer;
    // 캡슐 색상을 바꿀 Renderer. 비워두면 자식에서 자동 검색한다.

    [Header("Debug Colors")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color aggroColor = new Color(1f, 0.8f, 0.25f);
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private Color hitColor = Color.cyan;
    [SerializeField] private Color deadColor = Color.gray;
    [SerializeField] private float hitFlashDuration = 0.12f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animIsAggro = "IsAggro";
    [SerializeField] private string animIsMoving = "IsMoving";
    [SerializeField] private string animAttackTrigger = "Attack";
    [SerializeField] private string animHitTrigger = "Hit";
    [SerializeField] private string animDieTrigger = "Die";

    private KREnemyRoot root;
    private MaterialPropertyBlock propertyBlock;
    private Color stableColor;
    private float flashTimer;
    private bool isDead;

    private int animIsAggroHash;
    private int animIsMovingHash;
    private int animAttackHash;
    private int animHitHash;
    private int animDieHash;

    public void Initialize(KREnemyRoot owner)
    {
        root = owner;

        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        propertyBlock = new MaterialPropertyBlock();
        CacheAnimatorHashes();

        stableColor = idleColor;
        ApplyColor(stableColor);
    }

    private void Update()
    {
        if (flashTimer <= 0f)
            return;

        flashTimer -= Time.deltaTime;

        if (flashTimer <= 0f)
            ApplyColor(stableColor);
    }

    public void PlayAggro()
    {
        if (isDead)
            return;

        stableColor = aggroColor;
        ApplyColor(stableColor);
        SetAnimatorBool(animIsAggroHash, true);
    }

    public void SetMoving(bool isMoving)
    {
        SetAnimatorBool(animIsMovingHash, isMoving);
    }

    public void PlayAttackCue(float duration)
    {
        if (isDead)
            return;

        TriggerAnimator(animAttackHash);
        FlashColor(attackColor, duration);
    }

    public void PlayHit()
    {
        if (isDead)
            return;

        TriggerAnimator(animHitHash);
        FlashColor(hitColor, hitFlashDuration);
    }

    public void PlayDeath()
    {
        isDead = true;
        flashTimer = 0f;
        stableColor = deadColor;

        SetAnimatorBool(animIsMovingHash, false);
        ApplyColor(deadColor);
        TriggerAnimator(animDieHash);
    }

    private void FlashColor(Color color, float duration)
    {
        ApplyColor(color);
        flashTimer = Mathf.Max(0.01f, duration);
    }

    private void ApplyColor(Color color)
    {
        if (bodyRenderer == null)
            return;

        bodyRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color); // URP Lit 대응.
        propertyBlock.SetColor("_Color", color);     // 기본 셰이더 대응.
        bodyRenderer.SetPropertyBlock(propertyBlock);
    }

    private void CacheAnimatorHashes()
    {
        animIsAggroHash = GetHash(animIsAggro);
        animIsMovingHash = GetHash(animIsMoving);
        animAttackHash = GetHash(animAttackTrigger);
        animHitHash = GetHash(animHitTrigger);
        animDieHash = GetHash(animDieTrigger);
    }

    private int GetHash(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return 0;

        return Animator.StringToHash(parameterName);
    }

    private void SetAnimatorBool(int hash, bool value)
    {
        if (animator == null || hash == 0)
            return;

        animator.SetBool(hash, value);
    }

    private void TriggerAnimator(int hash)
    {
        if (animator == null || hash == 0)
            return;

        animator.SetTrigger(hash);
    }
}
