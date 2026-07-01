using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;            // UI의 Image, Text를 다루기 위해 필요합니다.
using KillRitual.Core.Damage;
using KillRitual.Core.Interfaces;

namespace KillRitual.Player
{
    /// <summary>
    /// 플레이어의 체력과 피격 반응을 담당합니다.
    ///   1. 체력 관리 (몬스터가 이 컴포넌트로 데미지를 보내면 체력을 깎습니다)
    ///   2. 체력바 UI 갱신 (유니티에서 만든 Image의 fillAmount를 체력 비율에 맞춰 조절)
    ///   3. 피격 시 화면 빨간 효과 (선택: 유니티에서 만든 빨간 Image의 투명도를 잠깐 올림)
    ///   4. 체력이 0이 되면 게임오버 화면 호출
    ///
    /// [체력바를 코드로 그리지 않고 UI로 만든 이유]
    /// 이전에는 코드(OnGUI)로 화면에 직접 막대를 그렸지만, 그러면 팀원이 위치나 색을
    /// 유니티 화면에서 조절할 수 없었습니다. 이제는 유니티에서 UI 오브젝트(Image)를 직접 만들고,
    /// 이 스크립트는 그 Image를 "체력만큼 채우는" 역할만 합니다. 위치·색·크기는 전부
    /// 유니티 화면에서 드래그와 클릭으로 조절할 수 있습니다.
    ///
    /// [연결 방법 요약] 유니티에서 Image 하나를 체력바로 만들고, 그 Image를 아래 _healthBarFill
    /// 슬롯에 끌어다 넣으면 됩니다. (자세한 절차는 대화 설명 참고)
    /// </summary>
    public sealed class KRPlayerDamageFeedback : MonoBehaviour, IDamageable
    {
        [Header("체력")]
        [Tooltip("플레이어의 최대 체력입니다.")]
        [Min(1f)]
        [SerializeField] private float _maxHealth = 100f;

        [Header("체력바 UI 연결")]
        [Tooltip("체력에 따라 채워질 막대 Image입니다. 유니티에서 만든 체력바(초록 막대) Image를 여기에 넣으세요. " +
                 "이 Image의 Image Type은 'Filled'여야 fillAmount로 조절됩니다.")]
        [SerializeField] private Image _healthBarFill;

        [Tooltip("체력 숫자를 표시할 Text(선택). 없으면 비워두세요. 예: '72 / 100'")]
        [SerializeField] private Text _healthText;

        [Header("게임오버 연결")]
        [Tooltip("게임오버 화면을 그리는 KRGameOverUI 컴포넌트. 비워두면 자동으로 씬에서 찾습니다.")]
        [SerializeField] private KRGameOverUI _gameOverUI;

        [Header("피격 화면 효과 (선택)")]
        [Tooltip("피격 시 잠깐 빨갛게 보일 전체화면 Image(선택). 유니티에서 반투명 빨간 Image를 만들어 넣으면 " +
                 "맞을 때마다 잠깐 나타났다 사라집니다. 없으면 비워두세요.")]
        [SerializeField] private Image _damageOverlay;

        [Tooltip("피격 효과가 사라지는 속도. 클수록 빨리 옅어집니다.")]
        [Min(0.1f)]
        [SerializeField] private float _fadeSpeed = 3f;

        // 현재 체력. 인스펙터에 노출하지 않고 내부에서만 관리합니다.
        private float _health;

        // 피격 효과의 현재 진하기(0~1). 맞으면 1이 되고 시간이 지나며 0으로 줄어듭니다.
        private float _overlayAlpha;

        // IDamageable 구현
        public bool IsDead => _health <= 0f;
        public bool IsGroggy => false; // 플레이어는 그로기 개념을 쓰지 않습니다.
        public Vector3 Position => transform.position;

        // 외부에서 체력을 읽고 싶을 때 사용할 수 있는 공개 프로퍼티(디버그/다른 UI용).
        public float CurrentHealth => _health;
        public float MaxHealth => _maxHealth;

        private void Awake()
        {
            _health = _maxHealth;

            if (_gameOverUI == null)
            {
                _gameOverUI = FindObjectOfType<KRGameOverUI>();
            }

            UpdateHealthBar();   // 시작할 때 체력바를 가득 찬 상태로 맞춥니다.
            HideOverlayInstantly();
        }

        private void Update()
        {
            // 피격 빨간 효과가 켜져 있으면 시간이 지나며 서서히 옅어지게 합니다.
            if (_damageOverlay != null && _overlayAlpha > 0f)
            {
                _overlayAlpha = Mathf.Max(0f, _overlayAlpha - _fadeSpeed * Time.deltaTime);
                SetOverlayAlpha(_overlayAlpha);
            }
        }

        /// <summary>몬스터가 데미지를 줄 때 호출됩니다.</summary>
        public void TakeDamage(KRDamageContext context)
        {
            if (IsDead)
            {
                return;
            }

            _health = Mathf.Max(0f, _health - context.DamageAmount);

            UpdateHealthBar();

            // 피격 효과를 최대로 켭니다(이후 Update에서 서서히 옅어집니다).
            if (_damageOverlay != null)
            {
                _overlayAlpha = 1f;
                SetOverlayAlpha(1f);
            }

            if (_health <= 0f)
            {
                TriggerGameOver();
            }
        }

        public void Execute()
        {
            _health = 0f;
            UpdateHealthBar();
            TriggerGameOver();
        }
        /// <summary>처형 보상 등 외부에서 체력을 회복시킬 때 호출합니다.</summary>
        public void Heal(float amount)
        {
            if (IsDead) return;

            _health = Mathf.Min(_maxHealth, _health + amount);
            UpdateHealthBar(); // HP바도 즉시 갱신
        }

        /// <summary>체력바 Image의 채움 정도와 숫자 텍스트를 현재 체력에 맞춰 갱신합니다.</summary>
        private void UpdateHealthBar()
        {
            float ratio = _maxHealth > 0f ? Mathf.Clamp01(_health / _maxHealth) : 0f;

            if (_healthBarFill != null)
            {
                // fillAmount는 0(빈 칸)~1(가득 참) 사이 값입니다. 체력 비율을 그대로 넣습니다.
                _healthBarFill.fillAmount = ratio;
            }

            if (_healthText != null)
            {
                _healthText.text = Mathf.CeilToInt(_health) + " / " + Mathf.CeilToInt(_maxHealth);
            }
        }

        private void TriggerGameOver()
        {
            if (_gameOverUI != null)
            {
                _gameOverUI.ShowGameOver();
            }
            else
            {
                Debug.LogWarning("[KRPlayerDamageFeedback] KRGameOverUI가 연결되지 않아 게임오버 화면을 띄울 수 없습니다.");
            }
        }

        // ------------------------------------------------------------------
        // 피격 빨간 화면 효과 헬퍼 (선택 기능 — _damageOverlay가 연결됐을 때만 동작)
        // ------------------------------------------------------------------

        /// <summary>빨간 오버레이 Image의 투명도(알파)를 설정합니다.</summary>
        private void SetOverlayAlpha(float alpha)
        {
            if (_damageOverlay == null)
            {
                return;
            }

            Color c = _damageOverlay.color;
            c.a = alpha;
            _damageOverlay.color = c;
        }

        /// <summary>시작 시 빨간 효과를 완전히 투명하게(안 보이게) 만듭니다.</summary>
        private void HideOverlayInstantly()
        {
            _overlayAlpha = 0f;
            SetOverlayAlpha(0f);
        }
    }
}