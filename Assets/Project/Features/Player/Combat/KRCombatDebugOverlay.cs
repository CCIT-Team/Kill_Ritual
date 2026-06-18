// Assets/Project/Scripts/02_Player/Combat/KRCombatDebugOverlay.cs
//
// [용도] 개발/포트폴리오 시연용 런타임 디버그 오버레이
// [조건] #if UNITY_EDITOR || DEVELOPMENT_BUILD 블록 전체로 감싸져 있어
//        Player Settings > Development Build 체크가 꺼진 릴리즈 빌드에서는
//        이 파일 전체가 컴파일에서 제외됩니다.
//
// [화면 출력 항목]
//  - 폭발 1회당: 원시 콜라이더 수 / GetComponentInParent 호출 수 / O(n²) 반복 수 / 실제 피격 수 / 중복 건너뜀 수
//  - 누적 세션 통계: 총 폭발 횟수, 평균 콜라이더 수, 평균 O(n²) 반복 수
//  - 현재 자원 지갑 잔량 (화/수/목/토/금 5속성)
//  - 처형 대상 탐지 여부 (그로기 대상 존재 시 강조 표시)
//
// [연결 방법]
//  씬의 아무 오브젝트에나 이 컴포넌트를 추가하면 됩니다.
//  KRCombatSystem 레퍼런스만 인스펙터에 연결하면 자원 지갑 표시도 동작합니다.

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;
using KillRitual.Weapons;
using KillRitual.Player.Combat;
using KillRitual.Core.Damage;

