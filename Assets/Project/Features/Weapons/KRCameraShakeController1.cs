// Assets/Project/Features/Player/CameraEffects/KRCameraShakeController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillRitual.Player.CameraEffects
{
    [DisallowMultipleComponent]
    public sealed class KRCameraShakeController : MonoBehaviour
    {
        [Serializable]
        public sealed class ShakeProfile
        {
            [Header("ID")]
            [Tooltip("Animation Event에서 호출할 셰이크 ID입니다. 예: Shotgun_Light, Shotgun_Heavy")]
            public string id = "Default";

            [Header("Time")]
            [Tooltip("셰이크 지속 시간입니다. 일반 공격은 짧게, 강공격은 약간 길게 잡습니다.")]
            [Min(0.01f)]
            public float duration = 0.12f;

            [Tooltip("값이 클수록 빠르게 진동합니다.")]
            [Min(0.1f)]
            public float frequency = 28f;

            [Header("Rotation Shake")]
            [Tooltip("상하 회전 흔들림입니다. FPS에서는 너무 크면 조준이 불쾌해집니다.")]
            public float pitchAmount = 1.2f;

            [Tooltip("좌우 회전 흔들림입니다.")]
            public float yawAmount = 0.5f;

            [Tooltip("카메라 기울어짐 흔들림입니다.")]
            public float rollAmount = 0.8f;

            [Header("Position Shake")]
            [Tooltip("좌우 위치 흔들림입니다.")]
            public float horizontalAmount = 0.025f;

            [Tooltip("상하 위치 흔들림입니다.")]
            public float verticalAmount = 0.018f;

            [Tooltip("앞뒤 위치 흔들림입니다.")]
            public float forwardAmount = 0.015f;
        }

        [Header("Profiles")]
        [SerializeField]
        private ShakeProfile[] _profiles =
        {
            new ShakeProfile
            {
                id = "Default",
                duration = 0.12f,
                frequency = 28f,
                pitchAmount = 1.2f,
                yawAmount = 0.5f,
                rollAmount = 0.8f,
                horizontalAmount = 0.025f,
                verticalAmount = 0.018f,
                forwardAmount = 0.015f
            }
        };

        [Header("Global Scale")]
        [Tooltip("전체 회전 셰이크 배율입니다. 멀미가 심하면 낮추면 됩니다.")]
        [SerializeField, Min(0f)] private float _globalRotationScale = 1f;

        [Tooltip("전체 위치 셰이크 배율입니다. FPS에서는 보통 회전보다 작게 쓰는 편이 안전합니다.")]
        [SerializeField, Min(0f)] private float _globalPositionScale = 1f;

        private readonly Dictionary<string, ShakeProfile> _profileMap = new Dictionary<string, ShakeProfile>();

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;

        private ShakeProfile _currentProfile;
        private float _timer;
        private float _seed;

        private void Awake()
        {
            CacheBaseTransform();
            BuildProfileMap();
        }

        private void OnEnable()
        {
            CacheBaseTransform();
        }

        private void OnDisable()
        {
            StopShake();
        }

        private void LateUpdate()
        {
            if (_currentProfile == null)
            {
                ResetTransform();
                return;
            }

            _timer += Time.deltaTime;

            float duration = Mathf.Max(0.01f, _currentProfile.duration);
            float normalizedTime = Mathf.Clamp01(_timer / duration);

            if (normalizedTime >= 1f)
            {
                StopShake();
                return;
            }

            ApplyShake(normalizedTime);
        }

        public void PlayShake(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("[KRCameraShakeController] Shake id is empty.", this);
                return;
            }

            if (!_profileMap.TryGetValue(id, out ShakeProfile profile))
            {
                Debug.LogWarning($"[KRCameraShakeController] Shake profile not found: {id}", this);
                return;
            }

            _currentProfile = profile;
            _timer = 0f;
            _seed = UnityEngine.Random.Range(0f, 1000f);
        }

        public void StopShake()
        {
            _currentProfile = null;
            _timer = 0f;
            ResetTransform();
        }

        private void CacheBaseTransform()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
        }

        private void BuildProfileMap()
        {
            _profileMap.Clear();

            if (_profiles == null)
            {
                return;
            }

            for (int i = 0; i < _profiles.Length; i++)
            {
                ShakeProfile profile = _profiles[i];

                if (profile == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(profile.id))
                {
                    continue;
                }

                _profileMap[profile.id] = profile;
            }
        }

        private void ApplyShake(float normalizedTime)
        {
            if (_currentProfile == null)
            {
                return;
            }

            // 초반에 강하고 빠르게 줄어드는 감쇠 곡선.
            // 공격 하이라이트용 셰이크는 오래 남기보다 짧게 터지고 사라지는 편이 낫다.
            float envelope = 1f - normalizedTime;
            envelope *= envelope;

            float time = Time.time * _currentProfile.frequency + _seed;

            float noiseX = Mathf.PerlinNoise(time, _seed + 13.17f) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(_seed + 27.44f, time) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(time, time + _seed + 91.3f) * 2f - 1f;

            Vector3 positionOffset = new Vector3(
                noiseX * _currentProfile.horizontalAmount,
                noiseY * _currentProfile.verticalAmount,
                noiseZ * _currentProfile.forwardAmount
            ) * envelope * _globalPositionScale;

            Vector3 rotationOffset = new Vector3(
                noiseY * _currentProfile.pitchAmount,
                noiseX * _currentProfile.yawAmount,
                noiseZ * _currentProfile.rollAmount
            ) * envelope * _globalRotationScale;

            transform.localPosition = _baseLocalPosition + positionOffset;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(rotationOffset);
        }

        private void ResetTransform()
        {
            transform.localPosition = _baseLocalPosition;
            transform.localRotation = _baseLocalRotation;
        }
    }
}