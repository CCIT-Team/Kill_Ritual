// Assets/Project/Features/Enemies/MakeNew/KRBossBodyPart.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Managers;

namespace KillRitual.Enemies
{
    /// <summary>
    /// [2026-07-07 신규] "부위타격(부위별 약점)" 구현의 핵심 컴포넌트입니다.
    ///
    /// [배경 - 불가살이 보스 기획]
    /// "온몸을 덮은 철갑 때문에 평상시에는 거의 피해를 받지 않지만, 공격 시 해당 부위가 약점이 됨"
    /// → 보스 전체가 하나의 체력/피격판정을 갖는 기존 방식(KREnemyBase 단일 콜라이더)으로는
    /// "이번엔 어깨만 맞아야 하고, 다음엔 코만 맞아야 한다"를 표현할 수 없습니다.
    ///
    /// [구현 방식]
    /// 몸통과 별개로, 어깨/코/머리/앞다리/등 같은 부위마다 자기 자신의 작은 콜라이더 +
    /// 이 컴포넌트를 붙입니다. 이 컴포넌트가 IDamageable을 직접 구현해서, 기존에 플레이어의
    /// 무기/작두/흡혼이 "콜라이더 → KRManagers.Combat.Lookup() → IDamageable.TakeDamage()"로
    /// 피해를 주는 파이프라인에 그대로 올라탑니다(새 피격 시스템을 따로 만들 필요 없음).
    ///
    /// - 평소(_isExposed = false): 철갑 상태. 받는 피해가 _armoredDamageRatio(기본 0 = 완전 무적)로 대폭 감소합니다.
    /// - 노출 중(_isExposed = true): 정상 피해(+ 선택적으로 _exposedDamageMultiplier 보너스)가
    ///   그대로 부모의 KREnemyBase.TakeDamage()로 전달되어 실제 보스 체력을 깎습니다.
    ///
    /// 보스 컨트롤러(KRBossJakdu01)가 각 패턴 진행 상황에 맞춰 SetExposed(true/false)를 호출해
    /// "지금은 이 부위만 때릴 수 있다"를 구현합니다.
    ///
    /// [씬/프리팹 설정]
    /// 부위마다 빈 자식 오브젝트를 만들고 Collider(Trigger 아님, 물리 충돌은 몸통이 이미 담당하므로
    /// 여기 콜라이더는 IsTrigger=true로 두고 피격 판정 전용으로 씁니다) + 이 컴포넌트를 붙이세요.
    /// _partName은 디버그 로그 구분용이고, 인스펙터에서 부위 이름(예: "왼쪽 어깨")으로 채워주세요.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KRBossBodyPart : MonoBehaviour, IDamageable
    {
        [Tooltip("디버그 로그에 표시할 부위 이름(예: 왼쪽 어깨, 코, 머리, 앞다리, 등).")]
        [SerializeField] private string _partName = "Part";

        [Tooltip("평소(철갑 상태)에 받는 피해 비율. 0 = 완전 무적, 0.1 = 10%만 들어감.")]
        [Range(0f, 1f)]
        [SerializeField] private float _armoredDamageRatio = 0f;

        [Tooltip("노출 상태일 때 받는 피해에 곱해지는 배율. 1보다 크면 약점 보너스 데미지.")]
        [Min(0f)]
        [SerializeField] private float _exposedDamageMultiplier = 1.5f;

        [Tooltip("노출 상태일 때 시각 피드백으로 이 부위 색을 바꿀지 여부. " +
                 "부위 전용 Renderer가 있을 때만 동작합니다(없으면 무시).")]
        [SerializeField] private bool _tintWhenExposed = true;

        [SerializeField] private Color _exposedTintColor = Color.yellow;

        [Tooltip("[2026-07-07 추가] 노출 상태일 때만 켜지는 시각 표시용 자식 오브젝트(예: 발광 구체). " +
                 "마스토돈은 하나의 스킨드 메시라 부위 콜라이더 자체엔 Renderer가 없는 경우가 많아 " +
                 "위 _tintWhenExposed 틴트가 안 먹습니다. 이 필드에 마커 오브젝트(자식으로 배치, 평소엔 " +
                 "꺼둔 상태)를 넣어두면 노출 시 SetActive(true)/철갑 복귀 시 SetActive(false)로 확실하게 보여줍니다. " +
                 "비워두면 기존 방식(있으면 Renderer 틴트)만 동작합니다.")]
        [SerializeField] private GameObject _weakPointIndicator;

        [Tooltip("[2026-07-07 추가] 철갑 상태(무적)일 때 맞으면 튕겨나가는 걸 보여줄 VFX 프리팹(선택). " +
                 "Assets/Project/Art/VFX/MetalImpacts.prefab처럼 화려한 파티클을 쓰고 싶으면 연결하세요. " +
                 "비워두면 자동으로 흰색 구체 오브젝트가 잠깐 나타났다 사라지는 방식으로 대체됩니다 " +
                 "(아무것도 미리 준비 안 해도 바로 동작).")]
        [SerializeField] private GameObject _armorBlockVfxPrefab;

        [Tooltip("VFX가 자동으로 사라지기까지의 시간(초).")]
        [Min(0.2f)]
        [SerializeField] private float _armorBlockVfxLifetime = 2f;

        [Tooltip("[2026-07-07 추가] 약점 적중 시 표시할 색(철갑 막힘=흰색과 확실히 구분되도록 기본은 빨강).")]
        [SerializeField] private Color _weakPointHitFlashColor = new Color(1f, 0.15f, 0.1f, 1f);

        private KREnemyBase _owner;
        private Collider _collider;
        private Renderer _partRenderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int kBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int kColorId = Shader.PropertyToID("_Color");

        private bool _isExposed;

        /// <summary>지금 이 부위가 노출(약점) 상태인지 여부. 보스 컨트롤러가 읽기/쓰기 둘 다 사용합니다.</summary>
        public bool IsExposed => _isExposed;

        /// <summary>디버그/로그용 부위 이름.</summary>
        public string PartName => _partName;

        // ── IDamageable ────────────────────────────────────────────────
        // 이 부위 자체는 죽거나 그로기 상태를 갖지 않고, 전부 부모(보스 본체)의 상태를 그대로 비춥니다.
        public bool IsDead => _owner != null && _owner.IsDead;
        public bool IsGroggy => _owner != null && _owner.IsGroggy;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            _owner = GetComponentInParent<KREnemyBase>();
            _collider = GetComponent<Collider>();
            _partRenderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();

            if (_owner == null)
                Debug.LogWarning($"[KRBossBodyPart] {name}: 부모 계층에서 KREnemyBase를 찾지 못했습니다. " +
                                  "피해가 어디로도 전달되지 않습니다.");

            // 평소엔 꺼진 상태로 시작 — 씬/프리팹에서 실수로 켜둔 채로 저장해도 안전하게 시작하도록.
            if (_weakPointIndicator != null)
                _weakPointIndicator.SetActive(false);
        }

