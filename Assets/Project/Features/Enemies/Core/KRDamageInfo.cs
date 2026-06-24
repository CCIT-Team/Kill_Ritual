using UnityEngine;

public struct KRDamageInfo
{
    public float Amount;          // 실제 피해량.
    public Vector3 HitPoint;      // 피격 위치. 피격 이펙트, 사운드 위치에 사용한다.
    public Vector3 HitDirection;  // 공격 방향. 넉백, 피격 회전, 혈흔 방향에 사용한다.
    public Transform Attacker;    // 공격자. 어그로 전환, 킬 보상, 로그에 사용한다.

    public KRDamageInfo(float amount, Vector3 hitPoint, Vector3 hitDirection, Transform attacker)
    {
        Amount = amount;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        Attacker = attacker;
    }
}
