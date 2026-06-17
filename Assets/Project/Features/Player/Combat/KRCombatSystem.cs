// Assets/Project/Scripts/02_Player/Combat/KRCombatSystem.cs
using System;
using UnityEngine;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Damage;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Data;
using KillRitual.Weapons;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 플레이어의 전투 "입력"과 발사 판정 전반을 담당하는 컨트롤러입니다.
    /// 이동/파쿠르/카메라는 Developer B의 별도 스크립트가 담당하므로 이 클래스는 다루지 않습니다.
    /// 02_Player는 "입력"만 책임지고, 무기의 구체적 격발 계산(투사체 물리 등)은 03_Weapons의
    /// KRPhysicsProjectile에 위임합니다.
    ///
    /// 핵심 기능:
    ///  - 1~5번 키로 오행(火水木土金) 무기 선택, R키 또는 같은 번호키 더블탭(0.3초)으로 공격유형 1↔2 토글
    ///  - 무기별 공격유형 모드가 메모리에 "기억"됨 (다른 무기로 갔다 와도 유지)
    ///  - 자원 지갑(KRResourceWallet)에서 즉시 차감하는 무탄창(無彈倉) 연사 구조
    ///  - 무기별/모드별 독립 쿨다운 (화면 밖에서도 계속 흐름, 퀵스왑해도 영향 없음)
    ///  - Hitscan/HitscanSpread는 NonAlloc 레이캐스트, Projectile/ExplosiveBurst는 KRPhysicsProjectile에 위임
    ///  - 선택된 무기/모드의 공격 형태를 OnDrawGizmosSelected로 씬 뷰에 시각화
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRCombatSystem : MonoBehaviour, IDamageable
    {
        // 오행 5종 = 인덱스 5개. KRDamageType의 정수값(Fire=0 ... Metal=4)과 항상 1:1로 대응합니다.
        private const int kElementCount = 5;
        private const int kModesPerElement = 2;

        [Header("Data 레이어 참조 (ScriptableObject)")]
        [SerializeField] private KRCharacterStatsSO _characterStats;

        [Tooltip("반드시 길이 5, [0]=Fire(화) [1]=Water(수) [2]=Wood(목) [3]=Earth(토) [4]=Metal(금) 순서로 할당해야 합니다.")]
        [SerializeField] private KRElementDataSO[] _elementDataSet = new KRElementDataSO[kElementCount];

        [Header("References")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private Transform _firePoint;

        [Tooltip("Projectile / ExplosiveBurst 타입 공격에 공통으로 사용되는 발사체 프리팹. KRPhysicsProjectile 컴포넌트가 붙어 있거나, 없으면 자동으로 추가됩니다.")]
        [SerializeField] private GameObject _projectilePrefab;

        [SerializeField] private LayerMask _damageableLayerMask = ~0;

        [Header("공용 자원 지갑")]
        [Tooltip("속성별 자원 주머니의 최대치. 모든 오행 속성이 동일한 최대치를 공유합니다.")]
        [SerializeField] private float _maxResourcePerElement = 100f;

        [Header("더블탭 설정")]
        [SerializeField] private float _doubleTapWindow = 0.3f;

        [Header("처형 (Execution)")]
        [Tooltip("그로기 상태인 대상을 처형할 수 있는 최대 거리.")]
        [SerializeField] private float _executionRange = 3f;

        [Tooltip("FirePoint 정면 기준 처형 판정 콘(원뿔)의 전체 각도(도). 시야 밖(예: 등 뒤)의 그로기 대상은 처형되지 않습니다.")]
        [Range(1f, 180f)]
        [SerializeField] private float _executionConeAngleDegrees = 100f;

        [Header("처형 보상(Absorption) 연동")]
        [Tooltip("04_Execution 계열(KREnemyEntity 등)이 발행하는 KRExecutionSuccessEvent를 구독해 체력/자원을 회복합니다.")]
        [SerializeField] private bool _subscribeToExecutionRewards = true;

        // 캐스팅 안전성을 위해 1~5 숫자키를 KRDamageType 정수값 순서(Fire..Metal)와 동일하게 배열로 관리합니다.
        private static readonly KeyCode[] _weaponKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5
        };

        // 처형 보상 시 모든 속성을 순회 회복시키기 위해 한 번만 계산해 캐싱한 전체 원소 배열 (GC 0)
        private static readonly KRDamageType[] _allElements =
        {
            KRDamageType.Fire, KRDamageType.Water, KRDamageType.Wood, KRDamageType.Earth, KRDamageType.Metal
        };

        // NonAlloc 레이캐스트 공용 버퍼. Unity 메인 스레드는 단일 스레드로 순차 실행되므로
        // static 버퍼를 여러 KRCombatSystem 인스턴스(2인 협동)가 공유해도 동시성 문제가 없습니다.
        private static readonly RaycastHit[] _hitscanBuffer = new RaycastHit[16];

        // 처형 대상 탐색 전용 NonAlloc 버퍼.
        private static readonly Collider[] _executionOverlapBuffer = new Collider[16];

        // 무기별(0~4) 현재 선택된 공격유형 모드(0 또는 1). 무기를 전환했다 돌아와도 값이 유지되어 "기억"을 구현합니다.
        private readonly int[] _currentModeIndex = new int[kElementCount];

        // (무기 × 모드) 조합별 "다음 발사 가능 시각(Time.time 절대값)". 무기를 전환해도 이 값은 그대로 흐르므로
        // 독립 쿨다운이 자연스럽게 구현됩니다. 인덱스 = element * 2 + modeIndex.
        private readonly float[] _nextFireReadyTime = new float[kElementCount * kModesPerElement];

        // 1~5번 키 각각의 마지막 입력 시각 (더블탭 판정용)
        private readonly float[] _lastNumberKeyTapTime = new float[kElementCount];

        private KRResourceWallet _resourceWallet;
        private KRDamageType _currentElement = KRDamageType.Fire;
        private float _health;

        // ------------------------------------------------------------------
        // IDamageable 구현부 (플레이어 자신이 데미지를 받는 경우)
        // ------------------------------------------------------------------
        public bool IsDead => _health <= 0f;

        // 플레이어는 별도의 그로기 시스템을 사용하지 않으므로 항상 false를 반환합니다(05_Enemies 전용 상태).
        public bool IsGroggy => false;

        public Vector3 Position => transform.position;

        private void Awake()
        {
            _health = _characterStats != null ? _characterStats.MaxHealth : 100f;
            _resourceWallet = new KRResourceWallet(_maxResourcePerElement);

            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            if (_firePoint == null)
            {
                _firePoint = _playerCamera != null ? _playerCamera.transform : transform;
            }
        }

        private void OnEnable()
        {
            if (_subscribeToExecutionRewards)
            {
                KRManagers.Event.Subscribe<KRExecutionSuccessEvent>(OnExecutionSuccess);
            }
        }

        private void OnDisable()
        {
            if (_subscribeToExecutionRewards)
            {
                KRManagers.Event.Unsubscribe<KRExecutionSuccessEvent>(OnExecutionSuccess);
            }
        }

        private void Update()
        {
            HandleWeaponSelectionInput();
            HandleModeToggleInput();
            HandleFireInput();
            HandleExecutionInput();
        }

        // ------------------------------------------------------------------
        // 무기 선택 / 공격유형(모드) 토글
        // ------------------------------------------------------------------
        private void HandleWeaponSelectionInput()
        {
            for (int i = 0; i < _weaponKeys.Length; i++)
            {
                if (!Input.GetKeyDown(_weaponKeys[i]))
                {
                    continue;
                }

                var element = (KRDamageType)i;

                // 더블탭 판정을 먼저 수행한 뒤 무기를 선택합니다. (선택은 멱등 연산이므로 순서는 무관)
                bool isDoubleTap = IsDoubleTap(ref _lastNumberKeyTapTime[i]);

                SelectElement(element);

                if (isDoubleTap)
                {
                    ToggleMode(element);
                }
            }
        }

        private void HandleModeToggleInput()
        {
            // R키는 더블탭 없이 단발로 "현재 선택된 무기"의 공격유형을 즉시 토글합니다.
            if (Input.GetKeyDown(KeyCode.R))
            {
                ToggleMode(_currentElement);
            }
        }

        /// <summary>더블탭 윈도우(_doubleTapWindow, 기본 0.3초) 내에 같은 키가 다시 눌렸는지 판정합니다.</summary>
        private bool IsDoubleTap(ref float lastTapTime)
        {
            float now = Time.time;
            bool isDoubleTap = (now - lastTapTime) <= _doubleTapWindow;
            lastTapTime = now;
            return isDoubleTap;
        }

        private void SelectElement(KRDamageType element)
        {
            // 사격 모션은 무기 전환 즉시 캔슬되어야 하므로(퀵스왑), 별도의 "발사 중" 상태를 두지 않고
            // 그냥 현재 선택 인덱스만 즉시 교체합니다. 다음 프레임부터는 새 무기 기준으로 입력이 처리되어
            // 자연스럽게 이전 무기의 발사가 캔슬된 것과 동일한 효과를 냅니다.
            _currentElement = element;
        }

        private void ToggleMode(KRDamageType element)
        {
            KRElementDataSO data = GetElementData(element);

            // 금(金)/BFG처럼 HasSecondMode = false인 단일 공격유형 무기는 토글이 무시됩니다.
            if (data == null || !data.HasSecondMode)
            {
                return;
            }

            int idx = (int)element;
            _currentModeIndex[idx] = 1 - _currentModeIndex[idx];
        }

        private KRElementDataSO GetElementData(KRDamageType element)
        {
            int idx = (int)element;

            if (_elementDataSet == null || idx < 0 || idx >= _elementDataSet.Length)
            {
                return null;
            }

            return _elementDataSet[idx];
        }

        private KRAttackModeData GetCurrentModeData(KRDamageType element, KRElementDataSO data)
        {
            int modeIdx = _currentModeIndex[(int)element];

            if (modeIdx == 1 && data.HasSecondMode && data.Mode2 != null)
            {
                return data.Mode2;
            }

            return data.Mode1;
        }

        // ------------------------------------------------------------------
        // 발사 처리 (홀드 연사: Mouse0을 누르고 있는 동안 독립 쿨다운이 허용할 때마다 발사됨)
        // ------------------------------------------------------------------
        private void HandleFireInput()
        {
            if (!Input.GetMouseButton(0))
            {
                return;
            }

            KRElementDataSO data = GetElementData(_currentElement);

            if (data == null)
            {
                return;
            }

            KRAttackModeData mode = GetCurrentModeData(_currentElement, data);

            if (mode == null)
            {
                return;
            }

            int modeIdx = _currentModeIndex[(int)_currentElement];
            int slotIndex = (int)_currentElement * kModesPerElement + modeIdx;

            // 무기 자체의 독립 쿨다운이 아직 끝나지 않았다면(화면 뒤에서도 흐르고 있던 시간 포함) 발사하지 않습니다.
            if (Time.time < _nextFireReadyTime[slotIndex])
            {
                return;
            }

            // 자원 지갑에서 즉시 차감을 시도합니다. 재장전 개념이 없으므로 자원이 부족하면 그냥 발사가 무산됩니다.
            if (!_resourceWallet.TryConsume(_currentElement, mode.ResourceCost))
            {
                return;
            }

            float attackSpeedMultiplier = _characterStats != null ? Mathf.Max(0.01f, _characterStats.AttackSpeedMultiplier) : 1f;
            float effectiveCooldown = mode.Cooldown / attackSpeedMultiplier;
            _nextFireReadyTime[slotIndex] = Time.time + effectiveCooldown;

            float attackMultiplier = _characterStats != null ? _characterStats.AttackMultiplier : 1f;
            float finalDamage = mode.Damage * attackMultiplier;

            switch (mode.AttackType)
            {
                case KRAttackTypeKind.Hitscan:
                    FireHitscan(mode, finalDamage);
                    break;

                case KRAttackTypeKind.HitscanSpread:
                    FireHitscanSpread(mode, finalDamage);
                    break;

                case KRAttackTypeKind.Projectile:
                    FireProjectile(mode, finalDamage, explodesOnImpact: false);
                    break;

                case KRAttackTypeKind.ExplosiveBurst:
                    FireProjectile(mode, finalDamage, explodesOnImpact: true);
                    break;
            }
        }

        /// <summary>목(木)/토(土) 1모드 등에서 사용되는 단일 즉발 레이캐스트.</summary>
        private void FireHitscan(KRAttackModeData mode, float damage)
        {
            Vector3 direction = ApplySpreadJitter(_firePoint.forward, mode.SpreadAngleDegrees);
            int hitCount = Physics.RaycastNonAlloc(_firePoint.position, direction, _hitscanBuffer, mode.Range, _damageableLayerMask);
            ApplyNearestHitscanDamage(hitCount, damage);
        }

        /// <summary>화(火) 샷건류, 토(土) 체인건 등에서 사용되는 다중 펠릿 산탄 레이캐스트.</summary>
        private void FireHitscanSpread(KRAttackModeData mode, float damagePerPellet)
        {
            int pelletCount = Mathf.Max(1, mode.PelletCount);

            for (int p = 0; p < pelletCount; p++)
            {
                Vector3 direction = ApplySpreadJitter(_firePoint.forward, mode.SpreadAngleDegrees);
                int hitCount = Physics.RaycastNonAlloc(_firePoint.position, direction, _hitscanBuffer, mode.Range, _damageableLayerMask);
                ApplyNearestHitscanDamage(hitCount, damagePerPellet);
            }
        }

        /// <summary>NonAlloc 버퍼 안에서 가장 가까운 충돌 1개를 찾아 데미지를 적용합니다(배열 정렬 보장이 없으므로 직접 탐색).</summary>
        private void ApplyNearestHitscanDamage(int hitCount, float damage)
        {
            int closestIndex = -1;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (_hitscanBuffer[i].distance < closestDistance)
                {
                    closestDistance = _hitscanBuffer[i].distance;
                    closestIndex = i;
                }
            }

            if (closestIndex < 0)
            {
                return;
            }

            RaycastHit hit = _hitscanBuffer[closestIndex];
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

            if (target == null || target.IsDead || ReferenceEquals(target, this))
            {
                return;
            }

            var context = new KRDamageContext(damage, _currentElement, hit.point, _firePoint.forward);
            target.TakeDamage(context);
        }

        /// <summary>원뿔(Cone) 내부의 무작위 방향을 산출합니다. spreadAngleDegrees가 0이면 정확히 forward를 반환합니다.</summary>
        private static Vector3 ApplySpreadJitter(Vector3 forward, float spreadAngleDegrees)
        {
            if (spreadAngleDegrees <= 0f)
            {
                return forward;
            }

            float halfAngle = spreadAngleDegrees * 0.5f;
            float randomYaw = UnityEngine.Random.Range(-halfAngle, halfAngle);
            float randomPitch = UnityEngine.Random.Range(-halfAngle, halfAngle);

            Quaternion jitterRotation = Quaternion.Euler(randomPitch, randomYaw, 0f);
            return jitterRotation * forward;
        }

        /// <summary>수(水) 플라즈마류, 금(金) BFG에서 사용되는 물리 투사체 발사.</summary>
        private void FireProjectile(KRAttackModeData mode, float damage, bool explodesOnImpact)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogWarning("[KRCombatSystem] Projectile prefab이 인스펙터에 할당되지 않았습니다.");
                return;
            }

            GameObject instance = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);

            if (!instance.TryGetComponent(out KRPhysicsProjectile projectile))
            {
                projectile = instance.AddComponent<KRPhysicsProjectile>();
            }

            projectile.Initialize(
                elementType: _currentElement,
                damage: damage,
                speed: mode.ProjectileSpeed,
                gravityScale: mode.GravityScale,
                pierceCount: mode.PierceCount,
                explodesOnImpact: explodesOnImpact,
                explosionRadius: mode.ExplosionRadius,
                maxRange: mode.Range,
                owner: this,
                damageableLayerMask: _damageableLayerMask);
        }

        // ------------------------------------------------------------------
        // 처형 입력 (E키): 시야 콘 + 사거리 내에서 그로기 상태인 가장 가까운 대상 1명을 처형합니다.
        // ------------------------------------------------------------------
        private void HandleExecutionInput()
        {
            if (!Input.GetKeyDown(KeyCode.E))
            {
                return;
            }

            IDamageable target = FindNearestExecutableTarget();

            // 그로기 상태가 아니거나 이미 (다른 플레이어 등에 의해) 죽은 대상은 탐색 단계에서부터
            // 걸러지므로, target이 null이 아니라면 안전하게 처형을 실행해도 됩니다.
            if (target == null)
            {
                return;
            }

            target.Execute();
        }

        /// <summary>
        /// FirePoint를 중심으로 _executionRange 반경 내, 정면 _executionConeAngleDegrees 콘 안에 있는
        /// IDamageable 중 그로기 상태인 대상만 후보로 삼아 가장 가까운 1명을 반환합니다.
        /// 후보가 없으면 null을 반환합니다.
        /// </summary>
        private IDamageable FindNearestExecutableTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(_firePoint.position, _executionRange, _executionOverlapBuffer, _damageableLayerMask);

            IDamageable best = null;
            float bestDistance = float.MaxValue;
            float halfAngleCos = Mathf.Cos(_executionConeAngleDegrees * 0.5f * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                IDamageable candidate = _executionOverlapBuffer[i].GetComponentInParent<IDamageable>();

                // 본인 자신, 죽은 대상, 그로기가 아닌 대상은 후보에서 제외합니다.
                // (다른 플레이어가 같은 프레임 안에서 이미 처형을 마쳤다면 IsDead가 true이므로 여기서 자연스럽게 걸러집니다.)
                if (candidate == null || ReferenceEquals(candidate, this) || candidate.IsDead || !candidate.IsGroggy)
                {
                    continue;
                }

                Vector3 toTarget = candidate.Position - _firePoint.position;
                float distance = toTarget.magnitude;

                if (distance <= 0.0001f)
                {
                    continue;
                }

                Vector3 direction = toTarget / distance;
                float dot = Vector3.Dot(_firePoint.forward, direction);

                // 시야 콘 밖(예: 등 뒤)에 있는 대상은 제외합니다.
                if (dot < halfAngleCos)
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        // ------------------------------------------------------------------
        // 처형 성공 보상 수신 (04_Execution의 Absorption 개념 - 기존 KREventBus 인프라 재사용)
        // ------------------------------------------------------------------
        private void OnExecutionSuccess(KRExecutionSuccessEvent evt)
        {
            float maxHealth = _characterStats != null ? _characterStats.MaxHealth : 100f;
            float healAmount = maxHealth * (evt.RecoverHealthAmount / 100f);
            _health = Mathf.Min(maxHealth, _health + healAmount);

            // 처형 보상은 오행 5속성 자원을 모두 동일하게 회복시킵니다.
            for (int i = 0; i < _allElements.Length; i++)
            {
                _resourceWallet.Refill(_allElements[i], evt.RecoverAmmoAmount);
            }
        }

        // ------------------------------------------------------------------
        // IDamageable 구현부 (플레이어가 데미지를 받는 경우)
        // ------------------------------------------------------------------
        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead)
            {
                return;
            }

            _health = Mathf.Max(0f, _health - context.DamageAmount);
        }

        public void Execute()
        {
            // 플레이어는 일반적으로 처형 대상이 아니지만, IDamageable 계약을 만족시키기 위해
            // 안전한 즉사 처리로 구현합니다.
            _health = 0f;
        }

        // ------------------------------------------------------------------
        // 에디터 기즈모: 선택된 무기/모드의 공격 범위를 씬 뷰에 시각화
        // ------------------------------------------------------------------
        private void OnDrawGizmosSelected()
        {
            if (_firePoint == null)
            {
                return;
            }

            KRElementDataSO data = GetElementData(_currentElement);

            if (data == null)
            {
                return;
            }

            KRAttackModeData mode = GetCurrentModeData(_currentElement, data);

            if (mode == null)
            {
                return;
            }

            Gizmos.color = Color.red;

            switch (mode.GizmoShape)
            {
                case KRGizmoShapeKind.Ray:
                    DrawRayGizmo(mode);
                    break;

                case KRGizmoShapeKind.Sphere:
                    DrawSphereGizmo(mode);
                    break;

                case KRGizmoShapeKind.Box:
                    DrawBoxGizmo(mode);
                    break;
            }
        }

        /// <summary>직선 레이 기즈모. Hitscan/Projectile 계열의 사거리를 표시합니다.</summary>
        private void DrawRayGizmo(KRAttackModeData mode)
        {
            Gizmos.DrawLine(_firePoint.position, _firePoint.position + (_firePoint.forward * mode.Range));
        }

        /// <summary>구형 기즈모. ExplosiveBurst(BFG)의 사거리 끝에서의 폭발 반경을 표시합니다.</summary>
        private void DrawSphereGizmo(KRAttackModeData mode)
        {
            Vector3 impactPoint = _firePoint.position + (_firePoint.forward * mode.Range);
            float radius = mode.ExplosionRadius > 0f ? mode.ExplosionRadius : 1f;

            Gizmos.DrawLine(_firePoint.position, impactPoint);
            Gizmos.DrawWireSphere(impactPoint, radius);
        }

        /// <summary>
        /// 사각형 박스 기즈모. 샷건류의 산탄 콘(원뿔)을 각도(SpreadAngleDegrees)와 사거리(Range)로부터
        /// 폭을 역산하여 근사한 박스로 표시합니다. Gizmos.matrix를 firePoint의 회전으로 설정하여
        /// 플레이어가 어느 방향을 보고 있어도 일그러짐 없이 정확히 정렬되도록 합니다.
        /// </summary>
        private void DrawBoxGizmo(KRAttackModeData mode)
        {
            float halfAngleRad = mode.SpreadAngleDegrees * 0.5f * Mathf.Deg2Rad;
            float halfWidth = mode.Range * Mathf.Tan(halfAngleRad);
            float width = Mathf.Max(0.05f, halfWidth * 2f);

            Vector3 boxSize = new Vector3(width, mode.BoxHeight, mode.Range);
            Vector3 boxCenterLocal = new Vector3(0f, 0f, mode.Range * 0.5f);

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(_firePoint.position, _firePoint.rotation, Vector3.one);
            Gizmos.DrawWireCube(boxCenterLocal, boxSize);
            Gizmos.matrix = originalMatrix;
        }

        // ------------------------------------------------------------------
        // [내부 전용] 자원 지갑 - 오행 5속성 공용 자원 주머니를 관리합니다.
        // 별도 파일로 분리하지 않고 KRCombatSystem 내부에 private nested class로 둔 이유는
        // "스크립트 개수를 최소화"하라는 데이터-로직 이원화 철학에 따른 것입니다.
        // ------------------------------------------------------------------
        private sealed class KRResourceWallet
        {
            private readonly float[] _pool = new float[kElementCount];
            private readonly float _maxPerElement;

            public KRResourceWallet(float maxPerElement)
            {
                _maxPerElement = maxPerElement;

                for (int i = 0; i < _pool.Length; i++)
                {
                    _pool[i] = maxPerElement;
                }
            }

            public float Get(KRDamageType element)
            {
                return _pool[(int)element];
            }

            public bool TryConsume(KRDamageType element, float amount)
            {
                int idx = (int)element;

                if (_pool[idx] < amount)
                {
                    return false;
                }

                _pool[idx] -= amount;
                return true;
            }

            public void Refill(KRDamageType element, float amount)
            {
                int idx = (int)element;
                _pool[idx] = Mathf.Min(_maxPerElement, _pool[idx] + amount);
            }
        }
    }
}
