// Assets/Project/Scripts/03_Weapons/KRProjectileWeapon.cs
using UnityEngine;
 
namespace KillRitual.Weapons
{
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

        [Tooltip("폭발 시 실제로 화면에 보이는 시각효과(파티클 등) 프리팹. " +
                 "ExplodesOnImpact가 true일 때만 사용되며, 비워두면 시각효과 없이 데미지만 적용됩니다.")]
        [SerializeField] private GameObject _explosionVfxPrefab;

        protected KRPhysicsProjectile _lastFiredProjectile;

        protected virtual float GetChargeRatio() => 1f;

        protected override void DoFire(float damage)
        {
            _lastFiredProjectile = null;

            if (_projectilePrefab == null)
            {
                Debug.LogWarning($"[{_weaponName}] Projectile Prefab이 인스펙터에 할당되지 않았습니다.");
                return;
            }

            float chargeRatio = Mathf.Clamp01(GetChargeRatio());
            float scaledDamage = damage * chargeRatio;
            float scaledExplosionRadius = _explosionRadius * chargeRatio;

            Transform fp = ResolveFirePoint();

            // [조준점 보정] 총구가 화면 중앙이 아니어도, 투사체는 크로스헤어가 가리키는
            // 지점으로 수렴하도록 fp.rotation 대신 GetAimDirection으로 보정된 방향을 사용합니다.
            Vector3 aimDirection = _combatSystem.GetAimDirection(fp.position, _range);
            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            GameObject instance = Instantiate(_projectilePrefab, fp.position, aimRotation);

            // 충전 비율만큼 투사체의 시각적 크기도 함께 줄어듭니다(원본 프리팹 스케일에 곱연산).
            instance.transform.localScale *= Mathf.Max(0.01f, chargeRatio);

            if (!instance.TryGetComponent(out KRPhysicsProjectile projectile))
            {
                projectile = instance.AddComponent<KRPhysicsProjectile>();
            }

            projectile.Initialize(
                elementType: _element,
                damage: scaledDamage,
                speed: _projectileSpeed,
                gravityScale: _gravityScale,
                pierceCount: _pierceCount,
                explodesOnImpact: _explodesOnImpact,
                explosionRadius: scaledExplosionRadius,
                maxRange: _range,
                owner: _combatSystem.Owner,
                hitscanLayerMask: _combatSystem.HitscanLayerMask,
                explosionLayerMask: _combatSystem.ExplosionLayerMask);

            _lastFiredProjectile = projectile;

            if (_explodesOnImpact && _explosionVfxPrefab != null)
            {
                projectile.ConfigureExplosionVisual(_explosionVfxPrefab);
            }
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

