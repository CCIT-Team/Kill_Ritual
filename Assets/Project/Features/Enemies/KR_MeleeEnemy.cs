using cowsins;
using System.Collections;
using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("=== 근접 공격 전용 설정 ===")]

    [SerializeField] private float chargeSpeed = 6f;
    // chargeSpeed: 돌진 속도 (일반 이동보다 빠름)

    [SerializeField] private float chargeRange = 5f;
    // chargeRange: 이 거리 안에 있으면 돌진 공격 시도

    [SerializeField] private bool canCharge = true;
    // canCharge: 돌진 공격을 사용할지 여부 (Inspector에서 끄고 켤 수 있음)

    [SerializeField] private float chargeCooldown = 5f;
    // chargeCooldown: 돌진 재사용 대기시간

    [Header("=== 근접 시각 피드백 ===")]

    [SerializeField] private Color chaseColor = new Color(1f, 0.5f, 0f);
    // chaseColor: 추격 중일 때 몬스터 색상 (주황색)

    [SerializeField] private Color attackColor = new Color(1f, 0f, 0f);
    // attackColor: 공격 직전 몬스터 색상 (빨간색)

    [SerializeField] private Color idleColor = new Color(0.3f, 0.8f, 0.3f);
    // idleColor: 대기 중 몬스터 색상 (초록색)

    [SerializeField] private Color chargeColor = new Color(1f, 0.2f, 0f);
    // chargeColor: 돌진 중 색상 (진한 주황빨)

    // ─────────────────────────────────────────
    // 내부 상태 변수
    // ─────────────────────────────────────────
    private bool isCharging = false;         // 현재 돌진 중인지
    private float lastChargeTime = -999f;    // 마지막 돌진 시간
    private float normalSpeed;               // 원래 이동 속도 (돌진 후 복구용)
    private Color currentBodyColor;          // 현재 몸체 색상

    // ─────────────────────────────────────────
    // OnStart: EnemyBase.Start()에서 호출되는 추가 초기화
    // override = 부모 클래스의 virtual 함수를 재정의
    // ─────────────────────────────────────────
    protected override void OnStart()
    {
        normalSpeed = moveSpeed; // 원래 속도 저장

        // 초기 색상을 대기 색상으로 설정
        SetBodyColor(idleColor);
        currentBodyColor = idleColor;

        Debug.Log($"[MeleeEnemy] {name} 초기화 완료");
    }

    // ─────────────────────────────────────────
    // Update: 매 프레임 실행 (EnemyBase.Update + 추가 처리)
    // ─────────────────────────────────────────
    protected override void Update()
    {
        base.Update();
        // base.Update(): 부모 클래스(EnemyBase)의 Update 실행 (상태 머신 포함)
        // 부모 기능을 먼저 실행하고 그 위에 자식 기능 추가

        // 돌진 중이 아닐 때만 돌진 체크
        if (!isCharging && canCharge && !IsDead)
        {
            CheckCharge();
        }
    }

    // ─────────────────────────────────────────
    // CheckCharge: 돌진 조건 확인
    // ─────────────────────────────────────────
    private void CheckCharge()
    {
        if (playerTransform == null) return;

        // 쿨다운 체크
        if (Time.time - lastChargeTime < chargeCooldown) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // 돌진 범위 안에 있고, 추격 중일 때 돌진 시작
        if (dist <= chargeRange && CurrentState == EnemyState.Chase)
        {
            StartCoroutine(ChargeAttack());
        }
    }

    // ─────────────────────────────────────────
    // ChargeAttack: 돌진 공격 코루틴
    // ─────────────────────────────────────────
    private IEnumerator ChargeAttack()
    {
        isCharging = true;
        lastChargeTime = Time.time;

        Debug.Log($"[MeleeEnemy] {name} 돌진 준비!");

        // ① 돌진 전 예고 동작: 색상 변경 + 짧은 대기
        SetBodyColor(chargeColor);
        agent.isStopped = true;  // 잠깐 멈춤 (예비동작)
        yield return new WaitForSeconds(0.3f); // 0.3초 대기 (플레이어에게 예고)

        if (IsDead) { isCharging = false; yield break; }
        // 대기 중에 죽으면 코루틴 종료

        // ② 돌진 실행: 속도 올리고 플레이어 방향으로 돌진
        agent.isStopped = false;
        agent.speed = chargeSpeed; // 빠른 속도로 전환

        float chargeTime = 0.8f;   // 돌진 지속 시간
        float elapsed = 0f;

        while (elapsed < chargeTime && !IsDead)
        {
            if (playerTransform != null)
                agent.SetDestination(playerTransform.position); // 목표 계속 업데이트

            elapsed += Time.deltaTime;

            // 돌진 중 플레이어와 충분히 가까우면 공격
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= attackRange)
            {
                ChargeDamage(); // 돌진 피해 적용
                break;
            }

            yield return null; // 다음 프레임까지 대기
        }

        // ③ 돌진 종료: 속도 원복
        agent.speed = normalSpeed;
        isCharging = false;

        if (!IsDead)
            SetBodyColor(chaseColor); // 추격 색상으로 복구

        Debug.Log($"[MeleeEnemy] {name} 돌진 종료");
    }

    // ─────────────────────────────────────────
    // ChargeDamage: 돌진 충돌 피해 (일반 공격보다 약간 강하게)
    // ─────────────────────────────────────────
    private void ChargeDamage()
    {
        if (playerStats != null && !playerStats.IsDead)
        {
            float chargeDamage = attackDamage * 1.5f; // 돌진 피해 = 기본 피해 x 1.5
            playerStats.TakeDamage(chargeDamage);
            Debug.Log($"[MeleeEnemy] {name} 돌진 피해: {chargeDamage}");
        }
    }

    // ─────────────────────────────────────────
    // PerformAttack: EnemyBase의 공격을 override
    // 근접 몬스터는 공격 전에 색상 변경 추가
    // ─────────────────────────────────────────
    protected override void PerformAttack()
    {
        // 공격 직전 빨간색 플래시
        StartCoroutine(AttackColorFlash());

        // 부모 클래스의 공격 실행 (실제 피해 처리)
        base.PerformAttack();
    }

    // ─────────────────────────────────────────
    // AttackColorFlash: 공격 시 색상 번쩍임 코루틴
    // ─────────────────────────────────────────
    private IEnumerator AttackColorFlash()
    {
        SetBodyColor(attackColor);          // 빨간색으로 변경
        yield return new WaitForSeconds(0.15f); // 0.15초 대기
        SetBodyColor(currentBodyColor);     // 원래 색으로 복구
    }

    // ─────────────────────────────────────────
    // OnStateChanged: 상태 변경 시 색상 변경 (시각 피드백)
    // EnemyBase.OnStateChanged를 override
    // ─────────────────────────────────────────
    protected override void OnStateChanged(EnemyState newState)
    {
        switch (newState)
        {
            case EnemyState.Idle:
                currentBodyColor = idleColor;
                SetBodyColor(idleColor);         // 초록색
                break;

            case EnemyState.Chase:
                currentBodyColor = chaseColor;
                SetBodyColor(chaseColor);        // 주황색
                break;

            case EnemyState.Attack:
                currentBodyColor = attackColor;
                SetBodyColor(attackColor);       // 빨간색
                break;

            case EnemyState.Dead:
                SetBodyColor(Color.gray);        // 회색 (사망)
                break;
        }
    }

    // ─────────────────────────────────────────
    // OnDeath: 사망 시 추가 처리
    // EnemyBase.OnDeath를 override
    // ─────────────────────────────────────────
    protected override void OnDeath()
    {
        isCharging = false;
        StopAllCoroutines();
        // StopAllCoroutines: 이 오브젝트에서 실행 중인 모든 코루틴 중지

        // 사망 연출: 몸체를 회색으로 + 아래로 가라앉기 시작
        SetBodyColor(Color.gray);
        StartCoroutine(DeathSink());
    }

    // ─────────────────────────────────────────
    // DeathSink: 사망 시 오브젝트가 아래로 가라앉는 연출
    // ─────────────────────────────────────────
    private IEnumerator DeathSink()
    {
        float sinkTime = 1.5f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * 2f;
        // Vector3.down = (0, -1, 0) 아래 방향. * 2f = 2유닛 아래

        while (elapsed < sinkTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sinkTime; // 0~1 사이의 진행 비율

            // Lerp: 두 값 사이를 t 비율로 선형 보간 (부드러운 이동)
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    // ─────────────────────────────────────────
    // SetBodyColor: bodyRenderer의 색상을 변경하는 편의 함수
    // ─────────────────────────────────────────
    private void SetBodyColor(Color color)
    {
        if (bodyRenderer != null)
            bodyRenderer.material.color = color;
    }
}