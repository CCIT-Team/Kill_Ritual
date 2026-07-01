// Assets/Project/Scripts/02_Player/Combat/KRPlayerStats.cs
using UnityEngine;
using KillRitual.Data;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// 플레이어의 인게임 런타임 스탯(체력, 공격 배율, 공격 속도 배율)을 한곳에서 관리하는
    /// 전용 컴포넌트입니다.
    ///
    /// [분리 배경] 기존에는 KRCombatSystem이 KRCharacterStatsSO를 직접 참조하며 체력과
    /// 배율 계산을 함께 떠안고 있었습니다. KRCombatSystem은 본래 "무기 홀더"(무기 전환,
    /// 발사 입력 전달, 처형 판정) 역할에 집중해야 하므로, 스탯 관련 책임을 이 컴포넌트로
    /// 분리했습니다. KRCombatSystem은 IDamageable 계약(TakeDamage/IsDead 등)을 여전히
    /// 구현하지만, 내부적으로는 이 컴포넌트에 위임만 합니다.
    ///
    /// [배치 규칙] KRCombatSystem과 같은 Player 오브젝트(또는 그 부모 계층)에 부착하세요.
    /// KRCombatSystem이 Awake 시 GetComponentInParent로 이 컴포넌트를 자동 탐색합니다.
    ///
    /// [저장/불러오기와는 무관] 이 컴포넌트는 순수 런타임 상태만 들고 있으며, 파일 저장이나
    /// 씬 전환 간 영속성은 다루지 않습니다(필요하다면 01_Core/SaveData의 KRFileManager와
    /// 별도로 연동해야 합니다).
    /// </summary>
    public sealed class KRPlayerStats : MonoBehaviour
    {
        [Header("Data 레이어 참조 (ScriptableObject)")]
        [Tooltip("MaxHealth, AttackMultiplier, AttackSpeedMultiplier의 기본값을 제공하는 데이터 에셋. " +
                 "비워두면 모두 안전한 기본값(체력 100, 배율 1)으로 동작합니다.")]
        [SerializeField] private KRCharacterStatsSO _characterStats;

        private float _currentHealth;
        private float _maxHealth;

        /// <summary>현재 체력.</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>최대 체력. KRCharacterStatsSO 기반이며, SetMaxHealth()로 런타임에 변경할 수도 있습니다.</summary>
        public float MaxHealth => _maxHealth;

        /// <summary>체력 비율(0~1). HP 바 UI 등에 사용합니다.</summary>
        public float HealthRatio => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;

        /// <summary>체력이 0 이하인지 여부.</summary>
        public bool IsDead => _currentHealth <= 0f;

        /// <summary>전역 공격 배율. 모든 무기 데미지에 곱연산으로 적용됩니다.</summary>
        public float AttackMultiplier => _characterStats != null ? _characterStats.AttackMultiplier : 1f;

        /// <summary>전역 공격 속도 배율. 모든 무기 쿨다운에 나눗셈으로 적용됩니다. 0 나누기 사고 방지를 위해 최소값을 보장합니다.</summary>
        public float AttackSpeedMultiplier => _characterStats != null ? Mathf.Max(0.01f, _characterStats.AttackSpeedMultiplier) : 1f;

        private void Awake()
        {
            _maxHealth = _characterStats != null ? _characterStats.MaxHealth : 100f;
            _currentHealth = _maxHealth;
        }

        /// <summary>지정한 양만큼 체력을 깎습니다. 0 이하로는 내려가지 않습니다.</summary>
        public void ApplyDamage(float amount)
        {
            if (IsDead) return;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        }

        /// <summary>지정한 양만큼 체력을 회복합니다. MaxHealth를 넘지 않습니다.</summary>
        public void Heal(float amount)
        {
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        }

        /// <summary>최대 체력 대비 비율(%)만큼 회복합니다. 처형 보상(Absorption) 등에서 사용합니다.</summary>
        public void HealByPercent(float percentOfMax)
        {
            Heal(_maxHealth * (percentOfMax / 100f));
        }

        /// <summary>체력을 0으로 만듭니다(즉사 처리). 처형 등에 사용합니다.</summary>
        public void Kill()
        {
            _currentHealth = 0f;
        }

        /// <summary>
        /// 최대 체력을 런타임에 변경합니다. 버프/장비 등으로 최대 체력이 늘어나는 경우를 위한 확장
        /// 지점입니다. keepRatio가 true면 현재 체력 비율을 유지한 채로 절댓값만 재계산합니다.
        /// </summary>
        public void SetMaxHealth(float newMaxHealth, bool keepRatio = true)
        {
            float ratio = HealthRatio;
            _maxHealth = Mathf.Max(1f, newMaxHealth);
            _currentHealth = keepRatio ? _maxHealth * ratio : Mathf.Min(_currentHealth, _maxHealth);
        }
    }
}