        private void OnEnable()
        {
            // [2026-07-07 추가] KREnemyBase가 자기 자신의 콜라이더들을 등록하는 것과 동일한 패턴입니다.
            // (Assets/Project/Features/Enemies/MakeNew/KREnemyBase.cs OnEnable/OnDisable 참고)
            if (_collider != null && KRManagers.Combat != null)
                KRManagers.Combat.Register(_collider, this);
        }

        private void OnDisable()
        {
            if (_collider != null && KRManagers.Combat != null)
                KRManagers.Combat.Unregister(_collider);
        }

        /// <summary>보스 컨트롤러가 패턴 진행에 맞춰 호출합니다. 노출 시작/종료 시 색도 함께 갱신합니다.</summary>
        public void SetExposed(bool exposed)
        {
            _isExposed = exposed;
            Debug.Log($"[KRBossBodyPart] {_partName}: {(exposed ? "노출(약점 활성화)" : "철갑으로 복귀")}");

            if (_weakPointIndicator != null)
                _weakPointIndicator.SetActive(exposed);

            if (!_tintWhenExposed || _partRenderer == null) return;

            // 부위 전용 Renderer가 있을 때만 이 부위만 색을 바꿉니다(보스 전체 OverrideColor와는 별개).
            // [2026-07-07 추가] KREnemyBase.ApplyColor()와 동일한 이유로 null 방어(플레이 모드 중
            // 스크립트 수정 시 도메인 리로드로 _mpb가 null로 리셋될 수 있음).
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Color color = exposed ? _exposedTintColor : Color.white;
            _partRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(kBaseColorId, color);
            _mpb.SetColor(kColorId, color);
            _partRenderer.SetPropertyBlock(_mpb);
        }

