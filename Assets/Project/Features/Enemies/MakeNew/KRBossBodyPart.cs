// Assets/Project/Features/Enemies/MakeNew/KRBossBodyPart.cs
using System;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Managers;

namespace KillRitual.Enemies
{
    /// <summary>
    /// [2026-07-07 전면 재작성 — "부위타격" 중심 설계로 컨셉 교체]
    ///
    /// 기존 "불가살이"(평소 거의 무적, 특정 패턴이 끝난 직후에만 해당 부위가 잠깐 노출)
    /// 컨셉을 버리고, 몬스터헌터류의 "부위별 체력 + 파괴(break)" 시스템으로 바꿨습니다.
    /// 새 모델(Four Legged Predator.fbx)이 머리/몸통/다리 텍스처가 서로 다르게 나뉘어 있어서,
    /// 그 구분을 그대로 게임플레이 부위 구분으로 쓰기로 했습니다.
    ///
    /// [새 방식 — 기존과 가장 다른 점]
    /// - 더 이상 "노출된 동안만 맞는다"는 시간 제한이 없습니다. 이 부위는 언제든 맞을 수 있습니다.
    /// - 맞을 때마다 (a) 보스 본체 체력(KREnemyBase)에 그대로 피해가 들어가고,
    ///   (b) 이 부위 자신의 체력(_partHealth, 본체 체력과 완전히 별개)도 깎입니다.
    /// - 부위 체력이 0이 되면 그 부위는 "파괴" 상태가 되고 OnBroken 이벤트가 딱 한 번 발생합니다.
    ///   보스 컨트롤러(KRBossJakdu01)가 이걸 구독해서 "이동속도 감소", "돌진 패턴 봉인",
    ///   "강제 다운" 같은 실제 행동 변화를 적용합니다 — 즉 부위 파괴가 그냥 눈요기가 아니라
    ///   전투 자체를 바꿉니다.
    /// - [2026-07-08 변경] 파괴된 부위는 몸통 렌더러의 해당 머티리얼 슬롯 색을 바꿔서 표시합니다
    ///   (모델이 부위별로 머티리얼 슬롯이 나뉘어 있다는 걸 확인해서 반영했습니다 — "메터리얼
    ///   변경으로 보여주면 안되는거야?" 요청 반영). _bodyRenderer/_materialSlotIndex를 안
    ///   연결해두면 예전 방식(그 자리에 작게 남는 구체 마커)으로 자동 대체됩니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KRBossBodyPart : MonoBehaviour, IDamageable
    {
        [Tooltip("디버그 로그/표시용 부위 이름(예: 머리, 몸통, 앞다리, 뒷다리, 꼬리).")]
        [SerializeField] private string _partName = "Part";

        [Tooltip("이 부위가 받는 피해에 곱해지는 배율. 1이면 보정 없음. " +
                 "약점(예: 머리)이면 1보다 크게, 덜 아픈 부위면 1보다 작게 설정하세요.")]
        [Min(0f)]
        [SerializeField] private float _damageMultiplier = 1f;

        [Tooltip("이 부위 자체의 체력입니다(보스 본체 체력과 별개로 관리됩니다). " +
                 "이 체력이 0이 되면 이 부위가 '파괴' 상태가 되어 OnBroken이 발생합니다.")]
        [Min(1f)]
        [SerializeField] private float _partHealth = 150f;

        [Tooltip("파괴 전, 일반 피격 시 표시할 플래시 색.")]
        [SerializeField] private Color _hitFlashColor = new Color(1f, 0.15f, 0.1f, 1f);

        [Tooltip("파괴된 부위를 표시할 색(머티리얼 틴트에도, 구체 마커 폴백에도 둘 다 씁니다).")]
        [SerializeField] private Color _breakMarkerColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        [Header("파괴 표시 - 머티리얼 틴트 (2026-07-08 신규)")]
        [Tooltip("이 부위에 해당하는 몸통 렌더러(모델의 SkinnedMeshRenderer). 보통 모든 부위가 " +
                 "같은 렌더러 하나를 공유합니다(하나의 스킨 메시라서) — 그 렌더러를 여기 끌어다 " +
                 "놓으세요. 비워두면 예전 방식(구체 마커)으로 자동 대체됩니다.")]
        [SerializeField] private Renderer _bodyRenderer;

        [Tooltip("_bodyRenderer의 Materials 리스트에서 이 부위에 해당하는 슬롯 번호(Element 0, 1, 2...). " +
                 "에디터에서 _bodyRenderer를 선택하면 Materials 항목에 머리/몸통/다리 등 부위별로 " +
                 "나뉜 슬롯이 보일 겁니다 — 거기 순서(0부터 시작)를 그대로 적으세요. -1이면 " +
                 "머티리얼 틴트를 안 쓰고 예전 방식(구체 마커)을 씁니다.")]
        [SerializeField] private int _materialSlotIndex = -1;

        [Tooltip("[2026-07-08 더 이상 기본으로 안 씀] _bodyRenderer/_materialSlotIndex가 제대로 " +
                 "연결되면 머티리얼 틴트가 우선이고, 이 구체 마커는 안 씁니다. 연결이 안 됐을 " +
                 "때만(폴백) 자동으로 이 구체 마커가 대신 나옵니다.")]
        [Min(0.05f)]
        [SerializeField] private float _breakMarkerScale = 0.4f;

        private KREnemyBase _owner;
        private Collider _collider;

        private float _currentPartHealth;
        private bool _isBroken;

        /// <summary>이 부위가 파괴되었는지 여부. 보스 컨트롤러가 패턴 가능 여부 등을 판단할 때 씁니다.</summary>
        public bool IsBroken => _isBroken;

        /// <summary>디버그/로그용 부위 이름.</summary>
        public string PartName => _partName;

        /// <summary>
        /// 이 부위가 파괴되는 순간 딱 한 번(중복 없이) 호출됩니다.
        /// 보스 컨트롤러가 Awake()에서 구독해서 이동속도 감소/패턴 봉인/강제 다운 같은
        /// 실제 행동 변화를 적용하세요.
        /// </summary>
        public event Action OnBroken;

        // ── IDamageable ────────────────────────────────────────────────
        // 이 부위 자체는 죽거나 그로기 상태를 갖지 않고, 전부 부모(보스 본체)의 상태를 그대로 비춥니다.
        public bool IsDead => _owner != null && _owner.IsDead;
        public bool IsGroggy => _owner != null && _owner.IsGroggy;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            _owner = GetComponentInParent<KREnemyBase>();
            _collider = GetComponent<Collider>();
            _currentPartHealth = _partHealth;

            if (_owner == null)
                Debug.LogWarning($"[KRBossBodyPart] {name}: 부모 계층에서 KREnemyBase를 찾지 못했습니다. " +
                                  "피해가 어디로도 전달되지 않습니다.");
        }

