// Assets/Project/Features/Enemies/MakeNew/KRBossChargeHitbox.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
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

        public void Activate(float damage)
        {
            _damage = damage;
            _hasHitThisDash = false;
            if (_collider != null) _collider.enabled = true;
        }

        public void Deactivate()
        {
            if (_collider != null) _collider.enabled = false;
        }

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
