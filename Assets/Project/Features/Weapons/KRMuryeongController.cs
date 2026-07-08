using KillRitual.Weapons.Visual;
using KillRitual.Core.Interfaces;
using UnityEngine;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// ���� �Է� ��Ʈ�ѷ�.
    /// ����� LCtrl �Է� �� ���� �и� �ִϸ��̼Ǹ� ����Ѵ�.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRMuryeongController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private bool _listenInputDirectly = true;
        [SerializeField] private KeyCode _parryKey = KeyCode.LeftControl;

        [Header("Visual")]
        [SerializeField] private KRMuryeongVisual _visual;

        [Tooltip("[2026-07-06 추가] 무령 사용 중 현재 장착 무기 손을 숨기는 데 사용합니다. " +
                 "비워두면 같은 오브젝트 부모 계층에서 자동 탐색합니다.")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Header("Temporary")]
        [SerializeField] private float _inputLockout = 0.2f;

        [Header("보스 패링 판정 (2026-07-07 추가)")]
        [Tooltip("지금까지 TryParry()는 판정 없이 연출만 재생했습니다. 이제 이 범위 안에서 " +
                 "IParryable(예: 보스의 예고형 공격)을 찾아, 판정 창이 열려 있으면 패링 성공으로 처리합니다.")]
        [Min(0.1f)]
        [SerializeField] private float _parryCheckRadius = 4f;

        [Tooltip("패링 판정 대상 스캔에 쓸 레이어. 보스/적이 쓰는 레이어를 포함하세요.")]
        [SerializeField] private LayerMask _parryableLayerMask = ~0;

        // KRCombatZone과 동일한 패턴 — 한 프레임에 여러 곳에서 동시에 패링하지 않는다는 전제로
        // 버퍼를 공유합니다.
        private static readonly Collider[] _parryOverlapBuffer = new Collider[16];

        private float _nextAvailableTime;

        private void Awake()
        {
            if (_visual == null)
                _visual = GetComponentInChildren<KRMuryeongVisual>();

            if (_combatSystem == null)
                _combatSystem = GetComponentInParent<KRCombatSystem>();
        }

        private void OnEnable()
        {
            // [2026-07-06 추가] 무령이 실제로 다시 숨겨지는 순간(자동 타이머든 애니메이션 이벤트든)
            // 숨겨뒀던 원래 무기 손을 다시 보여줍니다.
            if (_visual != null)
                _visual.OnHidden += OnMuryeongHidden;
        }

        private void OnDisable()
        {
            if (_visual != null)
                _visual.OnHidden -= OnMuryeongHidden;
        }

        private void OnMuryeongHidden()
        {
            _combatSystem?.SetCurrentWeaponVisualActive(true);
        }

        private void Update()
        {
            if (!_listenInputDirectly)
                return;

            if (Input.GetKeyDown(_parryKey))
                TryParry();
        }

        public bool TryParry()
        {
            if (Time.time < _nextAvailableTime)
                return false;

            _nextAvailableTime = Time.time + _inputLockout;

            // [2026-07-06 추가] 무령을 꺼내는 순간 현재 장착 무기 손을 숨깁니다.
            // 다시 보여주는 시점은 OnMuryeongHidden()에서 처리합니다(무령이 실제로 사라질 때).
            _combatSystem?.SetCurrentWeaponVisualActive(false);

            if (_visual != null)
                _visual.PlayParry();

            // [2026-07-07 추가] 실제 패링 판정 — 주변의 IParryable 중 판정 창이 열려있는 대상을 찾아
            // OnParried()를 호출합니다. 지금까지는 이 판정 자체가 없어서 무령이 항상 "연출만"
            // 재생하고 아무 것도 막지 못했습니다.
            TryPunishNearbyParryable();

            return true;
        }

        /// <summary>
        /// _parryCheckRadius 안에서 IParryable을 찾아 IsParryWindowOpen이 true인 대상 하나에게
        /// OnParried()를 호출합니다(한 번의 패링에 하나만 처리). 아무도 없거나 창이 닫혀있으면
        /// 아무 일도 일어나지 않습니다 — 이 경우 무령은 그냥 빈 스윙(연출만)이 됩니다.
        /// </summary>
        private void TryPunishNearbyParryable()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _parryCheckRadius, _parryOverlapBuffer, _parryableLayerMask);

            for (int i = 0; i < count; i++)
            {
                IParryable parryable = _parryOverlapBuffer[i].GetComponentInParent<IParryable>();
                if (parryable == null || !parryable.IsParryWindowOpen) continue;

                parryable.OnParried();
                break;
            }
        }
    }
}