using UnityEngine;

public class KRMeleeGhoulAI : KREnemyAIBase
{
    private enum AttackPhase
    {
        None,
        Windup,
        Recovery
    }

    [Header("Melee Attack")]
    [SerializeField] private float attackStartRange = 1.8f;
    // 이 거리 안에 들어오면 공격을 시작한다.

    [SerializeField] private float damage = 10f;
    [SerializeField] private float hitRadius = 1.9f;
    [SerializeField, Range(1f, 180f)] private float hitAngle = 90f;

    [Header("Timing")]
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float windup = 0.35f;
    // 공격 선딜. 플레이어가 보고 피할 수 있는 시간이다.

    [SerializeField] private float recovery = 0.35f;
    // 공격 후딜. 회피 후 반격 창구다.

    private AttackPhase phase = AttackPhase.None;
    private float phaseTimer;
    private float cooldownTimer;
    private bool hitApplied;

    protected override void TickAI(float deltaTime)
    {
        if (!HasAggro)
        {
            StopMovement(deltaTime);
            return;
        }

        if (!HasUsableTarget())
        {
            StopMovement(deltaTime);
            return;
        }

        if (cooldownTimer > 0f)
            cooldownTimer -= deltaTime;

        if (CurrentState == KREnemyState.Attacking)
        {
            TickAttack(deltaTime);
            return;
        }

        float targetDistance;

        if (Root.Motor != null)
            Root.Motor.GetFlatDirectionTo(Root.Target.AimPosition, out targetDistance);
        else
            targetDistance = Vector3.Distance(transform.position, Root.Target.AimPosition);

        if (cooldownTimer <= 0f && targetDistance <= attackStartRange)
        {
            BeginAttack();
            return;
        }

        ChaseTarget(deltaTime);
    }

    private void BeginAttack()
    {
        SetState(KREnemyState.Attacking);
        phase = AttackPhase.Windup;
        phaseTimer = 0f;
        hitApplied = false;
        cooldownTimer = attackCooldown;

        Root.Motor?.Stop();
        Root.Visual?.PlayAttackCue(windup);
    }

    private void TickAttack(float deltaTime)
    {
        RotateTowardTarget(deltaTime);
        StopMovement(deltaTime);

        phaseTimer += deltaTime;

        if (phase == AttackPhase.Windup)
        {
            if (phaseTimer >= windup)
            {
                if (!hitApplied)
                {
                    hitApplied = true;
                    KRAttackHitUtility.TryDamageTargetInCone(
                        Root.Target.CurrentTarget,
                        transform,
                        damage,
                        hitRadius,
                        hitAngle
                    );
                }

                phase = AttackPhase.Recovery;
                phaseTimer = 0f;
            }

            return;
        }

        if (phase == AttackPhase.Recovery)
        {
            if (phaseTimer >= recovery)
            {
                phase = AttackPhase.None;
                SetState(KREnemyState.Chasing);
            }
        }
    }

    private void RotateTowardTarget(float deltaTime)
    {
        if (Root.Motor == null || !HasUsableTarget())
            return;

        Vector3 direction = Root.Motor.GetFlatDirectionTo(Root.Target.AimPosition, out float _);
        Root.Motor.RotateTowards(direction, deltaTime);
    }
}
