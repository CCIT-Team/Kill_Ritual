// Assets/Project/Features/Weapons/Visual/KRAttackAnimationSignal.cs
using KillRitual.Player.CameraEffects;
using UnityEngine;

namespace KillRitual.Weapons.Visual
{
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

        public void CameraShakeDefault()
        {
            CameraShake(_defaultShakeId);
        }
    }
}