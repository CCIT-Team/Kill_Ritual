namespace KillRitual.Core.Events
{
    /// <summary>
    /// UI가 구독하는 자원(탄약/체력) 종류를 정의합니다.
    /// </summary>
    public enum KRResourceType
    {
        WindAmmo,
        WaterAmmo,
        FireAmmo,
        Health
    }

    /// <summary>
    /// 자원(탄약/체력)이 변경되었을 때 UI에 브로드캐스트되는 이벤트입니다.
    /// 사격/피격마다 빈번하게 발생하므로 struct로 선언하여 GC 부담을 없앱니다.
    /// </summary>
    public readonly struct KRResourceChangedEvent
    {
        public readonly KRResourceType Type;
        public readonly float CurrentAmount;
        public readonly float MaxAmount;

        public KRResourceChangedEvent(KRResourceType type, float currentAmount, float maxAmount)
        {
            Type = type;
            CurrentAmount = currentAmount;
            MaxAmount = maxAmount;
        }
    }

    /// <summary>
    /// 무기(속성)가 전환되었을 때 UI 아이콘/하이라이트 갱신을 위한 이벤트입니다.
    /// </summary>
    public readonly struct KRWeaponChangedEvent
    {
        /// <summary>0 = Wind, 1 = Water, 2 = Fire</summary>
        public readonly int SelectedIndex;

        public KRWeaponChangedEvent(int selectedIndex)
        {
            SelectedIndex = selectedIndex;
        }
    }

    /// <summary>
    /// 더블탭(R / 1)으로 스페셜 모드가 On/Off로 토글되었을 때
    /// UI 연출(테두리 발광, 색상 변경 등)을 위한 이벤트입니다.
    /// </summary>
    public readonly struct KRWeaponModeChangedEvent
    {
        public readonly bool IsSpecialMode;

        public KRWeaponModeChangedEvent(bool isSpecialMode)
        {
            IsSpecialMode = isSpecialMode;
        }
    }

    /// <summary>
    /// 처형 가능한(그로기) 대상이 사정거리(executionRange) 안에 들어왔거나 벗어났을 때
    /// UI 프롬프트("E: 처형")의 표시/숨김을 제어하기 위한 이벤트입니다.
    /// </summary>
    public readonly struct KRExecutionPromptEvent
    {
        public readonly bool IsActive;
        public readonly string TargetName;

        public KRExecutionPromptEvent(bool isActive, string targetName)
        {
            IsActive = isActive;
            TargetName = targetName;
        }
    }

    /// <summary>
    /// 처형이 성공적으로 완료되었을 때 플레이어에게 회복 보상을 지급하기 위한 이벤트입니다.
    /// KRCombatSystem이 이 이벤트를 구독하여 체력/탄약을 회복시킵니다.
    /// </summary>
    public readonly struct KRExecutionSuccessEvent
    {
        /// <summary>최대 체력 대비 회복 비율(%). 예: 40 = 최대 체력의 40% 회복.</summary>
        public readonly float RecoverHealthAmount;

        /// <summary>속성별로 동일하게 회복되는 탄약 절대량.</summary>
        public readonly float RecoverAmmoAmount;

        public KRExecutionSuccessEvent(float recoverHealthAmount, float recoverAmmoAmount)
        {
            RecoverHealthAmount = recoverHealthAmount;
            RecoverAmmoAmount = recoverAmmoAmount;
        }
    }
}