        public void TakeDamage(KRDamageContext context)
        {
            if (_owner == null || _owner.IsDead) return;

            float ratio = _isExposed ? _exposedDamageMultiplier : _armoredDamageRatio;
            if (ratio <= 0f)
            {
                // 완전 무적 상태 — 철갑에 튕겨나간 것으로 취급하고 부모에게 전달조차 하지 않습니다.
                // [2026-07-07 추가] "철갑이 있다는 걸 어떻게 확인하나"에 대한 답 — 막혔을 때 로그 +
                // (연결돼 있다면) 스파크 VFX로 눈에 보이는 반응을 줍니다.
                Debug.Log($"[KRBossBodyPart] {_partName}: 철갑에 막힘 (피해 0) - 지금은 약점이 아닙니다");
                SpawnArmorBlockVfx(context.HitPoint);
                return;
            }

            float adjustedAmount = context.DamageAmount * ratio;
            var adjustedContext = new KRDamageContext(adjustedAmount, context.Type, context.HitPoint, context.Direction);

            // [2026-07-07 추가] "약점에 맞은 건지 철갑에 맞은 건지 구분이 안 된다"에 대한 답 —
            // 철갑 막힘과 확실히 다른 로그 + 빨간 크리티컬 색 플래시를 남깁니다.
            Debug.Log($"[KRBossBodyPart] {_partName}: 약점 적중! {adjustedAmount:F1} 데미지");
            SpawnFlash(context.HitPoint, _weakPointHitFlashColor);

            // [2026-07-07 변경] TakeDamage()가 아니라 TakeDamageDirect()를 씁니다. 이미 위에서
            // 철갑/노출 배율을 적용했으니, TakeDamage()가 내부적으로 또 거치는
            // ModifyIncomingDamage()(몸통 전체 방어 훅)에 중복으로 깎이지 않도록 하기 위함입니다.
            // 실제 체력/그로기/사망 처리는 그대로 KREnemyBase가 전담합니다 — 부위별 컴포넌트는
            // 체력을 따로 들고 있지 않고 "얼마나 통과시킬지"만 결정합니다.
            _owner.TakeDamageDirect(adjustedContext);
        }

        public void Execute(ExecutionSource source = ExecutionSource.Default)
        {
            _owner?.Execute(source);
        }

        /// <summary>
        /// [2026-07-07 변경] "VFX 파티클 프리팹을 굳이 연결 안 해도 오브젝트만으로 시각화가 되는지"에
        /// 대한 답입니다. _armorBlockVfxPrefab이 연결돼 있으면 그걸 쓰고(더 화려한 효과 원할 때),
        /// 없으면 코드에서 즉석으로 작은 구체 오브젝트를 만들어 흰색으로 잠깐 띄웠다 지웁니다 —
        /// 씬/프리팹에 아무것도 미리 배치해둘 필요가 없어서 바로 동작합니다.
        /// </summary>
        private void SpawnArmorBlockVfx(Vector3 point)
        {
            if (_armorBlockVfxPrefab != null)
            {
                GameObject vfx = Instantiate(_armorBlockVfxPrefab, point, Quaternion.identity);
                Destroy(vfx, _armorBlockVfxLifetime);
                return;
            }

            SpawnFlash(point, Color.white);
        }

        /// <summary>
        /// [2026-07-07 변경] 색을 인자로 받도록 일반화 — 철갑 막힘(흰색)과 약점 적중(빨강)을
        /// 같은 코드로 만들되 색만 다르게 해서 확실히 구분되게 합니다.
        /// </summary>
        private void SpawnFlash(Vector3 point, Color color)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "HitFlash(임시 오브젝트)";

            // 물리 판정에 끼어들면 안 되므로 콜라이더는 바로 제거 — 순수 시각용입니다.
            Collider flashCollider = flash.GetComponent<Collider>();
            if (flashCollider != null) Destroy(flashCollider);

            flash.transform.position = point;
            flash.transform.localScale = Vector3.one * 0.35f;

            Renderer flashRenderer = flash.GetComponent<Renderer>();
            if (flashRenderer != null)
            {
                // .material(공유 아님)로 접근하면 자동으로 이 오브젝트 전용 머티리얼 인스턴스가
                // 생성되므로, 다른 오브젝트의 머티리얼을 건드릴 걱정 없이 색만 바꿔도 됩니다.
                Material instanceMat = flashRenderer.material;
                if (instanceMat.HasProperty(kColorId)) instanceMat.SetColor(kColorId, color);
                if (instanceMat.HasProperty(kBaseColorId)) instanceMat.SetColor(kBaseColorId, color);
                if (instanceMat.HasProperty("_EmissionColor"))
                {
                    instanceMat.EnableKeyword("_EMISSION");
                    instanceMat.SetColor("_EmissionColor", color * 3f);
                }
            }

            Destroy(flash, 0.15f);
        }
    }
}
