namespace KillRitual.Weapons
{
    public enum KRAttackSlot
    {
        Primary,   // 좌클릭, 약공
        Secondary  // 우클릭, 강공
    }

    public enum KRAttackInputType
    {
        Tap,            // 누르는 순간 1회 발사
        HoldAuto,       // 누르고 있는 동안 반복 발사
        ChargeRelease   // 누르고 차지, 떼면 발사
    }

    public enum KRWeaponActionState
    {
        Idle,
        Busy,
        Holding,
        Charging
    }
}