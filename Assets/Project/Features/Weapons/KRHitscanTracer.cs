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
    /// [동작 방식 - 거리 비례 + 즉시 소멸]
    /// 기존에는 거리와 무관하게 고정된 시간(duration) 동안 늘 같은 길이로 보였는데, 이제는
    /// 시작점~끝점 사이 거리를 "탄속"으로 나눈 시간만큼만 보이고, 끝점(충돌 지점)에 도달하는
    /// 즉시 더 이상의 잔상 없이 바로 파괴됩니다. 가까운 거리에 맞으면 거의 0초 만에, 먼 거리는
    /// 그만큼 더 길게 보여서 실제 탄속처럼 느껴집니다.
    ///
    /// [길이 제한]
    /// MaxVisualLength를 두면, 사거리가 아주 길어도 트레이서 자체의 시각적 길이는 그 값을
    /// 넘지 않습니다(시작점을 끝점 방향으로 당겨서 짧게 표시). 0이면 제한 없이 전체 거리를
    /// 다 그립니다.
    ///
    /// [왜 기본 LineRenderer는 사각형처럼 보이는가]
    /// LineRenderer의 Alignment 기본값(Transform Z)은 월드 공간에 고정된 평면(리본)을 그립니다.
    /// 카메라가 그 평면을 정면이 아닌 옆/위 각도에서 보면, 얇은 선이 아니라 공중에 떠 있는
    /// 평평한 카드(사각형)처럼 보입니다. 이를 Awake에서 코드로 강제 보정합니다(View 정렬 + 둥근 끝 + 폭 테이퍼).
    ///
    /// [프리팹 구성 요구사항]
    ///   1. 빈 GameObject에 LineRenderer 컴포넌트 부착
    ///   2. LineRenderer Material은 발광이 잘 보이는 알파 블렌딩 셰이더 권장 (예: Sprites/Default, Particles/Additive)
    ///   3. 이 컴포넌트(KRHitscanTracer)를 같은 GameObject에 부착
    ///
    /// [사용 패턴] 각 무기 스크립트(KRHitscanWeapon)가 Instantiate 직후 Play()를 호출합니다.
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
        [Tooltip("트레이서가 화면에서 이동하는 속도(미터/초). 클수록 더 빨리 지나가서 더 짧게 보입니다. " +
                 "예: 200이면 10미터 거리는 0.05초 만에 지나가고 사라집니다.")]
        [Min(1f)]
        [SerializeField] private float _visualSpeed = 250f;

        [Tooltip("트레이서 선 자체의 최대 시각적 길이(미터). 0이면 제한 없음(전체 사거리를 다 그림). " +
                 "값을 주면 선이 이 길이를 넘지 않도록 시작점을 끝점 쪽으로 당겨서 그립니다 " +
                 "(예: 사거리가 100미터여도 선은 항상 최대 8미터 길이로만 보임).")]
        [Min(0f)]
        [SerializeField] private float _maxVisualLength = 8f;

        private LineRenderer _line;
        private Vector3 _startPos;
        private Vector3 _endPos;
        private Color _baseColor;
        private float _travelDuration;
        private float _elapsed;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
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
        /// <param name="visualSpeedOverride">0 이하면 인스펙터 기본값(_visualSpeed) 사용. 무기별로 탄속을 다르게 주고 싶을 때 전달합니다.</param>
        /// <param name="maxLengthOverride">음수면 인스펙터 기본값(_maxVisualLength) 사용. 0을 명시적으로 넘기면 길이 제한 없음.</param>
        public void Play(Vector3 start, Vector3 end, Color color, float visualSpeedOverride = -1f, float maxLengthOverride = -1f)
        {
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
                ConfigureLineRenderer();
            }

            float speed = visualSpeedOverride > 0f ? visualSpeedOverride : _visualSpeed;
            float maxLength = maxLengthOverride >= 0f ? maxLengthOverride : _maxVisualLength;

            _startPos = start;
            _endPos = end;
            _baseColor = color;
            _elapsed = 0f;

            float totalDistance = Vector3.Distance(start, end);
            // [거리 비례] 표시 시간을 "거리 ÷ 탄속"으로 계산합니다 — 가까운 거리는 거의 즉시,
            // 먼 거리는 그만큼 길게 보여서 실제 탄속이 있는 것처럼 느껴집니다.
            _travelDuration = Mathf.Max(0.02f, totalDistance / speed);

            // [길이 제한] maxLength가 0보다 크고 전체 거리보다 짧으면, 시작점을 끝점 쪽으로
            // 당겨서 트레이서 선 자체의 길이를 maxLength로 고정합니다.
            Vector3 renderStart = start;
            if (maxLength > 0f && totalDistance > maxLength)
            {
                Vector3 dir = (end - start) / totalDistance;
                renderStart = end - dir * maxLength;
            }

            _line.SetPosition(0, renderStart);
            _line.SetPosition(1, end);
            _line.startColor = _baseColor;
            _line.endColor = _baseColor;

            // 시작점을 별도로 기억해 Update()에서 같은 비율로 따라가게 합니다.
            _startPos = renderStart;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _travelDuration);

            // [핵심 변경] 기존의 "서서히 꼬리가 줄어드는 잔상" 대신, 선 전체(시작~끝)가
            // 한 덩어리로 끝점을 향해 빠르게 이동하다가, 도착하는 즉시(t>=1) 잔상 없이
            // 바로 파괴됩니다 — "충돌하자마자 사라진다"는 요구사항에 맞춘 동작입니다.
            Vector3 currentStart = Vector3.Lerp(_startPos, _endPos, t);

            _line.SetPosition(0, currentStart);
            _line.SetPosition(1, _endPos);

            if (t >= 1f)
            {
                // 도착 즉시 파괴 — 알파 페이드 등 추가 잔상 없이 충돌과 동시에 사라집니다.
                Destroy(gameObject);
            }
        }
    }
}