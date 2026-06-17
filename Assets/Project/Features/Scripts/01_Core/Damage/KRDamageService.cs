using UnityEngine;
using KillRitual.Core.Interfaces;

namespace KillRitual.Core.Damage
{
    /// <summary>
    /// [보조 파일] 데미지 적용 로직을 한 곳에서 관리하는 서비스입니다.
    /// 명세에서 KRManagers.Gameplay.cs가 "KRDamageService Damage" 프로퍼티를 노출하도록
    /// 요구했지만 클래스 자체는 별도 파일로 명시되지 않아, Single File Mandate를 지키기 위해
    /// 이 파일에서 구현했습니다.
    /// 단일 대상 데미지뿐 아니라 AoE(범위) 선형 거리 감쇠 데미지 계산도 이 클래스에서 일괄 처리하여,
    /// KRPhysicsProjectile 등 여러 곳에서 동일한 공식을 재사용할 수 있게 합니다.
    /// </summary>
    public sealed class KRDamageService
    {
        /// <summary>
        /// 단일 대상에게 데미지를 즉시 적용합니다. 이미 사망한 대상에게는 적용하지 않습니다.
        /// </summary>
        public void ApplyDamage(IDamageable target, KRDamageContext context)
        {
            if (target == null || target.IsDead)
            {
                return;
            }

            target.TakeDamage(context);
        }

        /// <summary>
        /// 선형 거리 감쇠 공식을 이용해 AoE 데미지 수치를 계산합니다.
        /// Damage_final = Damage_max * (1 - Distance / Radius)
        /// 중심(Distance = 0)에서는 100%, 반경 경계(Distance = Radius)에서는 0%로 수렴합니다.
        /// </summary>
        public float CalculateLinearDecayDamage(float maxDamage, float distance, float radius)
        {
            if (radius <= 0f)
            {
                return 0f;
            }

            float ratio = Mathf.Clamp01(1f - (distance / radius));
            return maxDamage * ratio;
        }
    }
}
