// Assets/Project/Features/Enemies/MakeNew/KRBossChargeHitbox.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// [2026-07-07 신규] 돌진(Charge) 패턴 전용 피해 판정 콜라이더입니다.
    ///
    /// [기존 방식의 한계]
    /// 이전엔 Pattern_Charge()의 DoChargeDash() 안에서 매 프레임
    /// Vector3.Distance(transform.position, _player.position) <= _chargeHitRadius 로 "맞았는지"를
    /// 판정했습니다. 이건 보스의 피벗(중심점) 기준 구(sphere) 판정이라 실제 돌진하는 몸통의
    /// 모양/방향과 안 맞고(예: 옆으로 비껴가도 중심 거리만 가까우면 맞은 걸로 처리됨),
    /// 정확도가 떨어집니다.
    ///
    /// [이 컴포넌트]
    /// 실제 Trigger 콜라이더로 OnTriggerEnter 판정을 하므로 훨씬 정확합니다. 평소엔 콜라이더를
    /// 꺼둔 채로 두고, 돌진이 시작될 때만 KRBossJakdu01이 Activate(damage)를 호출해 켜고,
    /// 돌진이 끝나면 Deactivate()로 끕니다. 한 번의 돌진에 한 번만 맞도록 내부적으로 막습니다.
    ///
    /// [씬/프리팹 설정] 보스 몸통 앞쪽을 덮는 Collider(Box/Capsule 추천, IsTrigger 체크)가
    /// 붙은 자식 오브젝트를 만들고 이 스크립트를 붙이세요.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KRBossChargeHitbox : MonoBehaviour
    {
        private Collider _collider;
        private KREnemyBase _owner;
        private float _damage;
        private bool _hasHitThisDash;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _collider.enabled = false; // 평소엔 꺼둠 — 돌진 중에만 Activate()로 켭니다.
            _owner = GetComponentInParent<KREnemyBase>();
        }

        /// <summary>돌진 시작 시 호출합니다. 판정을 켜고 이번 돌진의 피해량을 설정합니다.</summary>
        public void Activate(float damage)
        {
            _damage = damage;
            _hasHitThisDash = false;
            if (_collider != null) _collider.enabled = true;
        }

        /// <summary>돌진 종료 시 호출합니다. 판정을 끕니다.</summary>
        public void Deactivate()
        {
            if (_collider != null) _collider.enabled = false;
        }

        /// <summary>
        /// [2026-07-08 신규] "돌진 시각화를 콜라이더 폭만큼 보이게" 요청 반영 — 이 콜라이더의
        /// 실제 폭(돌진 방향과 수직인 가로 폭)을 월드 스케일까지 반영해서 돌려줍니다. 돌진 경로
        /// 시각화(면)를 실제 판정 폭과 정확히 맞추는 데 씁니다. CapsuleCollider(권장)면 지름
        /// (반지름×2), BoxCollider면 가로(X) 크기를 씁니다 — 씬 설정에 따라 콜라이더 타입이
        /// 바뀌어도 안전하게 동작하도록 둘 다 지원합니다.
        /// </summary>
        public float GetWidth()
        {
            if (_collider is CapsuleCollider capsule)
            {
                float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                return capsule.radius * 2f * scale;
            }

            if (_collider is BoxCollider box)
                return box.size.x * transform.lossyScale.x;

            return _collider != null ? _collider.bounds.size.x : 3f;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasHitThisDash) return; // 한 번의 돌진에 한 번만 — 다단히트 방지.

            // [2026-07-08 신규 — KRBossArmorShard에서 발견된 것과 동일한 버그 예방]
            // Player/CameraRoot 하위의 "Absortion Collider"(아이템 자동 흡수용, 스케일 15×13×23m
            // 트리거)처럼 실제 몸이 아닌 게임플레이용 트리거 콜라이더까지 GetComponentInParent로
            // "플레이어를 맞췄다"고 오판할 수 있습니다. 진짜 몸(피지컬) 콜라이더는 트리거가 아니므로
            // (CharacterController), 트리거 콜라이더는 아예 판정에서 제외합니다.
            if (other.isTrigger) return;

            // 기존 KREnemyBase.FindPlayerDamageable()과 동일한 우선순위입니다:
            // KRPlayerDamageFeedback을 우선 찾고 없으면 일반 IDamageable로 폴백합니다.
            IDamageable target = other.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();
            if (target == null) target = other.GetComponentInParent<IDamageable>();

            if (target == null || target.IsDead) return;
            if (_owner != null && ReferenceEquals(target, _owner)) return; // 자기 자신 제외.

            _hasHitThisDash = true;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 direction = (other.transform.position - transform.position);
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            Debug.Log($"[불가살이] 돌진 콜라이더 적중 - {_damage} 데미지, 히트박스 위치 {transform.position}, " +
                      $"충돌지점 {hitPoint}, 대상 콜라이더 {other.name}");
            target.TakeDamage(new KRDamageContext(_damage, KRDamageType.Fire, hitPoint, direction));
        }
    }
}
