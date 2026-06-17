// Assets/Project/Scripts/01_Core/Interfaces/IDamageable.cs
using UnityEngine;
using KillRitual.Core.Damage;

namespace KillRitual.Core.Interfaces
{
    /// <summary>
    /// 피격, 그로기, 처형 대상이 준수해야 하는 계약(추상 인터페이스)입니다.
    /// 01_Core는 어떠한 하위 계층(02_Player, 03_Weapons, 05_Enemies 등)의 구체적인 클래스도
    /// 직접 참조하지 않으므로, 모든 전투 로직은 반드시 이 인터페이스를 통해서만 대상과 상호작용합니다.
    /// (예: KRCombatSystem과 KRPhysicsProjectile은 KREnemyEntity를 직접 알지 못하고,
    ///     오직 IDamageable로만 데미지를 적용/조회합니다.)
    /// </summary>
    public interface IDamageable
    {
        /// <summary>해당 대상이 사망했는지 여부.</summary>
        bool IsDead { get; }

        /// <summary>해당 대상이 그로기(처형 대기) 상태인지 여부. 체력 30% 이하 진입 시 true가 됩니다.</summary>
        bool IsGroggy { get; }

        /// <summary>월드 공간 상의 현재 위치. 처형 사거리 판정, AoE 거리 감쇠 계산 등에 사용됩니다.</summary>
        Vector3 Position { get; }

        /// <summary>
        /// 데미지를 적용합니다. 매 프레임 빈번하게 발생할 수 있는 연산이므로,
        /// GC 스파이크 방지를 위해 class가 아닌 struct(값 타입)인 KRDamageContext를 인자로 받습니다.
        /// </summary>
        void TakeDamage(KRDamageContext context);

        /// <summary>
        /// 그로기 상태의 대상에게 처형(즉사 처리)을 실행합니다.
        /// 호출 전 IsGroggy == true 검증, 그리고 처형 사거리 내인지 확인하는 것은
        /// 호출부(KRCombatSystem)의 책임입니다.
        /// </summary>
        void Execute();
    }
}