        private void OnEnable()
        {
            // [기존과 동일한 패턴] KREnemyBase가 자기 자신의 콜라이더들을 등록하는 것과 동일합니다.
            if (_collider != null && KRManagers.Combat != null)
                KRManagers.Combat.Register(_collider, this);
        }

        private void OnDisable()
        {
            if (_collider != null && KRManagers.Combat != null)
                KRManagers.Combat.Unregister(_collider);
        }

        public void TakeDamage(KRDamageContext context)
        {
            if (_owner == null || _owner.IsDead) return;

            float adjustedAmount = context.DamageAmount * _damageMultiplier;
            var adjustedContext = new KRDamageContext(adjustedAmount, context.Type, context.HitPoint, context.Direction);

            if (!_isBroken)
            {
                Debug.Log($"[KRBossBodyPart] {_partName}: {adjustedAmount:F1} 데미지 " +
                          $"(부위 체력 {_currentPartHealth:F0} → {Mathf.Max(0f, _currentPartHealth - adjustedAmount):F0})");
                SpawnFlash(context.HitPoint, _hitFlashColor);
                ApplyPartDamage(adjustedAmount);
            }
            else
            {
                Debug.Log($"[KRBossBodyPart] {_partName}: {adjustedAmount:F1} 데미지 (이미 파괴된 부위)");
            }

            // [기존과 동일한 이유] TakeDamage()가 아니라 TakeDamageDirect()를 씁니다 — 위에서 이미
            // 부위 배율을 적용했으니, ModifyIncomingDamage()(몸통 전체 방어 훅)를 또 거쳐서
            // 이중으로 깎이지 않도록 하기 위함입니다. 실제 체력/그로기/사망 처리는 그대로
            // KREnemyBase가 전담합니다 — 부위별 컴포넌트는 자기 체력만 별도로 들고 있습니다.
            _owner.TakeDamageDirect(adjustedContext);
        }

