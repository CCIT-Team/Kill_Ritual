public interface KRIDamageable
{
    bool IsDead { get; } // 이미 죽은 대상인지 확인한다. 중복 사망/중복 보상을 막는다.

    void ReceiveDamage(KRDamageInfo damageInfo); // 피해를 받는 공통 진입점이다.
}
