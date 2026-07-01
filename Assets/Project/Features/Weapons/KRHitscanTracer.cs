// Assets/Project/Scripts/03_Weapons/KRHitscanTracer.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// Hitscan / HitscanSpread(레이캐스트 즉발) 무기 발사 시, 사람 눈에 "총알이 날아갔다"는
    /// 인상을 주기 위한 시각 효과입니다.
    ///
    /// 레이캐스트는 물리적으로 한 프레임에 즉시 결과가 결정되므로 실제로 날아가는 발사체가
    /// 존재하지 않습니다. 이 컴포넌트는 발사 시작점(총구)과 명중/사거리 소진 지점을 잇는
    /// 짧은 선분을, "탄속(VisualSpeed)"에 맞춰 끝점 쪽으로 빠르게 훑고 지나가도록 그려서
    /// "총알 꼬리"를 흉내 냅니다.
    ///
    /// [LineRenderer + Moving Particle]
    /// LineRenderer는 실제 Transform이 이동하는 것이 아니라 월드 좌표 두 점을 그리는 방식입니다.
    /// 따라서 자식 ParticleSystem을 붙여도 자동으로 선을 따라가지 않습니다.
    /// 이 스크립트는 MovingParticleRoot를 직접 start → end 방향으로 이동시켜,
    /// 총알 머리/불씨/탄흔 파티클이 트레이서를 따라가는 것처럼 보이게 만듭니다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class KRHitscanTracer : MonoBehaviour
    {
        [Header("외형 (코드가 자동 적용)")]
        [Tooltip("총구 쪽(시작점) 두께")]
        [Min(0.001f)]
        [SerializeField] private float _startWidth = 0.05f;

        [Tooltip("명중 지점 쪽(끝점) 두께. 시작보다 얇게 두면 \"쏘아져 나가는\" 느낌이 강해집니다.")]
        [Min(0f)]
        [SerializeField] private float _endWidth = 0.015f;

        [Header("탄속 / 길이 (무기별 기본값, Play() 인자로 덮어쓸 수 있음)")]
        [Tooltip("트레이서가 화면에서 이동하는 속도(미터/초). 클수록 더 빨리 지나가서 더 짧게 보입니다.")]
        [Min(1f)]
        [SerializeField] private float _visualSpeed = 250f;

        [Tooltip("트레이서 선 자체의 최대 시각적 길이(미터). 0이면 제한 없음.")]
        [Min(0f)]
        [SerializeField] private float _maxVisualLength = 8f;

        [Header("이동 파티클")]
        [Tooltip("트레이서 머리를 따라 이동할 파티클 루트입니다. 비워두면 파티클 추적을 사용하지 않습니다.")]
        [SerializeField] private Transform _movingParticleRoot;

        [Tooltip("이동 파티클 루트 아래의 ParticleSystem입니다. 비워두면 위치 이동만 수행합니다.")]
        [SerializeField] private ParticleSystem _movingParticle;

        [Tooltip("true면 MovingParticleRoot가 트레이서 머리를 따라 start → end로 이동합니다.")]
        [SerializeField] private bool _moveParticleAlongTracer = true;

        [Tooltip("true면 MovingParticleRoot가 이동 방향을 바라보도록 회전합니다.")]
        [SerializeField] private bool _rotateParticleToDirection = true;

        [Tooltip("true면 트레이서가 사라질 때 파티클을 분리하고 StopEmitting만 하여 남은 입자가 자연스럽게 사라지게 합니다.")]
        [SerializeField] private bool _detachParticleOnFinish = false;

        [Tooltip("Detach Particle On Finish가 true일 때, 분리된 파티클 루트를 제거하기까지의 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _detachedParticleDestroyDelay = 1.0f;

        private LineRenderer _line;

        private Vector3 _startPos;
        private Vector3 _actualEndPos;
        private Vector3 _direction;

        private Color _baseColor;

        private float _totalDistance;
        private float _visibleLength;
        private float _travelDuration;
        private float _elapsed;

        private bool _isPlaying;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            ConfigureLineRenderer();

            if (_movingParticleRoot != null)
            {
                _movingParticleRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 사각형으로 보이는 문제의 핵심 원인(평면 고정 정렬, 직각 끝처리)을 코드로 강제 보정합니다.
        /// </summary>
        private void ConfigureLineRenderer()
        {
            _line.positionCount = 2;
            _line.alignment = LineAlignment.View;
            _line.numCapVertices = 8;
            _line.numCornerVertices = 4;

            var widthCurve = new AnimationCurve(
                new Keyframe(0f, _startWidth),
                new Keyframe(1f, _endWidth));

            _line.widthCurve = widthCurve;
            _line.useWorldSpace = true;
        }

        /// <summary>
        /// 트레이서를 재생합니다. 외부(KRHitscanWeapon)에서 Instantiate 직후 1회 호출합니다.
        /// </summary>
        /// <param name="start">총구(FirePoint) 월드 좌표</param>
        /// <param name="end">명중 지점 또는 사거리 소진 지점의 월드 좌표</param>
        /// <param name="color">속성별 트레이서 색상</param>
        /// <param name="visualSpeedOverride">0 이하면 인스펙터 기본값(_visualSpeed) 사용</param>
        /// <param name="maxLengthOverride">음수면 인스펙터 기본값(_maxVisualLength) 사용. 0이면 길이 제한 없음</param>
        public void Play(
            Vector3 start,
            Vector3 end,
            Color color,
            float visualSpeedOverride = -1f,
            float maxLengthOverride = -1f)
        {
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
                ConfigureLineRenderer();
            }

            float speed = visualSpeedOverride > 0f ? visualSpeedOverride : _visualSpeed;
            float maxLength = maxLengthOverride >= 0f ? maxLengthOverride : _maxVisualLength;

            _startPos = start;
            _actualEndPos = end;
            _baseColor = color;
            _elapsed = 0f;

            Vector3 delta = _actualEndPos - _startPos;
            _totalDistance = delta.magnitude;

            if (_totalDistance <= 0.001f)
            {
                Destroy(gameObject);
                return;
            }

            _direction = delta / _totalDistance;

            _visibleLength = maxLength > 0f
                ? Mathf.Min(maxLength, _totalDistance)
                : _totalDistance;

            _travelDuration = Mathf.Max(0.02f, _totalDistance / Mathf.Max(1f, speed));

            _line.startColor = _baseColor;
            _line.endColor = _baseColor;

            // 시작 프레임에서는 선이 총구 지점에서 시작하도록 둡니다.
            // 이후 Update에서 총알 머리(head)가 앞으로 나가고,
            // 꼬리(tail)는 visibleLength만큼 뒤따라갑니다.
            _line.SetPosition(0, _startPos);
            _line.SetPosition(1, _startPos);

            SetupMovingParticle();

            _isPlaying = true;
        }

        private void SetupMovingParticle()
        {
            if (_movingParticleRoot == null)
            {
                return;
            }

            _movingParticleRoot.gameObject.SetActive(true);
            _movingParticleRoot.position = _startPos;

            if (_rotateParticleToDirection)
            {
                _movingParticleRoot.rotation = Quaternion.LookRotation(_direction, Vector3.up);
            }

            if (_movingParticle != null)
            {
                _movingParticle.Clear(true);
                _movingParticle.Play(true);
            }
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            _elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(_elapsed / _travelDuration);

            UpdateLine(t);
            UpdateMovingParticle(t);

            if (t >= 1f)
            {
                FinishTracer();
            }
        }

        private void UpdateLine(float t)
        {
            // head는 실제 총알 머리처럼 start → actualEnd로 이동합니다.
            float headDistance = _totalDistance * t;

            // tail은 head보다 visibleLength만큼 뒤에 있습니다.
            // 초반에는 tail이 총구보다 뒤로 갈 수 없으므로 0으로 클램프합니다.
            float tailDistance = Mathf.Max(0f, headDistance - _visibleLength);

            Vector3 tailPos = _startPos + (_direction * tailDistance);
            Vector3 headPos = _startPos + (_direction * headDistance);

            _line.SetPosition(0, tailPos);
            _line.SetPosition(1, headPos);
        }

        private void UpdateMovingParticle(float t)
        {
            if (!_moveParticleAlongTracer || _movingParticleRoot == null)
            {
                return;
            }

            Vector3 headPos = Vector3.Lerp(_startPos, _actualEndPos, t);
            _movingParticleRoot.position = headPos;

            if (_rotateParticleToDirection)
            {
                _movingParticleRoot.rotation = Quaternion.LookRotation(_direction, Vector3.up);
            }
        }

        private void FinishTracer()
        {
            _isPlaying = false;

            if (_movingParticle != null)
            {
                _movingParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (_detachParticleOnFinish && _movingParticleRoot != null)
            {
                _movingParticleRoot.SetParent(null, true);
                Destroy(_movingParticleRoot.gameObject, _detachedParticleDestroyDelay);
            }

            Destroy(gameObject);
        }
    }
}