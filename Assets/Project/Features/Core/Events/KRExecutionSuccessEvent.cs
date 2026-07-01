// Assets/Project/Scripts/01_Core/Events/KRExecutionSuccessEvent.cs
namespace KillRitual.Core.Events
{
    /// <summary>
    /// 처형이 성공적으로 완료되었을 때 플레이어에게 회복 보상을 지급하기 위한 이벤트입니다.
    /// KREnemyEntity(또는 KRMockExecutionSandbox)가 발행하고, KRCombatSystem이 구독해
    /// 체력/자원을 회복시킵니다. 사격/피격마다 빈번하게 발생하므로 struct로 선언해 GC 부담을 없앱니다.
    /// </summary>
    public readonly struct KRExecutionSuccessEvent
    {
        /// <summary>최대 체력 대비 회복 비율(%). 예: 25 = 최대 체력의 25% 회복.</summary>
        public readonly float RecoverHealthAmount;

        /// <summary>오행 5속성 자원에 각각 더해지는 절대 회복량.</summary>
        public readonly float RecoverAmmoAmount;

        public KRExecutionSuccessEvent(float recoverHealthAmount, float recoverAmmoAmount)
        {
            RecoverHealthAmount = recoverHealthAmount;
            RecoverAmmoAmount = recoverAmmoAmount;
        }
    }
}