        private void ApplyPartDamage(float amount)
        {
            _currentPartHealth -= amount;
            if (_currentPartHealth > 0f || _isBroken) return;

            _isBroken = true;
            Debug.Log($"[KRBossBodyPart] {_partName}: 부위 파괴!");
            ApplyBrokenVisual();
            OnBroken?.Invoke();
        }

        public void Execute(ExecutionSource source = ExecutionSource.Default)
        {
            _owner?.Execute(source);
        }

        private static readonly int kFlashColorId = Shader.PropertyToID("_Color");
        private static readonly int kFlashBaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>기존과 동일한 방식 — 준비물 없이 즉석에서 작은 구체를 만들어 색을 입히고
        /// 잠깐 후 지웁니다(순수 시각용, 콜라이더 없음).</summary>
        private void SpawnFlash(Vector3 point, Color color)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "HitFlash(임시 오브젝트)";

            Collider flashCollider = flash.GetComponent<Collider>();
            if (flashCollider != null) Destroy(flashCollider);

            flash.transform.position = point;
            flash.transform.localScale = Vector3.one * 0.35f;
            TintObject(flash, color, emissive: true);

            Destroy(flash, 0.15f);
        }

        /// <summary>
        /// [2026-07-08 신규] 부위 파괴 시각 표시의 진입점입니다. _bodyRenderer/_materialSlotIndex가
        /// 제대로 연결돼 있으면 그 머티리얼 슬롯만 콕 집어서 색을 바꾸고(SetPropertyBlock의
        /// materialIndex 오버로드 — 다른 부위/슬롯엔 영향 없음), 안 연결돼 있으면 예전 방식인
        /// 구체 마커(SpawnPersistentBreakMarker)로 자동 대체합니다.
        /// </summary>
        private void ApplyBrokenVisual()
        {
            if (_bodyRenderer == null || _materialSlotIndex < 0)
            {
                Debug.LogWarning($"[KRBossBodyPart] {_partName}: _bodyRenderer 또는 " +
                                  "_materialSlotIndex가 안 연결되어 있어 머티리얼 틴트 대신 " +
                                  "구체 마커로 대체합니다. 모델의 SkinnedMeshRenderer를 " +
                                  "_bodyRenderer에, 해당 부위의 Materials 슬롯 번호를 " +
                                  "_materialSlotIndex에 연결하면 머티리얼 틴트를 씁니다.");
                SpawnPersistentBreakMarker();
                return;
            }

            var block = new MaterialPropertyBlock();
            _bodyRenderer.GetPropertyBlock(block, _materialSlotIndex);
            block.SetColor(kFlashColorId, _breakMarkerColor);
            block.SetColor(kFlashBaseColorId, _breakMarkerColor);
            _bodyRenderer.SetPropertyBlock(block, _materialSlotIndex);
        }

        /// <summary>
        /// [2026-07-07 신규, 2026-07-08부터 폴백 전용] 부위가 파괴된 자리에 영구적으로 남는 작은
        /// 표시입니다(자동 소멸 안 함). _bodyRenderer/_materialSlotIndex가 연결 안 됐을 때만
        /// ApplyBrokenVisual()이 자동으로 이걸 대신 호출합니다.
        /// </summary>
        private void SpawnPersistentBreakMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"BrokenMarker_{_partName}";

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null) Destroy(markerCollider);

            marker.transform.SetParent(transform, worldPositionStays: false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * _breakMarkerScale;
            TintObject(marker, _breakMarkerColor, emissive: false);
        }

        private void TintObject(GameObject go, Color color, bool emissive)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;

            // .material(공유 아님)로 접근하면 이 오브젝트 전용 머티리얼 인스턴스가 자동 생성되므로
            // 다른 오브젝트에 영향 없이 색만 바꿀 수 있습니다.
            Material instanceMat = r.material;
            if (instanceMat.HasProperty(kFlashColorId)) instanceMat.SetColor(kFlashColorId, color);
            if (instanceMat.HasProperty(kFlashBaseColorId)) instanceMat.SetColor(kFlashBaseColorId, color);

            if (emissive && instanceMat.HasProperty("_EmissionColor"))
            {
                instanceMat.EnableKeyword("_EMISSION");
                instanceMat.SetColor("_EmissionColor", color * 3f);
            }
        }
    }
}
