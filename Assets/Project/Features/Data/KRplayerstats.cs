// Assets/Project/Scripts/02_Player/Combat/KRPlayerStats.cs
using UnityEngine;
using KillRitual.Data;

namespace KillRitual.Player.Combat
{
    public sealed class KRPlayerStats : MonoBehaviour
    {
        [Header("Data 레이어 참조 (ScriptableObject)")]
        [Tooltip("MaxHealth, AttackMultiplier, AttackSpeedMultiplier의 기본값을 제공하는 데이터 에셋. " +
                 "비워두면 모두 안전한 기본값(체력 100, 배율 1)으로 동작합니다.")]
        [SerializeField] private KRCharacterStatsSO _characterStats;

        private float _currentHealth;
        private float _maxHealth;

        public float CurrentHealth => _currentHealth;

        public float MaxHealth => _maxHealth;

        public float HealthRatio => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;

        public bool IsDead => _currentHealth <= 0f;

        public float AttackMultiplier => _characterStats != null ? _characterStats.AttackMultiplier : 1f;

        public float AttackSpeedMultiplier => _characterStats != null ? Mathf.Max(0.01f, _characterStats.AttackSpeedMultiplier) : 1f;

        private void Awake()
        {
            _maxHealth = _characterStats != null ? _characterStats.MaxHealth : 100f;
            _currentHealth = _maxHealth;
        }

        public void ApplyDamage(float amount)
        {
            if (IsDead) return;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        }

        public void Heal(float amount)
        {
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        }

        public void HealByPercent(float percentOfMax)
        {
            Heal(_maxHealth * (percentOfMax / 100f));
        }

        public void Kill()
        {
            _currentHealth = 0f;
        }

        public void SetMaxHealth(float newMaxHealth, bool keepRatio = true)
        {
            float ratio = HealthRatio;
            _maxHealth = Mathf.Max(1f, newMaxHealth);
            _currentHealth = keepRatio ? _maxHealth * ratio : Mathf.Min(_currentHealth, _maxHealth);
        }
    }
}