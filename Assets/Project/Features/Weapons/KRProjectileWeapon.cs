// Assets/Project/Scripts/03_Weapons/KRProjectileWeapon.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 물리 투사체 방식 무기의 공통 구현입니다.
    /// _explodesOnImpact, _gravityScale, _explosionRadius 등을 인스펙터에서 다르게 설정하면
    /// 이 클래스 하나로 다음 무기들을 구현할 수 있습니다.
    ///   수(水) 유형I 플라즈마건     → ExplodesOnImpact=false, GravityScale=0 (등속 직선)
    ///   금(金) 유형I 그레네이드런처 → ExplodesOnImpact=true, GravityScale&gt;0 (포물선), ExplosionRadius 중간
    ///
    /// [BFG/충전구체 전용 기능 안내] 충전 발사나 유도 추적탄처럼 일부 무기에만 필요한 기능은
    /// 이 공용 부모 클래스를 건드리지 않고 KRChargeProjectileWeapon(자식 클래스)에만 추가합니다.
    /// 이렇게 해야 플라즈마건/그레네이드런처의 인스펙터에 불필요한 필드가 노출되지 않습니다.
    /// (가속 연사 기능을 KRHitscanWeapon이 아닌 KRRampingHitscanWeapon에만 추가한 것과 동일한 원칙입니다.)
    /// </summary>
    public class KRProjectileWeapon : KRWeaponBase
    {
        [Header("투사체")]
        [Tooltip("발사할 투사체 프리팹. KRPhysicsProjectile 컴포넌트가 붙어 있거나, 없으면 자동으로 추가됩니다.")]
        [SerializeField] private GameObject _projectilePrefab;

        [Tooltip("투사체 비행 속도 (미터/초)")]
        [Min(0f)]
        [SerializeField] private float _projectileSpeed = 40f;

        [Tooltip("0 = 완전한 등속 직선 운동, 0보다 크면 중력의 영향을 받는 포물선 운동")]
        [Min(0f)]
        [SerializeField] private float _gravityScale = 0f;

        [Tooltip("관통 가능 횟수. 0이면 첫 명중 대상에서 즉시 소멸합니다.")]
        [Min(0)]
        [SerializeField] private int _pierceCount = 0;

        [Header("광역 폭발")]
        [Tooltip("true면 충돌(또는 사거리 소진) 시 광역 폭발 데미지를 발생시킵니다.")]
        [SerializeField] private bool _explodesOnImpact = false;

        [Tooltip("폭발 반경. ExplodesOnImpact가 true일 때만 사용됩니다.")]
        [Min(0f)]
        [SerializeField] private float _explosionRadius = 0f;

        /// <summary>
        /// 가장 최근에 생성한 투사체 인스턴스. KRChargeProjectileWeapon처럼 발사 직후 추가 설정
        /// (예: 유도 추적탄)이 필요한 자식 클래스가 DoFire()를 오버라이드해 이 참조를 사용합니다.
        /// </summary>
        protected KRPhysicsProjectile _lastFiredProjectile;

        protected override void DoFire(float damage)
        {
            _lastFiredProjectile = null;

            if (_projectilePrefab == null)
            {
                Debug.LogWarning($"[{_weaponName}] Projectile Prefab이 인스펙터에 할당되지 않았습니다.");
                return;
            }

            Transform fp = ResolveFirePoint();

            // [조준점 보정] 총구가 화면 중앙이 아니어도, 투사체는 크로스헤어가 가리키는
            // 지점으로 수렴하도록 fp.rotation 대신 GetAimDirection으로 보정된 방향을 사용합니다.
            Vector3 aimDirection = _combatSystem.GetAimDirection(fp.position, _range);
            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            GameObject instance = Instantiate(_projectilePrefab, fp.position, aimRotation);

            if (!instance.TryGetComponent(out KRPhysicsProjectile projectile))
            {
                projectile = instance.AddComponent<KRPhysicsProjectile>();
            }

            projectile.Initialize(
                elementType: _element,
                damage: damage,
                speed: _projectileSpeed,
                gravityScale: _gravityScale,
                pierceCount: _pierceCount,
                explodesOnImpact: _explodesOnImpact,
                explosionRadius: _explosionRadius,
                maxRange: _range,
                owner: _combatSystem.Owner,
                hitscanLayerMask: _combatSystem.HitscanLayerMask,
                explosionLayerMask: _combatSystem.ExplosionLayerMask);

            _lastFiredProjectile = projectile;
        }

        // ------------------------------------------------------------------
        // 에디터 기즈모: 사거리는 직선 레이로, 폭발형이면 사거리 끝에 폭발 반경 구를 함께 표시합니다.
        // ------------------------------------------------------------------
        protected virtual void OnDrawGizmosSelected()
        {
            Transform fp = ResolveFirePoint();
            if (fp == null) return;

            Gizmos.color = Color.red;
            Vector3 endPoint = fp.position + (fp.forward * _range);
            Gizmos.DrawLine(fp.position, endPoint);

            if (_explodesOnImpact)
            {
                Gizmos.DrawWireSphere(endPoint, _explosionRadius > 0f ? _explosionRadius : 1f);
            }
        }
    }
}