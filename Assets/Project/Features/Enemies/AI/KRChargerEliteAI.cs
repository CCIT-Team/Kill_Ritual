using UnityEngine;

public class KRChargerEliteAI : KREnemyAIBase
{
    private enum Phase
    {
        None,
        ChargeWindup,
        Charging,
        ChargeRecovery,
        MeleeWindup,
        MeleeRecovery
    }

    [Header("Charge Condition")]
    [SerializeField] private float minChargeDistance = 4f;
    // 이 거리보다 가까우면 돌진하지 않는다. 너무 가까운 돌진은 반응 불가능하다.

    [SerializeField] private float maxChargeDistance = 12f;
    // 이 거리보다 멀면 돌진하지 않는다. 지나치게 먼 돌진은 불합리하게 느껴진다.

    [Header("Charge Timing")]
    [SerializeField] private float chargeCooldown = 4f;
    [SerializeField] private float chargeWindup = 0.6f;
    [SerializeField] private float chargeDuration = 0.45f;
    [SerializeField] private float chargeRecovery = 0.8f;

    [Header("Charge Damage")]
    [SerializeField] private float chargeSpeed = 12f;
    [SerializeField] private float chargeDamage = 25f;
    [SerializeField] private float chargeHitRadius = 1.6f;
    [SerializeField, Range(1f, 180f)] private float chargeHitAngle = 100f;

    [Header("Close Melee Fallback")]
    [SerializeField] private float meleeStartRange = 1.8f;
    [SerializeField] private float meleeCooldown = 1.2f;
    [SerializeField] private float meleeWindup = 0.3f;
    [SerializeField] private float meleeRecovery = 0.35f;
    [SerializeField] private float meleeDamage = 12f;
    [SerializeField] private float meleeHitRadius = 1.9f;
    [SerializeField, Range(1f, 180f)] private float meleeHitAngle = 90f;

    private Phase phase = Phase.None;
    private float phaseTimer;
    private float chargeCooldownTimer;
    private float meleeCooldownTimer;
    private Vector3 chargeDirection;
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

        TickCooldowns(deltaTime);

        if (CurrentState == KREnemyState.Attacking)
        {
            TickActivePhase(deltaTime);
            return;
        }

        float targetDistance;
        Vector3 direction = Vector3.zero;

        if (Root.Motor != null)
            direction = Root.Motor.GetFlatDirectionTo(Root.Target.AimPosition, out targetDistance);
        else
            targetDistance = Vector3.Distance(transform.position, Root.Target.AimPosition);

        if (CanStartCharge(targetDistance))
        {
            BeginCharge(direction);
            return;
        }

        if (CanStartMelee(targetDistance))
        {
            BeginMelee();
            return;
        }

        ChaseTarget(deltaTime);
    }

    private void TickCooldowns(float deltaTime)
    {
        if (chargeCooldownTimer > 0f)
            chargeCooldownTimer -= deltaTime;

        if (meleeCooldownTimer > 0f)
            meleeCooldownTimer -= deltaTime;
    }

    private bool CanStartCharge(float distance)
    {
        return chargeCooldownTimer <= 0f &&
               distance >= minChargeDistance &&
               distance <= maxChargeDistance;
    }

    private bool CanStartMelee(float distance)
    {
        return meleeCooldownTimer <= 0f && distance <= meleeStartRange;
    }

    private void BeginCharge(Vector3 initialDirection)
    {
        SetState(KREnemyState.Attacking);
        phase = Phase.ChargeWindup;
        phaseTimer = 0f;
        hitApplied = false;
        chargeCooldownTimer = chargeCooldown;
        chargeDirection = initialDirection == Vector3.zero ? transform.forward : initialDirection;

        Root.Motor?.Stop();
        Root.Visual?.PlayAttackCue(chargeWindup);
    }

    private void BeginMelee()
    {
        SetState(KREnemyState.Attacking);
        phase = Phase.MeleeWindup;
        phaseTimer = 0f;
        hitApplied = false;
        meleeCooldownTimer = meleeCooldown;

        Root.Motor?.Stop();
        Root.Visual?.PlayAttackCue(meleeWindup);
    }

    private void TickActivePhase(float deltaTime)
    {
        phaseTimer += deltaTime;

        switch (phase)
        {
            case Phase.ChargeWindup:
                TickChargeWindup(deltaTime);
                break;

            case Phase.Charging:
                TickCharging(deltaTime);
                break;

            case Phase.ChargeRecovery:
                TickChargeRecovery(deltaTime);
                break;

            case Phase.MeleeWindup:
                TickMeleeWindup(deltaTime);
                break;

            case Phase.MeleeRecovery:
                TickMeleeRecovery(deltaTime);
                break;
        }
    }

    private void TickChargeWindup(float deltaTime)
    {
        UpdateChargeDirection(deltaTime);
        StopMovement(deltaTime);

        if (phaseTimer >= chargeWindup)
        {
            phase = Phase.Charging;
            phaseTimer = 0f;
            Root.Visual?.PlayAttackCue(chargeDuration);
        }
    }

    private void TickCharging(float deltaTime)
    {
        Root.Motor?.MoveDirection(chargeDirection, chargeSpeed, deltaTime);

        if (!hitApplied)
        {
            hitApplied = KRAttackHitUtility.TryDamageTargetInCone(
                Root.Target.CurrentTarget,
                transform,
                chargeDamage,
                chargeHitRadius,
                chargeHitAngle
            );
        }

        if (phaseTimer >= chargeDuration)
        {
            phase = Phase.ChargeRecovery;
            phaseTimer = 0f;
        }
    }

    private void TickChargeRecovery(float deltaTime)
    {
        StopMovement(deltaTime);

        if (phaseTimer >= chargeRecovery)
            FinishAttack();
    }

    private void TickMeleeWindup(float deltaTime)
    {
        RotateTowardTarget(deltaTime);
        StopMovement(deltaTime);

        if (phaseTimer >= meleeWindup)
        {
            if (!hitApplied)
            {
                hitApplied = true;
                KRAttackHitUtility.TryDamageTargetInCone(
                    Root.Target.CurrentTarget,
                    transform,
                    meleeDamage,
                    meleeHitRadius,
                    meleeHitAngle
                );
            }

            phase = Phase.MeleeRecovery;
            phaseTimer = 0f;
        }
    }

    private void TickMeleeRecovery(float deltaTime)
    {
        StopMovement(deltaTime);

        if (phaseTimer >= meleeRecovery)
            FinishAttack();
    }

    private void FinishAttack()
    {
        phase = Phase.None;
        phaseTimer = 0f;
        hitApplied = false;
        SetState(KREnemyState.Chasing);
    }

    private void UpdateChargeDirection(float deltaTime)
    {
        if (Root.Motor == null || !HasUsableTarget())
            return;

        Vector3 direction = Root.Motor.GetFlatDirectionTo(Root.Target.AimPosition, out float _);

        if (direction == Vector3.zero)
            return;

        chargeDirection = direction;
        Root.Motor.RotateTowards(direction, deltaTime);
    }

    private void RotateTowardTarget(float deltaTime)
    {
        if (Root.Motor == null || !HasUsableTarget())
            return;

        Vector3 direction = Root.Motor.GetFlatDirectionTo(Root.Target.AimPosition, out float _);
        Root.Motor.RotateTowards(direction, deltaTime);
    }
}
