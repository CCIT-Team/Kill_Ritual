// Assets/Project/Features/Weapons/Visual/KRAttackAnimationSignal.cs
using KillRitual.Player.CameraEffects;
using UnityEngine;

namespace KillRitual.Weapons.Visual
{
    /// <summary>
    /// 공격 애니메이션 클립의 Animation Event를 받아
    /// 카메라 셰이크 같은 연출 신호로 전달하는 브릿지.
    ///
    /// 애니메이션 클립은 카메라를 직접 건드리지 않고,
    /// 이 컴포넌트에 "어떤 연출을 실행할지"만 알려준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRAttackAnimationSignal : MonoBehaviour
    {
        [Header("Camera Shake")]
        [Tooltip("비워두면 Awake에서 씬 안의 KRCameraShakeController를 자동으로 찾습니다.")]
        [SerializeField] private KRCameraShakeController _cameraShake;

        [Header("Fallback")]
        [Tooltip("Animation Event에서 문자열 파라미터 없이 호출할 때 사용할 기본 셰이크 ID입니다.")]
        [SerializeField] private string _defaultShakeId = "Default";

        private void Awake()
        {
            if (_cameraShake == null)
            {
                _cameraShake = FindFirstObjectByType<KRCameraShakeController>();
            }
        }

        /// <summary>
        /// Animation Event에서 호출.
        /// String Parameter에 Shake ID를 넣으면 된다.
        ///
        /// 예:
        /// Function: CameraShake
        /// String: Shotgun_Heavy
        /// </summary>
        public void CameraShake(string shakeId)
        {
            if (_cameraShake == null)
            {
                Debug.LogWarning("[KRAttackAnimationSignal] CameraShakeController is missing.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(shakeId))
            {
                shakeId = _defaultShakeId;
            }

            _cameraShake.PlayShake(shakeId);
        }

        /// <summary>
        /// Animation Event가 문자열 파라미터를 못 받는 상황용 기본 셰이크.
        /// </summary>
        public void CameraShakeDefault()
        {
            CameraShake(_defaultShakeId);
        }
    }
}