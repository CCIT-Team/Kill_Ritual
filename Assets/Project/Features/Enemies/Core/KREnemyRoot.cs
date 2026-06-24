using UnityEngine;

[DisallowMultipleComponent]
public class KREnemyRoot : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string enemyName = "Enemy";
    [SerializeField] private KREnemyGrade grade = KREnemyGrade.Normal;

    [Header("Validation")]
    [SerializeField] private bool logMissingOptionalComponents = true;
    // true면 빠진 컴포넌트를 경고한다. 자동 AddComponent는 하지 않는다.

    public string EnemyName => enemyName;
    public KREnemyGrade Grade => grade;

    public KREnemyAIBase AI { get; private set; }
    public KREnemyHealth Health { get; private set; }
    public KREnemyTarget Target { get; private set; }
    public KREnemyPerception Perception { get; private set; }
    public KREnemyMotor Motor { get; private set; }
    public KREnemyVisualFeedback Visual { get; private set; }

    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        CacheComponents();
        InitializeComponents();
    }

    private void OnEnable()
    {
        if (Health != null)
        {
            Health.Damaged += HandleDamaged;
            Health.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (Health != null)
        {
            Health.Damaged -= HandleDamaged;
            Health.Died -= HandleDied;
        }
    }

    private void CacheComponents()
    {
        AI = GetComponent<KREnemyAIBase>();
        Health = GetComponent<KREnemyHealth>();
        Target = GetComponent<KREnemyTarget>();
        Perception = GetComponent<KREnemyPerception>();
        Motor = GetComponent<KREnemyMotor>();
        Visual = GetComponent<KREnemyVisualFeedback>();

        // 적 프리팹 구성이 기획 의도이므로, 코드가 몰래 AddComponent하지 않는다.
        if (AI == null)
            Debug.LogError($"[{nameof(KREnemyRoot)}] {name}에 몬스터별 AI가 없습니다. 예: KRMeleeGhoulAI 또는 KRChargerEliteAI를 붙이세요.", this);

        if (logMissingOptionalComponents)
        {
            if (Health == null)
                Debug.LogWarning($"[{nameof(KREnemyRoot)}] {name}에 KREnemyHealth가 없습니다. 무적/트랩 의도가 아니라면 추가하세요.", this);

            if (Target == null)
                Debug.LogWarning($"[{nameof(KREnemyRoot)}] {name}에 KREnemyTarget이 없습니다. 타겟을 찾는 적이라면 추가하세요.", this);

            if (Motor == null)
                Debug.LogWarning($"[{nameof(KREnemyRoot)}] {name}에 KREnemyMotor가 없습니다. 이동하는 적이라면 KRNavMeshEnemyMotor를 추가하세요.", this);
        }
    }

    private void InitializeComponents()
    {
        Target?.Initialize(this);
        Perception?.Initialize(this);
        Motor?.Initialize(this);
        Visual?.Initialize(this);
        AI?.Initialize(this);

        IsInitialized = true;
    }

    private void HandleDamaged(KRDamageInfo damageInfo, float currentHealth, float maxHealth)
    {
        Visual?.PlayHit();
        AI?.OnDamaged(damageInfo);
    }

    private void HandleDied(KRDamageInfo damageInfo)
    {
        AI?.OnDeath(damageInfo);
        Motor?.Stop();
        Visual?.PlayDeath();
    }
}
