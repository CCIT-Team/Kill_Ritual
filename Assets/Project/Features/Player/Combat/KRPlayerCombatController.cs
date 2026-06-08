using UnityEngine;

public sealed class KRPlayerCombatController : MonoBehaviour
{
    private KRPlayerContext _context;

    [Header("Equipped Weapons")]
    public KRWeaponAction[] weapons = new KRWeaponAction[3];
    private int _currentWeaponIndex = 0;

    [Header("Charging Settings")]
    public float maxChargeTime = 2f;
    private float _chargeTimer = 0f;
    private bool _isCharging = false;
    private float _cooldownTimer = 0f;

    [Header("Execution Settings")]
    public float executionDistance = 3.5f; // 처형 가능한 가까운 거리 (3.5미터 이내)
    private KRExecutionSystem _executionSystem;

    public void Initialize(KRPlayerContext context)
    {
        _context = context;

        // 플레이어 몸통에 함께 붙여둘 처형 시스템을 코드로 연결합니다.
        _executionSystem = GetComponent<KRExecutionSystem>();

        Debug.Log("KRPlayerCombatController: 무기 및 처형 상호작용 키(F) 연동 완료.");
    }

    public void Tick()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        // 1. 처형 버튼 (F키) 입력 감지
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryTriggerExecution();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) SwapWeapon(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwapWeapon(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwapWeapon(2);

        if (weapons == null || _currentWeaponIndex >= weapons.Length) return;
        KRWeaponAction currentWeapon = weapons[_currentWeaponIndex];
        if (currentWeapon == null) return;

        KRWeaponUseContext useContext = new KRWeaponUseContext
        {
            OwnerTransform = transform,
            Camera = _context.Camera.cameraTransform.GetComponent<Camera>(),
            Resources = transform.GetComponent<KRPlayerResourceSystem>(),
            DamageService = _context.GameContext.DamageService,
            EventBus = _context.GameContext.EventBus
        };

        if (Input.GetMouseButtonDown(0) && _cooldownTimer <= 0f)
        {
            if (currentWeapon.CanUse(useContext, 0f))
            {
                _isCharging = true;
                _chargeTimer = 0f;
            }
        }

        if (Input.GetMouseButton(0) && _isCharging)
        {
            _chargeTimer += Time.deltaTime;
        }

        if (Input.GetMouseButtonUp(0) && _isCharging)
        {
            _isCharging = false;
            float chargeRatio = Mathf.Clamp01(_chargeTimer / maxChargeTime);

            if (currentWeapon.CanUse(useContext, chargeRatio))
            {
                currentWeapon.Use(useContext, chargeRatio);
                _cooldownTimer = currentWeapon.cooldown;
            }
        }
    }

    private void TryTriggerExecution()
    {
        if (_executionSystem == null) return;

        Debug.Log("플레이어: 처형 키(F) 입력 확인! 주변 그로기 적 탐색 시작...");

        // ──────────────────────────────────────────────────────────
        // ★ [처형 판정 완전 개조: 범위 자석 방식]
        // 조준선 조준 필요 없이, 플레이어 주변 반경(executionDistance) 안의 모든 충돌체를 긁어모읍니다.
        // ──────────────────────────────────────────────────────────
        Collider[] targetsInRadius = Physics.OverlapSphere(transform.position, executionDistance);

        IExecutableTarget closestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (var col in targetsInRadius)
        {
            // 나 자신은 대상에서 제외
            if (col.transform == transform || col.transform.root == transform.root) continue;

            // 충돌한 물체나 그 부모에게서 처형 계약서가 있는지 검사
            IExecutableTarget target = col.GetComponent<IExecutableTarget>();
            if (target == null) target = col.GetComponentInParent<IExecutableTarget>();

            if (target != null && target.CanExecute())
            {
                // 그로기 상태인 적들 중 나와 가장 가까운 적을 타깃으로 선정합니다.
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }
        }

        // 2. 가장 가까운 그로기 적이 발견되었다면 즉시 처형 집행!
        if (closestTarget != null)
        {
            bool success = _executionSystem.TryExecute(closestTarget);
            if (success)
            {
                Debug.Log("플레이어: 범위 자석 처형 대성공!");
            }
        }
        else
        {
            Debug.LogWarning("플레이어: 주변 3.5미터 이내에 '그로기(처형 대기) 상태'인 적이 한 마리도 없습니다!");
        }
    }

    private void SwapWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length || weapons[index] == null) return;
        _currentWeaponIndex = index;
        _isCharging = false;
        Debug.Log($"주술 속성 변경: {weapons[_currentWeaponIndex].weaponName}");
    }
}