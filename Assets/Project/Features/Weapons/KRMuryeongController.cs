using KillRitual.Weapons.Visual;
using UnityEngine;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 무령 입력 컨트롤러.
    /// 현재는 LCtrl 입력 시 무령 패링 애니메이션만 재생한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRMuryeongController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private bool _listenInputDirectly = true;
        [SerializeField] private KeyCode _parryKey = KeyCode.LeftControl;

        [Header("Visual")]
        [SerializeField] private KRMuryeongVisual _visual;

        [Header("Temporary")]
        [SerializeField] private float _inputLockout = 0.2f;

        private float _nextAvailableTime;

        private void Awake()
        {
            if (_visual == null)
                _visual = GetComponentInChildren<KRMuryeongVisual>();
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

            if (_visual != null)
                _visual.PlayParry();

            return true;
        }
    }
}