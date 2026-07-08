using UnityEngine;

/// <summary>
/// 맵에 이미 배치해둔 포탈 오브젝트에 부착하는 컴포넌트.
/// - 기존 트리거 콜라이더로 플레이어 진입을 감지해 CombatArena에 알림
/// - 구역이 잠기면 별도의 차단용 콜라이더를 켜서 실제로 통행을 막음
///   (트리거 콜라이더는 물리적으로 막지 못하기 때문에 분리되어 있음)
/// - 선택적으로 Animator의 Open/Close 트리거를 재생해 문 여닫는 연출 가능
/// </summary>
[RequireComponent(typeof(Collider))]
public class ArenaPortal : MonoBehaviour
{
    [Header("트리거 감지")]
    [Tooltip("플레이어 태그")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("진입 감지용 트리거 콜라이더 (비워두면 이 오브젝트의 Collider를 자동 사용)")]
    [SerializeField] private Collider triggerCollider;

    [Header("물리적 차단")]
    [Tooltip("구역이 잠겼을 때 켜지는 실제 차단용 콜라이더 (Is Trigger 해제된 것). " +
              "포탈 자식 오브젝트로 얇은 Box Collider를 하나 만들어 연결하는 것을 추천")]
    [SerializeField] private Collider blockingCollider;

    [Header("연출 (선택사항)")]
    [Tooltip("포탈 열림/닫힘 애니메이션을 재생할 Animator (없으면 비워둬도 됨)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string closeTrigger = "Close";
    [SerializeField] private string openTrigger = "Open";

    private CombatArena owner;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();

        // 평소(구역 비활성 상태)엔 차단 콜라이더를 꺼둬서 자유롭게 통과 가능
        if (blockingCollider != null) blockingCollider.enabled = false;
    }

    /// <summary>
    /// CombatArena가 자기 자신을 등록할 때 호출.
    /// </summary>
    public void Bind(CombatArena arena)
    {
        owner = arena;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;
        if (!other.CompareTag(playerTag)) return;

        owner.OnPortalEntered(this);
    }

    /// <summary>
    /// 구역 잠금/해제 상태에 맞춰 포탈을 닫거나 엽니다.
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (blockingCollider != null)
        {
            blockingCollider.enabled = locked;
        }

        if (animator != null)
        {
            animator.SetTrigger(locked ? closeTrigger : openTrigger);
        }
    }

    /// <summary>
    /// 구역 클리어 완료 후 재발동 방지를 위해 감지 트리거를 꺼버림.
    /// </summary>
    public void DisableTrigger()
    {
        if (triggerCollider != null) triggerCollider.enabled = false;
    }
}