using KillRitual.Weapons.Visual;
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

            return true;
        }
    }
}