namespace KillRitual.Player.Combat
{
    [DisallowMultipleComponent]
    public sealed class KRCombatDebugOverlay : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("자원 지갑 잔량 표시를 위해 플레이어의 KRCombatSystem을 연결합니다.")]
        [SerializeField] private KRCombatSystem _combatSystem;

        [Header("표시 설정")]
        [SerializeField] private bool _showExplosionStats  = true;
        [SerializeField] private bool _showResourceWallet  = true;
        [SerializeField] private bool _showSessionSummary  = true;

        [Tooltip("OnGUI 패널의 화면 좌측 여백(픽셀)")]
        [SerializeField] private float _panelX = 10f;

        [Tooltip("OnGUI 패널의 화면 상단 여백(픽셀)")]
        [SerializeField] private float _panelY = 10f;

        [Tooltip("패널 너비(픽셀)")]
        [SerializeField] private float _panelWidth = 360f;

        // -----------------------------------------------------------------------
        // 마지막 폭발 통계 (KRPhysicsProjectile.OnExplosionDebugStats 이벤트로 수신)
        // -----------------------------------------------------------------------
        private KRPhysicsProjectile.KRExplosionStats _lastStats;
        private bool _hasStats;

        // -----------------------------------------------------------------------
        // 세션 누적 통계
        // -----------------------------------------------------------------------
        private int   _totalExplosions;
        private float _totalRawColliders;
        private float _totalDeduplicationIterations;
        private float _totalActualHits;

        // -----------------------------------------------------------------------
        // 최근 N회 통계 히스토리 (그래프용 링 버퍼)
        // -----------------------------------------------------------------------
        private const int kHistorySize = 20;
        private readonly float[] _rawCountHistory   = new float[kHistorySize];
        private readonly float[] _dedupHistory      = new float[kHistorySize];
        private int _historyIndex;

        // -----------------------------------------------------------------------
        // OnGUI 스타일 캐시 (매 프레임 new GUIStyle 방지)
        // -----------------------------------------------------------------------
        private GUIStyle _headerStyle;
        private GUIStyle _normalStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _goodStyle;
        private bool     _stylesInitialized;

        private void OnEnable()
        {
            KRPhysicsProjectile.OnExplosionDebugStats += OnExplosionStats;
        }

        private void OnDisable()
        {
            KRPhysicsProjectile.OnExplosionDebugStats -= OnExplosionStats;
        }

        private void OnExplosionStats(KRPhysicsProjectile.KRExplosionStats stats)
        {
            _lastStats  = stats;
            _hasStats   = true;

            _totalExplosions++;
            _totalRawColliders              += stats.RawColliderCount;
            _totalDeduplicationIterations   += stats.DeduplicationIterations;
            _totalActualHits                += stats.ActualHitCount;

            // 링 버퍼에 히스토리 기록
            _rawCountHistory[_historyIndex] = stats.RawColliderCount;
            _dedupHistory[_historyIndex]    = stats.DeduplicationIterations;
            _historyIndex = (_historyIndex + 1) % kHistorySize;
        }

        private void OnGUI()
        {
            InitStyles();

            float lineH  = 20f;
            float padH   = 8f;
            float x      = _panelX;
            float y      = _panelY;
            float w      = _panelWidth;

            // -----------------------------------------------------------------------
            // 반투명 배경 박스 높이 사전 계산
            // -----------------------------------------------------------------------
            int lineCount = 2; // 타이틀 + 여백
            if (_showExplosionStats)  lineCount += _hasStats ? 9 : 3;
            if (_showResourceWallet)  lineCount += 8;
            if (_showSessionSummary && _totalExplosions > 0) lineCount += 6;

            float totalH = lineCount * lineH + padH * 4f;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(x - 4f, y - 4f, w + 8f, totalH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // -----------------------------------------------------------------------
            // 타이틀
            // -----------------------------------------------------------------------
            GUI.Label(new Rect(x, y, w, lineH), "■ Kill Ritual │ Combat Debug Overlay", _headerStyle);
            y += lineH + padH;

            // -----------------------------------------------------------------------
            // 섹션 1: 마지막 폭발 통계
            // -----------------------------------------------------------------------
            if (_showExplosionStats)
            {
                GUI.Label(new Rect(x, y, w, lineH), "[ 광역 폭발 판정 ]", _headerStyle);
                y += lineH;

                if (!_hasStats)
                {
                    GUI.Label(new Rect(x, y, w, lineH), "  — 아직 폭발 없음 (BFG 또는 화(火) 폭발형 발사)", _normalStyle);
                    y += lineH * 2f;
                }
                else
                {
                    // 콜라이더 수
                    GUI.Label(new Rect(x, y, w, lineH),
                        $"  브로드페이즈 통과 콜라이더 수 : {_lastStats.RawColliderCount}개", _normalStyle);
                    y += lineH;

                    // GetComponentInParent 호출 수 — 많을수록 빨간색 경고
                    int lookups = _lastStats.ComponentLookupCount;
                    GUIStyle lookupStyle = lookups > 20 ? _warnStyle : _normalStyle;
                    GUI.Label(new Rect(x, y, w, lineH),
                        $"  GetComponentInParent 호출 수  : {lookups}회", lookupStyle);
                    y += lineH;

                    // O(n²) 반복 횟수 — 핵심 지표, 강조
                    int dedup = _lastStats.DeduplicationIterations;
                    GUIStyle dedupStyle = dedup > 30 ? _warnStyle : (dedup > 10 ? _normalStyle : _goodStyle);
                    GUI.Label(new Rect(x, y, w, lineH),
                        $"  중복 제거 O(n²) 반복 횟수     : {dedup}회  ← 최적화 대상", dedupStyle);
                    y += lineH;

                    // 실제 피격 / 중복 건너뜀
                    GUI.Label(new Rect(x, y, w, lineH),
                        $"  실제 TakeDamage 호출 수       : {_lastStats.ActualHitCount}명", _goodStyle);
                    y += lineH;
                    GUI.Label(new Rect(x, y, w, lineH),
                        $"  중복 콜라이더 건너뜀           : {_lastStats.DuplicateSkipCount}회 (노란선)", _normalStyle);
                    y += lineH;

                    // 미니 바 그래프: 콜라이더 수 vs O(n²) 반복 수
                    y += 4f;
                    DrawMiniBarGraph(x, y, w, "콜라이더", _lastStats.RawColliderCount,
                                              "O(n²) 반복", _lastStats.DeduplicationIterations, 64f);
                    y += lineH + 4f;

                    GUI.Label(new Rect(x, y, w, lineH),
                        "  (씬 뷰: 빨강=폭발 구, 초록=피격 대상, 노랑=중복 스킵)", _normalStyle);
                    y += lineH;
                }
                y += padH;
            }

            // -----------------------------------------------------------------------
            // 섹션 2: 자원 지갑 잔량
            // -----------------------------------------------------------------------
            if (_showResourceWallet)
            {
                GUI.Label(new Rect(x, y, w, lineH), "[ 자원 지갑 (KRResourceWallet) ]", _headerStyle);
                y += lineH;

                if (_combatSystem == null)
                {
                    GUI.Label(new Rect(x, y, w, lineH), "  — KRCombatSystem이 연결되지 않았습니다.", _warnStyle);
                    y += lineH * 5f;
                }
                else
                {
                    DrawResourceBar(x, y, w, lineH, "화(火) Fire  ", _combatSystem.GetResourceRatio(KRDamageType.Fire),  new Color(1f, 0.3f, 0.1f));
                    y += lineH;
                    DrawResourceBar(x, y, w, lineH, "수(水) Water ", _combatSystem.GetResourceRatio(KRDamageType.Water), new Color(0.2f, 0.6f, 1f));
                    y += lineH;
                    DrawResourceBar(x, y, w, lineH, "목(木) Wood  ", _combatSystem.GetResourceRatio(KRDamageType.Wood),  new Color(0.2f, 0.8f, 0.2f));
                    y += lineH;
                    DrawResourceBar(x, y, w, lineH, "토(土) Earth ", _combatSystem.GetResourceRatio(KRDamageType.Earth), new Color(0.8f, 0.65f, 0.2f));
                    y += lineH;
                    DrawResourceBar(x, y, w, lineH, "금(金) Metal ", _combatSystem.GetResourceRatio(KRDamageType.Metal), new Color(0.8f, 0.8f, 0.9f));
                    y += lineH;

                    // 처형 가능 대상 탐지 표시
                    bool hasExecutable = _combatSystem.HasExecutableTargetNearby;
                    GUIStyle execStyle = hasExecutable ? _goodStyle : _normalStyle;
                    string execLabel   = hasExecutable ? "  ▶ 그로기 대상 감지! [E] 처형 가능" : "  — 처형 가능 대상 없음";
                    GUI.Label(new Rect(x, y, w, lineH), execLabel, execStyle);
                    y += lineH;
                }
                y += padH;
            }

            // -----------------------------------------------------------------------
            // 섹션 3: 세션 누적 요약
            // -----------------------------------------------------------------------
            if (_showSessionSummary && _totalExplosions > 0)
            {
                GUI.Label(new Rect(x, y, w, lineH), "[ 세션 누적 요약 ]", _headerStyle);
                y += lineH;

                float avgRaw   = _totalRawColliders            / _totalExplosions;
                float avgDedup = _totalDeduplicationIterations / _totalExplosions;
                float avgHit   = _totalActualHits              / _totalExplosions;

                GUI.Label(new Rect(x, y, w, lineH), $"  총 폭발 횟수                 : {_totalExplosions}회", _normalStyle);
                y += lineH;
                GUI.Label(new Rect(x, y, w, lineH), $"  평균 콜라이더 수             : {avgRaw:F1}개", _normalStyle);
                y += lineH;
                GUI.Label(new Rect(x, y, w, lineH), $"  평균 O(n²) 반복 수          : {avgDedup:F1}회", avgDedup > 20 ? _warnStyle : _normalStyle);
                y += lineH;
                GUI.Label(new Rect(x, y, w, lineH), $"  평균 실제 피격 수            : {avgHit:F1}명", _goodStyle);
                y += lineH;

                // 히스토리 미니 그래프
                DrawHistoryGraph(x, y, w, 30f);
                y += 34f;
            }
        }

        // -----------------------------------------------------------------------
        // 두 값을 나란히 보여주는 미니 바 그래프
        // -----------------------------------------------------------------------
        private void DrawMiniBarGraph(float x, float y, float w,
            string labelA, float valA, string labelB, float valB, float maxVal)
        {
            float barW = (w - 140f) * 0.5f - 4f;
            float barH = 14f;

            GUI.Label(new Rect(x, y, 70f, barH), $"{labelA}: {valA:F0}", _normalStyle);
            float ratioA = Mathf.Clamp01(valA / Mathf.Max(1f, maxVal));
            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(x + 72f, y + 2f, barW * ratioA, barH - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float bx = x + 72f + barW + 8f;
            GUI.Label(new Rect(bx, y, 70f, barH), $"{labelB}: {valB:F0}", valB > valA * 2f ? _warnStyle : _normalStyle);
            float ratioB = Mathf.Clamp01(valB / Mathf.Max(1f, maxVal));
            GUI.color = valB > valA * 2f ? Color.red : Color.yellow;
            GUI.DrawTexture(new Rect(bx + 72f, y + 2f, barW * ratioB, barH - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // -----------------------------------------------------------------------
        // 오행 속성별 자원 잔량 가로 바
        // -----------------------------------------------------------------------
        private void DrawResourceBar(float x, float y, float w, float h,
            string label, float ratio, Color barColor)
        {
            float labelW = 90f;
            float barW   = w - labelW - 40f;

            GUI.Label(new Rect(x, y, labelW, h), $"  {label}", _normalStyle);

            // 배경 (회색)
            GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);
            GUI.DrawTexture(new Rect(x + labelW, y + 3f, barW, h - 6f), Texture2D.whiteTexture);

            // 잔량 바
            GUI.color = ratio < 0.25f ? Color.red : barColor;
            GUI.DrawTexture(new Rect(x + labelW, y + 3f, barW * Mathf.Clamp01(ratio), h - 6f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + labelW + barW + 4f, y, 36f, h), $"{ratio * 100f:F0}%", _normalStyle);
        }

        // -----------------------------------------------------------------------
        // 최근 kHistorySize회 폭발의 콜라이더 수 / O(n²) 반복 수 추이 그래프
        // -----------------------------------------------------------------------
        private void DrawHistoryGraph(float x, float y, float w, float h)
        {
            float maxVal = 1f;
            for (int i = 0; i < kHistorySize; i++)
            {
                maxVal = Mathf.Max(maxVal, _rawCountHistory[i], _dedupHistory[i]);
            }

            float colW = (w - 4f) / kHistorySize;

            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            for (int i = 0; i < kHistorySize; i++)
            {
                int dataIdx = (_historyIndex + i) % kHistorySize;
                float px    = x + colW * i;

                // 콜라이더 수 (초록)
                float rawH = (_rawCountHistory[dataIdx] / maxVal) * (h - 2f);
                GUI.color  = new Color(0.2f, 0.9f, 0.2f, 0.8f);
                GUI.DrawTexture(new Rect(px + 1f, y + h - rawH - 1f, colW * 0.4f, rawH), Texture2D.whiteTexture);

                // O(n²) 반복 수 (빨강)
                float dedH = (_dedupHistory[dataIdx] / maxVal) * (h - 2f);
                GUI.color  = new Color(0.95f, 0.2f, 0.2f, 0.8f);
                GUI.DrawTexture(new Rect(px + colW * 0.5f, y + h - dedH - 1f, colW * 0.4f, dedH), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y - 2f, w * 0.5f, 14f), "  ■초록=콜라이더  ■빨강=O(n²)반복", _normalStyle);
        }

        // -----------------------------------------------------------------------
        // GUIStyle 초기화 (첫 OnGUI 호출 시 1회만 실행)
        // -----------------------------------------------------------------------
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 12,
                normal    = { textColor = new Color(1f, 0.85f, 0.3f) }
            };

            _normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal   = { textColor = Color.white }
            };

            _warnStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 11,
                normal    = { textColor = new Color(1f, 0.35f, 0.2f) }
            };

            _goodStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal   = { textColor = new Color(0.4f, 1f, 0.5f) }
            };
        }
    }
}

#endif
