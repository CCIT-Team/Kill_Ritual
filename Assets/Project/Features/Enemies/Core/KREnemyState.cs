public enum KREnemyState
{
    Idle,       // 아직 전투에 들어가지 않은 상태.
    Chasing,    // 타겟을 추적 중인 상태.
    Attacking,  // 공격 동작을 수행 중인 상태.
    Stunned,    // 경직, 파살 가능, 흡혼 가능 상태 확장을 위한 상태.
    Dead        // 사망 상태. AI 갱신을 중단한다.
}

public enum KRAggroPolicy
{
    LockOnFirstDetection, // 한 번 감지하면 끝까지 추적하는 살굿 기본값.
    ForgetAfterLostSight  // 추후 은신/잠입/순찰형 적을 위한 확장값.
}
