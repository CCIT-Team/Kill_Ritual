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
    /// LineRenderer를 짧은 시간(기본 0.05초) 동안 그려서 "총알 꼬리"를 흉내 냅니다.
    ///
    /// [왜 기본 LineRenderer는 사각형처럼 보이는가]
    /// LineRenderer의 Alignment 기본값(Transform Z)은 월드 공간에 고정된 평면(리본)을 그립니다.
    /// 카메라가 그 평면을 정면이 아닌 옆/위 각도에서 보면, 얇은 선이 아니라 공중에 떠 있는
    /// 평평한 카드(사각형)처럼 보입니다. 또한 양쪽 끝이 직각으로 잘려 있어(numCapVertices=0)
    /// 더 블록처럼 보입니다. 이 두 가지를 Awake에서 코드로 강제 설정해 해결합니다:
    ///   1. Alignment = View → 항상 카메라를 향해 빌보드 처리되어 "평면이 보이는" 각도가 사라짐
    ///   2. numCapVertices를 높여 끝을 둥글게 처리
    ///   3. Width Curve로 양 끝을 좁게 테이퍼시켜 "쏘아져 나가는 빛줄기" 느낌을 살림
    ///
    /// [프리팹 구성 요구사항]
    ///   1. 빈 GameObject에 LineRenderer 컴포넌트 부착
    ///   2. LineRenderer Material은 발광이 잘 보이는 알파 블렌딩 셰이더 권장 (예: Sprites/Default, Particles/Additive)
    ///      — 머티리얼이 알파 블렌딩을 지원하지 않으면 페이드 아웃이 보이지 않고 끝까지 불투명하게 남습니다.
    ///   3. 이 컴포넌트(KRHitscanTracer)를 같은 GameObject에 부착 — 그 외 Width/Alignment/Caps는 코드가 자동 설정합니다.
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

        private LineRenderer _line;
        private float _duration;
        private float _elapsed;
        private Color _baseColor;
        private Vector3 _startPos;
        private Vector3 _endPos;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
        }

        /// <summary>
        /// 사각형으로 보이는 문제의 핵심 원인 두 가지(평면 고정 정렬, 직각 끝처리)를 코드로 강제
        /// 보정합니다. 인스펙터에서 잘못 설정해도 항상 자연스러운 모양이 나오도록 합니다.
        /// </summary>
        private void ConfigureLineRenderer()
        {
            _line.positionCount = 2;

            // 핵심 수정 ①: 항상 카메라를 향해 빌보드 처리. 이게 빠지면 보는 각도에 따라
            // 월드에 고정된 평면이 그대로 보여서 "사각형 카드"처럼 보입니다.
            _line.alignment = LineAlignment.View;

            // 핵심 수정 ②: 양쪽 끝을 둥글게 처리해 직각으로 잘린 블록 모양을 없앱니다.
            _line.numCapVertices = 8;
            _line.numCornerVertices = 4;

            // 핵심 수정 ③: 시작은 두껍고 끝은 얇은 폭 곡선을 적용해 "빛줄기가 쏘아져 나가는"
            // 느낌을 살립니다. 양 끝 두께가 같으면 평평한 막대처럼 보이기 쉽습니다.
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
        /// <param name="duration">완전히 사라지기까지 걸리는 시간(초)</param>
        /// <param name="color">속성별 트레이서 색상</param>
        public void Play(Vector3 start, Vector3 end, float duration, Color color)
        {
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
                ConfigureLineRenderer();
            }

            _startPos = start;
            _endPos = end;

            _line.SetPosition(0, start);
            _line.SetPosition(1, end);

            _baseColor = color;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;

            _line.startColor = _baseColor;
            _line.endColor = _baseColor;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // 이즈아웃 곡선: 초반에는 거의 그대로 보이다가 후반에 급격히 줄어들게 해
            // "총알이 막 지나간 직후의 잔상"처럼 보이게 합니다 (선형보다 훨씬 자연스럽습니다).
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            // 핵심: 알파만 줄이는 대신, 시작점(총구 쪽)을 끝점(명중 지점) 방향으로 점점 당겨와
            // "꼬리가 목표를 향해 빨려 들어가며 사라지는" 느낌을 만듭니다.
            Vector3 shrinkingStart = Vector3.Lerp(_startPos, _endPos, easedT);
            _line.SetPosition(0, shrinkingStart);
            _line.SetPosition(1, _endPos);

            // 알파도 함께 살짝 줄여 완전히 사라지는 순간이 더 매끄럽게 보이도록 보강합니다.
            float alpha = Mathf.Lerp(1f, 0f, t * t);
            Color faded = _baseColor;
            faded.a = alpha;

            _line.startColor = faded;
            _line.endColor = faded;

            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
