// Assets/Project/Features/Core/Interfaces/IDamageable.cs
using UnityEngine;
using KillRitual.Core.Damage;

namespace KillRitual.Core.Interfaces
{
    /// <summary>
    /// 처형 원인을 구분하는 열거형입니다.
    /// Execute()가 호출될 때 어떤 시스템이 처형했는지를 전달하여
    /// 보상(체력/탄약 드롭 등)을 다르게 적용할 수 있습니다.
    /// </summary>
    public enum ExecutionSource
    {
        Absorption, // 흡혼 — 체력 회복 (KRAbsorptionSystem)
        Jakdu,      // 작두 — 탄약 드롭 (KRJakduSystem, 추후 구현)
        Default     // 기타 (테스트, 외부 호출 등)
    }

    /// <summary>
    /// 피격/처형 가능한 모든 오브젝트가 구현하는 인터페이스입니다.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        bool IsGroggy { get; }
        Vector3 Position { get; }

        void TakeDamage(KRDamageContext context);

        /// <summary>
        /// 처형 호출. source로 어떤 시스템이 처형했는지 전달합니다.
        /// 보상(체력/탄약 드롭)은 source에 따라 각 구현체가 분기 처리합니다.
        /// </summary>
        void Execute(ExecutionSource source = ExecutionSource.Default);
    }
}