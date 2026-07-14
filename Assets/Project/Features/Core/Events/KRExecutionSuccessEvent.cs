// Assets/Project/Scripts/01_Core/Events/KRExecutionSuccessEvent.cs
namespace KillRitual.Core.Events
{
    public readonly struct KRExecutionSuccessEvent
    {
        public readonly float RecoverHealthAmount;

        public readonly float RecoverAmmoAmount;

        public KRExecutionSuccessEvent(float recoverHealthAmount, float recoverAmmoAmount)
        {
            RecoverHealthAmount = recoverHealthAmount;
            RecoverAmmoAmount = recoverAmmoAmount;
        }
    }
}