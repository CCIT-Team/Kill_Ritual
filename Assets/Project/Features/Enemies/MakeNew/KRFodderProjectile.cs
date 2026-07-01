// Assets/Project/Scripts/05_Enemies/KREnemyProjectile.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 원거리 몬스터(KRFodderRanged)가 쏘는 발사체입니다.
    /// 직선으로 날아가다가 무언가에 닿으면 사라지고, 그 대상이 플레이어(IDamageable)면 데미지를 줍니다.
    /// 아무것도 맞히지 못해도 _lifeTime 초가 지나면 스스로 사라져 화면에 쌓이지 않습니다.
    ///
    /// 콜라이더가 isTrigger(트리거)인 상태를 가정하므로 OnTriggerEnter로 충돌을 감지합니다.
    /// (KRFodderRanged가 자동 생성하는 구는 트리거로 설정됩니다.)
    /// </summary>
    public sealed class KREnemyProjectile : MonoBehaviour
    {
        private float _speed;
        private float _damage;
        private Transform _shooter;     // 쏜 몬스터 자신. 자기 발사체에 자기가 맞지 않도록 무시합니다.
        private Vector3 _direction;
        private float _lifeTime = 5f;   // 최대 비행 시간(초)
        private float _spawnTime;

        /// <summary>
        /// 발사체를 초기화하고 날립니다. KRFodderRanged가 생성 직후 1회 호출합니다.
        /// </summary>
        public void Launch(Vector3 direction, float speed, float damage, Transform shooter)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _shooter = shooter;
            _spawnTime = Time.time;
        }

        private void Update()
        {
            // 직선 등속 운동.
            transform.position += _direction * _speed * Time.deltaTime;

            // 수명이 다하면 스스로 제거합니다.
            if (Time.time - _spawnTime >= _lifeTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 자신을 쏜 몬스터(또는 그 자식)와는 충돌을 무시합니다.
            if (_shooter != null && other.transform.IsChildOf(_shooter))
            {
                return;
            }

            // 다른 몬스터끼리는 서로 안 맞도록, 상대가 몬스터면 무시합니다.
            if (other.GetComponentInParent<KREnemyBase>() != null)
            {
                return;
            }

            // 플레이어 등 데미지를 받을 수 있는 대상이면 데미지를 적용합니다.
            // 게임오버/체력바를 담당하는 KRPlayerDamageFeedback을 우선 찾고, 없으면 일반 IDamageable을 씁니다.
            IDamageable target = other.GetComponentInParent<KillRitual.Player.KRPlayerDamageFeedback>();
            if (target == null)
            {
                target = other.GetComponentInParent<IDamageable>();
            }

            if (target != null && !target.IsDead)
            {
                // KRDamageContext는 속성을 요구하지만 몬스터 발사체에는 속성 개념이 없습니다.
                // 형식상 아무 값(Fire)이나 넣어 전달하며, 플레이어 체력은 속성과 무관하게 깎입니다.
                var context = new KRDamageContext(_damage, KRDamageType.Fire, transform.position, _direction);
                target.TakeDamage(context);
            }

            // 무언가에 닿았으면(벽이든 플레이어든) 발사체는 사라집니다.
            Destroy(gameObject);
        }
    }
}