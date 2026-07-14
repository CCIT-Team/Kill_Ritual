// Assets/Project/Features/Core/Interfaces/IDamageable.cs
using UnityEngine;
using KillRitual.Core.Damage;

namespace KillRitual.Core.Interfaces
{
    public enum ExecutionSource
    {
        Absorption, // 흡혼 — 체력 회복 (KRAbsorptionSystem)
        Jakdu,      // 작두 — 탄약 드롭 (KRJakduSystem, 추후 구현)
        Default     // 기타 (테스트, 외부 호출 등)
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        bool IsGroggy { get; }
        Vector3 Position { get; }

        void TakeDamage(KRDamageContext context);

        void Execute(ExecutionSource source = ExecutionSource.Default);
    }
}