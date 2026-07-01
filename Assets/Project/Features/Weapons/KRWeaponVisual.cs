using UnityEngine;

namespace KillRitual.Weapons.Visual
{
    [DisallowMultipleComponent]
    public sealed class KRWeaponVisual : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator _animator;

        [Header("Animator Trigger Names")]
        [SerializeField] private string _equipTrigger = "Equip";
        [SerializeField] private string _unequipTrigger = "Unequip";

        [SerializeField] private string _primaryTapTrigger = "Primary_Tap";
        [SerializeField] private string _secondaryTapTrigger = "Secondary_Tap";

        [SerializeField] private string _primaryHoldStartTrigger = "Primary_Hold_Start";
        [SerializeField] private string _primaryHoldEndTrigger = "Primary_Hold_End";

        [SerializeField] private string _secondaryHoldStartTrigger = "Secondary_Hold_Start";
        [SerializeField] private string _secondaryHoldEndTrigger = "Secondary_Hold_End";

        [SerializeField] private string _primaryChargeStartTrigger = "Primary_Charge_Start";
        [SerializeField] private string _primaryChargeReleaseTrigger = "Primary_Charge_Release";
        [SerializeField] private string _primaryChargeCancelTrigger = "Primary_Charge_Cancel";

        [SerializeField] private string _secondaryChargeStartTrigger = "Secondary_Charge_Start";
        [SerializeField] private string _secondaryChargeReleaseTrigger = "Secondary_Charge_Release";
        [SerializeField] private string _secondaryChargeCancelTrigger = "Secondary_Charge_Cancel";

        [SerializeField] private string _chargeRatioFloat = "ChargeRatio";

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        public void PlayEquip()
        {
            SetTriggerSafe(_equipTrigger);
        }

        public void PlayUnequip()
        {
            SetTriggerSafe(_unequipTrigger);
        }

        public void PlayTap(KRAttackSlot slot)
        {
            SetTriggerSafe(slot == KRAttackSlot.Primary
                ? _primaryTapTrigger
                : _secondaryTapTrigger);
        }

        public void PlayHoldStart(KRAttackSlot slot)
        {
            SetTriggerSafe(slot == KRAttackSlot.Primary
                ? _primaryHoldStartTrigger
                : _secondaryHoldStartTrigger);
        }

        public void PlayHoldEnd(KRAttackSlot slot)
        {
            SetTriggerSafe(slot == KRAttackSlot.Primary
                ? _primaryHoldEndTrigger
                : _secondaryHoldEndTrigger);
        }

        public void PlayChargeStart(KRAttackSlot slot)
        {
            SetChargeRatio(0f);

            SetTriggerSafe(slot == KRAttackSlot.Primary
                ? _primaryChargeStartTrigger
                : _secondaryChargeStartTrigger);
        }

        public void UpdateCharge(float ratio)
        {
            SetChargeRatio(ratio);
        }

        public void PlayChargeRelease(KRAttackSlot slot, float ratio)
        {
            SetChargeRatio(ratio);

            SetTriggerSafe(slot == KRAttackSlot.Primary
                ? _primaryChargeReleaseTrigger
                : _secondaryChargeReleaseTrigger);
        }

        public void PlayChargeCancel(KRAttackSlot slot)
        {
            SetTriggerSafe(slot == KRAttackSlot.Primary
                ? _primaryChargeCancelTrigger
                : _secondaryChargeCancelTrigger);
        }

        public void CancelAll()
        {
            // 지금은 Animator/VFX를 강제로 끌 필요 없음.
            // 나중에 Hold bool, Charge bool을 쓰게 되면 여기서 false 처리하면 됨.
        }

        private void SetChargeRatio(float ratio)
        {
            if (_animator == null) return;
            _animator.SetFloat(_chargeRatioFloat, Mathf.Clamp01(ratio));
        }

        private void SetTriggerSafe(string triggerName)
        {
            if (_animator == null) return;
            if (string.IsNullOrWhiteSpace(triggerName)) return;

            _animator.ResetTrigger(triggerName);
            _animator.SetTrigger(triggerName);
        }
    }
}