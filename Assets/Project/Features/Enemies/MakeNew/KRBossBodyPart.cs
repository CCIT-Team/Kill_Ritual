// Assets/Project/Features/Enemies/MakeNew/KRBossBodyPart.cs
using System;
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;
using KillRitual.Core.Managers;

namespace KillRitual.Enemies
{
    [RequireComponent(typeof(Collider))]
    public sealed class KRBossBodyPart : MonoBehaviour, IDamageable
    {
        [Tooltip("디버그 로그/표시용 부위 이름(예: 머리, 몸통, 앞다리, 뒷다리, 꼬리).")]
        [SerializeField] private string _partName = "Part";

        [Tooltip("이 부위가 받는 피해에 곱해지는 배율로, 약점이면 1보다 크게 설정하세요.")]
        [Min(0f)]
        [SerializeField] private float _damageMultiplier = 1f;

        [Tooltip("보스 본체 체력과 별개로 관리되는 이 부위 자체의 체력으로, 0이 되면 파괴되어 OnBroken이 발생합니다.")]
        [Min(1f)]
        [SerializeField] private float _partHealth = 150f;

        [Tooltip("파괴 전, 일반 피격 시 표시할 플래시 색.")]
        [SerializeField] private Color _hitFlashColor = new Color(1f, 0.15f, 0.1f, 1f);

        [Tooltip("파괴된 부위를 표시할 색(머티리얼 틴트에도, 구체 마커 폴백에도 둘 다 씁니다).")]
        [SerializeField] private Color _breakMarkerColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        [Header("파괴 표시 - 머티리얼 틴트")]
        [Tooltip("이 부위에 해당하는 몸통 SkinnedMeshRenderer로, 비워두면 구체 마커로 자동 대체됩니다.")]
        [SerializeField] private Renderer _bodyRenderer;

        [Tooltip("_bodyRenderer의 Materials 리스트에서 이 부위에 해당하는 슬롯 번호이며, -1이면 구체 마커를 씁니다.")]
        [SerializeField] private int _materialSlotIndex = -1;

        [Tooltip("_bodyRenderer/_materialSlotIndex 연결이 안 됐을 때만 폴백으로 쓰이는 구체 마커의 크기입니다.")]
        [Min(0.05f)]
        [SerializeField] private float _breakMarkerScale = 0.4f;

        private KREnemyBase _owner;
        private Collider _collider;

        private float _currentPartHealth;
        private bool _isBroken;

        public bool IsBroken => _isBroken;

        public string PartName => _partName;

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

            if (!_isBroken)
            {
                // 부위가 살아있는 동안에만 _damageMultiplier(부위별 취약/저항 배율)를 적용합니다.
                float adjustedAmount = context.DamageAmount * _damageMultiplier;
                var adjustedContext = new KRDamageContext(adjustedAmount, context.Type, context.HitPoint, context.Direction);

                SpawnFlash(context.HitPoint, _hitFlashColor);

                if (context.IsMuryeongReflected)
                {
                    Debug.Log($"[KRBossBodyPart] {_partName}: 무령 반사 {adjustedAmount:F1} 데미지 " +
                              $"(부위 체력 {_currentPartHealth:F0} → {Mathf.Max(0f, _currentPartHealth - adjustedAmount):F0})");
                    ApplyPartDamage(adjustedAmount);
                }
                else
                {
                    Debug.Log($"[KRBossBodyPart] {_partName}: 일반 피해 {adjustedAmount:F1} " +
                              $"(부위 파괴는 무령 반사탄 전용이라 부위 체력 변화 없음)");
                }

                // [기존과 동일한 이유] TakeDamage()가 아니라 TakeDamageDirect()를 씁니다 — 위에서 이미
                // 부위 배율을 적용했으니, ModifyIncomingDamage()(몸통 전체 방어 훅)를 또 거쳐서
                // 이중으로 깎이지 않도록 하기 위함입니다. 실제 체력/그로기/사망 처리는 그대로
                // KREnemyBase가 전담합니다 — 부위별 컴포넌트는 자기 체력만 별도로 들고 있습니다.
                _owner.TakeDamageDirect(adjustedContext);
            }
            else
            {
                Debug.Log($"[KRBossBodyPart] {_partName}: {context.DamageAmount:F1} 데미지 (이미 파괴된 부위, 배율 미적용)");
                SpawnFlash(context.HitPoint, _hitFlashColor);
                _owner.TakeDamageDirect(context);
            }
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
