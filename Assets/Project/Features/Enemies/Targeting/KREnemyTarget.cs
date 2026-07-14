using UnityEngine;

public class KREnemyTarget : MonoBehaviour
{
    [Header("Default Target Search")]
    [SerializeField] private string defaultTargetTag = "Player";
    // 문자열 태그 의존은 이 컴포넌트 한 곳에만 둔다.

    [SerializeField] private KRAITarget targetOverride;
    // 테스트용 수동 타겟. 플레이어의 KRAITarget을 직접 넣으면 태그 검색 없이 작동한다.

    [SerializeField] private float reacquireInterval = 0.5f;
    // 타겟이 없거나 죽었을 때 다시 찾는 주기. 매 프레임 검색을 피한다.

    public KRAITarget CurrentTarget { get; private set; }
    public Transform TargetTransform => CurrentTarget != null ? CurrentTarget.transform : null;
    public Vector3 AimPosition => CurrentTarget != null ? CurrentTarget.AimPoint.position : transform.position;
    public KRIDamageable CurrentDamageable => CurrentTarget != null ? CurrentTarget.Damageable : null;
    public bool HasValidTarget => CurrentTarget != null && CurrentTarget.IsValidTarget();

    private KREnemyRoot root;
    private float reacquireTimer;
    private bool missingTagLogged;

    public void Initialize(KREnemyRoot owner)
    {
        root = owner;
        reacquireTimer = 0f;
        TryAcquireTarget();
    }

    public void Tick(float deltaTime)
    {
        if (HasValidTarget)
            return;

        reacquireTimer -= deltaTime;

        if (reacquireTimer > 0f)
            return;

        reacquireTimer = Mathf.Max(0.05f, reacquireInterval);
        TryAcquireTarget();
    }

    public bool TryAcquireTarget()
    {
        if (targetOverride != null)
        {
            SetTarget(targetOverride);
            return true;
        }

        GameObject targetObject = null;

        try
        {
            targetObject = GameObject.FindGameObjectWithTag(defaultTargetTag);
        }
        catch (UnityException)
        {
            if (!missingTagLogged)
            {
                Debug.LogWarning($"[{nameof(KREnemyTarget)}] '{defaultTargetTag}' 태그가 없습니다. Player 태그를 만들거나 targetOverride를 넣으세요.", this);
                missingTagLogged = true;
            }

            return false;
        }

        if (targetObject == null)
            return false;

        KRAITarget aiTarget = targetObject.GetComponentInParent<KRAITarget>();

        if (aiTarget == null)
        {
            Debug.LogWarning($"[{nameof(KREnemyTarget)}] {targetObject.name}에 KRAITarget이 없습니다. 플레이어에 KRAITarget을 추가하세요.", targetObject);
            return false;
        }

        SetTarget(aiTarget);
        return true;
    }

    public void SetTarget(KRAITarget newTarget)
    {
        CurrentTarget = newTarget;
    }
}
