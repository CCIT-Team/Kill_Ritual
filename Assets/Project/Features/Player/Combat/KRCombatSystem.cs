// Assets/Project/Scripts/02_Player/Combat/KRCombatSystem.cs
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
    /// 플레이어의 전투 "입력"과 무기 전환을 담당하는 컨트롤러입니다.
    /// 이동/파쿠르/카메라는 Developer B의 별도 스크립트가 담당하므로 이 클래스는 다루지 않습니다.
    ///
    /// [아키텍처 변경] 기존에는 KRElementDataSO(ScriptableObject) 1개에 모든 무기 스펙을 담고
    /// KRCombatSystem이 직접 레이캐스트/투사체 로직을 디스패치했습니다. 이제는 무기마다 자신만의
    /// 컴포넌트(KRWeaponBase 계열)를 가지며, KRCombatSystem은 다음 역할만 수행하는
    /// "무기 홀더(Weapon Holder)"로 축소되었습니다:
    ///   1. 1~5 숫자키로 현재 장착 속성(오행) 선택
    ///   2. 좌클릭(Mouse0)/우클릭(Mouse1) 입력을 현재 장착된 무기의 NotifyHeld()/NotifyReleased()로 전달
    ///   3. 공용 자원 지갑(오행 5속성), 플레이어 체력(IDamageable), 처형(Execution) 로직 유지
    ///   4. 무기 스크립트가 참조할 공용 서비스(FirePoint, 레이어 마스크, 공격 배율) 제공
    ///
    /// 실제 발사 판정(레이캐스트/투사체/트레이서/가속연사/충전발사)은 03_Weapons의
    /// KRWeaponBase 및 그 자식 클래스(KRHitscanWeapon, KRProjectileWeapon 등)가 전담합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRCombatSystem : MonoBehaviour, IDamageable
    {
        private const int kElementCount = 5;

        [Header("Data 레이어 참조 (ScriptableObject)")]
        [SerializeField] private KRCharacterStatsSO _characterStats;

        [Header("무기 홀더 (인스펙터에서 각 속성의 유형I/유형II 무기 GameObject를 드래그하세요)")]
        [Tooltip("길이 5. [0]=Fire(화) [1]=Water(수) [2]=Wood(목) [3]=Earth(토) [4]=Metal(금) 순서로, " +
                 "각 속성의 \"유형I(좌클릭)\" 무기 컴포넌트를 배치합니다.")]
        [SerializeField] private KRWeaponBase[] _typeOneWeapons = new KRWeaponBase[kElementCount];

        [Tooltip("길이 5, 순서는 위와 동일. 각 속성의 \"유형II(우클릭)\" 무기 컴포넌트를 배치합니다. " +
                 "특정 속성에 유형II가 없다면 해당 인덱스를 비워두세요(우클릭이 안전하게 무시됩니다).")]
        [SerializeField] private KRWeaponBase[] _typeTwoWeapons = new KRWeaponBase[kElementCount];

        [Header("References")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private Transform _firePoint;

        [SerializeField] private LayerMask _damageableLayerMask = ~0;

        [Tooltip("광역 폭발 판정 전용 마스크. Damageable 레이어만 포함하고 Environment(벽/바닥)는 제외합니다. " +
                 "브로드페이즈 후보 수를 줄여 폭발 판정 내로우페이즈 비용을 감소시킵니다.")]
        [SerializeField] private LayerMask _explosionLayerMask = ~0;

        [Header("공용 자원 지갑")]
        [Tooltip("속성별 자원 주머니의 최대치. 길이 5, [0]=Fire(화) [1]=Water(수) [2]=Wood(목) [3]=Earth(토) [4]=Metal(금) 순서. " +
                 "속성마다 다른 최대 탄약량을 줄 수 있습니다(예: 금(金) BFG는 적게, 목(木) 정밀소총은 많게).")]
        [SerializeField] private float[] _maxResourcePerElement = new float[] { 100f, 100f, 100f, 100f, 100f };

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

        // 처형 대상 탐색 전용 NonAlloc 버퍼.
        private static readonly Collider[] _executionOverlapBuffer = new Collider[16];

        // [조준점 보정] FirePoint(총구)가 화면 중앙이 아닌 위치에 있어도, 실제 탄이 카메라가
        // 가리키는 화면 정중앙(조준점)으로 수렴하도록 보정할 때 사용하는 전용 레이캐스트 버퍼.
        private static readonly RaycastHit[] _aimRaycastBuffer = new RaycastHit[8];

        private KRResourceWallet _resourceWallet;
        private KRDamageType _currentElement = KRDamageType.Fire;
        private float _health;

        // ------------------------------------------------------------------
        // 무기 스크립트가 참조하는 공용 서비스 API
        // ------------------------------------------------------------------

        /// <summary>무기의 발사 기준점(총구) Transform.</summary>
        public Transform FirePoint => _firePoint;

        /// <summary>
        /// 플레이어 카메라 참조. 스나이퍼(KRZoomHitscanWeapon)처럼 줌(FOV 조정) 같은
        /// 카메라 효과가 필요한 무기가 이 프로퍼티로 직접 접근합니다.
        /// </summary>
        public Camera PlayerCamera => _playerCamera;

        /// <summary>Hitscan/CCD 판정용 마스크 (Damageable + Environment 포함).</summary>
        public LayerMask HitscanLayerMask => _damageableLayerMask;

        /// <summary>
        /// [조준점 보정] 무기의 총구(muzzleOrigin)가 화면 정중앙이 아닌 곳에 있어도, 카메라가
        /// 가리키는 화면 정중앙의 "조준점"을 향해 탄이 날아가도록 보정된 방향을 계산합니다.
        ///
        /// 동작 방식: 먼저 카메라 위치에서 카메라 정면 방향으로 maxRange만큼 레이캐스트를 쏴서
        /// 화면 중앙이 실제로 가리키는 지점(조준점)을 찾습니다. 그 지점에 아무것도 없으면
        /// maxRange 끝 지점을 조준점으로 삼습니다. 그런 다음 (조준점 - muzzleOrigin) 방향을
        /// 반환합니다. 이렇게 하면 총구 위치가 화면 한쪽으로 치우쳐 있어도, 실제 탄/투사체는
        /// 항상 크로스헤어가 가리키는 지점으로 수렴합니다.
        /// </summary>
        /// <param name="muzzleOrigin">실제 탄/투사체가 출발할 총구 위치 (FirePoint 또는 무기별 발사 지점)</param>
        /// <param name="maxRange">조준점을 탐색할 최대 거리 (무기의 사거리를 그대로 사용하면 됩니다)</param>
        public Vector3 GetAimDirection(Vector3 muzzleOrigin, float maxRange)
        {
            if (_playerCamera == null)
            {
                // 카메라가 없는 비정상 상황에서는 FirePoint의 정면 방향으로 안전하게 폴백합니다.
                return _firePoint != null ? _firePoint.forward : Vector3.forward;
            }

            Vector3 camPos = _playerCamera.transform.position;
            Vector3 camForward = _playerCamera.transform.forward;

            int hitCount = Physics.RaycastNonAlloc(camPos, camForward, _aimRaycastBuffer, maxRange, _damageableLayerMask);
            int closestIndex = FindClosestAimHitIndex(hitCount);

            Vector3 aimPoint = closestIndex >= 0
                ? _aimRaycastBuffer[closestIndex].point
                : camPos + (camForward * maxRange);

            Vector3 direction = aimPoint - muzzleOrigin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : camForward;
        }

        private static int FindClosestAimHitIndex(int hitCount)
        {
            int closestIndex = -1;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (_aimRaycastBuffer[i].distance < closestDistance)
                {
                    closestDistance = _aimRaycastBuffer[i].distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        /// <summary>광역 폭발 판정용 마스크 (Damageable만 포함).</summary>
        public LayerMask ExplosionLayerMask => _explosionLayerMask;

        /// <summary>투사체의 발사 주체. 자기 자신에게는 데미지가 들어가지 않도록 비교에 사용됩니다.</summary>
        public IDamageable Owner => this;

        /// <summary>전역 공격 배율 (KRCharacterStatsSO 기반).</summary>
        public float AttackMultiplier => _characterStats != null ? _characterStats.AttackMultiplier : 1f;

        /// <summary>전역 공격 속도 배율 (KRCharacterStatsSO 기반). 0으로 나누는 사고를 막기 위해 최소값을 보장합니다.</summary>
        public float AttackSpeedMultiplier => _characterStats != null ? Mathf.Max(0.01f, _characterStats.AttackSpeedMultiplier) : 1f;

        /// <summary>지정한 속성의 공용 자원을 소모를 시도합니다. 무기 스크립트가 발사 시 호출합니다.</summary>
        public bool TryConsumeResource(KRDamageType element, float amount)
        {
            return _resourceWallet != null && _resourceWallet.TryConsume(element, amount);
        }

        // ------------------------------------------------------------------
        // [DEBUG] KRCombatDebugOverlay 전용 공개 API
        // ------------------------------------------------------------------

        /// <summary>지정 속성 자원의 현재 잔량 비율(0~1). 오버레이 바 그래프에 사용됩니다.</summary>
        public float GetResourceRatio(KRDamageType element)
        {
            if (_resourceWallet == null) return 0f;
            float max = _resourceWallet.GetMax(element);
            return max > 0f ? _resourceWallet.Get(element) / max : 0f;
        }

        /// <summary>현재 프레임 기준 시야 콘+사거리 안에 처형 가능한 대상이 존재하면 true.</summary>
        /// <summary>현재 장착(선택)된 오행 속성. UI가 탄약 표시 대상을 결정하는 데 사용합니다.</summary>
        public KRDamageType CurrentElement => _currentElement;

        /// <summary>지정 속성의 현재 잔탄(자원) 절대값. 비율이 아닌 실제 수치가 필요한 UI 표시에 사용합니다.</summary>
        public float GetResourceAmount(KRDamageType element)
        {
            return _resourceWallet != null ? _resourceWallet.Get(element) : 0f;
        }

        /// <summary>지정 속성의 최대 자원량. 속성마다 다를 수 있습니다(KRAmmoUI 등이 호출).</summary>
        public float GetMaxResourceAmount(KRDamageType element)
        {
            return _resourceWallet != null ? _resourceWallet.GetMax(element) : 0f;
        }

        public bool HasExecutableTargetNearby => FindNearestExecutableTarget() != null;

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

            // 좌클릭 = 유형I(Mouse0), 우클릭 = 유형II(Mouse1). 각 무기 스크립트가 자신의 발사 로직을 전담합니다.
            HandleFireButton(mouseButton: 0, weaponArray: _typeOneWeapons);
            HandleFireButton(mouseButton: 1, weaponArray: _typeTwoWeapons);

            HandleExecutionInput();
        }

        // ------------------------------------------------------------------
        // 무기 선택 (1~5 숫자키)
        // ------------------------------------------------------------------
        private void HandleWeaponSelectionInput()
        {
            for (int i = 0; i < _weaponKeys.Length; i++)
            {
                if (!Input.GetKeyDown(_weaponKeys[i]))
                {
                    continue;
                }

                var newElement = (KRDamageType)i;

                if (newElement == _currentElement)
                {
                    continue;
                }

                // 무기 전환(퀵스왑) 시, 이전에 장착했던 무기들의 가속/충전 등 임시 상태를 리셋합니다.
                // 버튼을 누르고 있던 도중 무기를 바꿔도 다음에 그 무기로 돌아왔을 때 깨끗한 상태로 시작합니다.
                GetWeapon(_typeOneWeapons, _currentElement)?.NotifyCancelled();
                GetWeapon(_typeTwoWeapons, _currentElement)?.NotifyCancelled();

                _currentElement = newElement;
            }
        }

        /// <summary>지정한 마우스 버튼의 누름/뗌 상태를 현재 장착된 무기에 전달합니다.</summary>
        private void HandleFireButton(int mouseButton, KRWeaponBase[] weaponArray)
        {
            KRWeaponBase weapon = GetWeapon(weaponArray, _currentElement);

            if (weapon == null)
            {
                return;
            }

            if (Input.GetMouseButton(mouseButton))
            {
                weapon.NotifyHeld();
            }
            else
            {
                weapon.NotifyReleased();
            }
        }

        private static KRWeaponBase GetWeapon(KRWeaponBase[] array, KRDamageType element)
        {
            int idx = (int)element;
            return array != null && idx >= 0 && idx < array.Length ? array[idx] : null;
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
            _health = 0f;
        }

        // ------------------------------------------------------------------
        // [내부 전용] 자원 지갑 - 오행 5속성 공용 자원 주머니를 관리합니다.
        // 속성마다 최대치가 다를 수 있으므로 _maxPerElement도 배열로 보관합니다.
        // ------------------------------------------------------------------
        private sealed class KRResourceWallet
        {
            private readonly float[] _pool = new float[kElementCount];
            private readonly float[] _maxPerElement = new float[kElementCount];

            public KRResourceWallet(float[] maxPerElement)
            {
                for (int i = 0; i < kElementCount; i++)
                {
                    // 인스펙터에서 배열 길이를 5보다 작게 줄여놓는 실수를 해도 100f로 안전하게 대체합니다.
                    float max = (maxPerElement != null && i < maxPerElement.Length) ? maxPerElement[i] : 100f;
                    _maxPerElement[i] = max;
                    _pool[i] = max; // 시작 시 가득 채운 상태로 둡니다.
                }
            }

            public float Get(KRDamageType element) => _pool[(int)element];

            public float GetMax(KRDamageType element) => _maxPerElement[(int)element];

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
                _pool[idx] = Mathf.Min(_maxPerElement[idx], _pool[idx] + amount);
            }
        }
    }
}