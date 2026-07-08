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

        private void OnTriggerEnter(Collider other)
        {
            if (_hasHitThisDash) return; // 한 번의 돌진에 한 번만 — 다단히트 방지.

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

            target.TakeDamage(new KRDamageContext(_damage, KRDamageType.Fire, hitPoint, direction));
            Debug.Log($"[불가살이] 돌진 콜라이더 적중 - {_damage} 데미지");
        }
    }
}
