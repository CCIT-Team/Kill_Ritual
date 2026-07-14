// Assets/Project/Scripts/03_Weapons/Visual/KRWeaponVisual.cs
using UnityEngine;

namespace KillRitual.Weapons.Visual
{
    [DisallowMultipleComponent]
    public sealed class KRWeaponVisual : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator _animator;

        [Header("State Names")]
        [Tooltip("전환 취소/퀵스왑 시 이전 무기를 정리할 때 재생할 기본 대기 상태 이름입니다.")]
        [SerializeField] private string _idleStateName = "Idle";

        [Tooltip("새 무기를 꺼낼 때 즉시 재생할 장착 상태 이름입니다.")]
        [SerializeField] private string _equipStateName = "Equip";

        [Header("Trigger Names")]
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
                _animator = GetComponentInChildren<Animator>(true);
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
            ClearAllTriggers();
        }

        public void ClearAllTriggers()
        {
            if (_animator == null)
            {
                return;
            }

            ResetTriggerIfValid(_equipTrigger);
            ResetTriggerIfValid(_unequipTrigger);

            ResetTriggerIfValid(_primaryTapTrigger);
            ResetTriggerIfValid(_secondaryTapTrigger);

            ResetTriggerIfValid(_primaryHoldStartTrigger);
            ResetTriggerIfValid(_primaryHoldEndTrigger);
            ResetTriggerIfValid(_secondaryHoldStartTrigger);
            ResetTriggerIfValid(_secondaryHoldEndTrigger);

            ResetTriggerIfValid(_primaryChargeStartTrigger);
            ResetTriggerIfValid(_primaryChargeReleaseTrigger);
            ResetTriggerIfValid(_primaryChargeCancelTrigger);

            ResetTriggerIfValid(_secondaryChargeStartTrigger);
            ResetTriggerIfValid(_secondaryChargeReleaseTrigger);
            ResetTriggerIfValid(_secondaryChargeCancelTrigger);
        }

        public void PlayIdleImmediately()
        {
            if (_animator == null)
            {
                return;
            }

            ClearAllTriggers();
            TryPlayStateImmediately(_idleStateName);
        }

        public void PlayEquipImmediately()
        {
            if (_animator == null)
            {
                return;
            }

            ClearAllTriggers();

            bool played = TryPlayStateImmediately(_equipStateName);

            // Equip 상태 이름이 없거나 Animator 구조가 Trigger 기반일 때를 위한 폴백.
            if (!played)
            {
                PlayEquip();
            }
        }

        private void SetChargeRatio(float ratio)
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetFloat(_chargeRatioFloat, Mathf.Clamp01(ratio));
        }

        private void SetTriggerSafe(string triggerName)
        {
            if (_animator == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            _animator.ResetTrigger(triggerName);
            _animator.SetTrigger(triggerName);
        }

        private void ResetTriggerIfValid(string triggerName)
        {
            if (_animator == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            _animator.ResetTrigger(triggerName);
        }

        private bool TryPlayStateImmediately(string stateName)
        {
            if (_animator == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            int shortHash = Animator.StringToHash(stateName);
            int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");

            if (_animator.HasState(0, shortHash))
            {
                _animator.Play(shortHash, 0, 0f);
                _animator.Update(0f);
                return true;
            }

            if (_animator.HasState(0, fullPathHash))
            {
                _animator.Play(fullPathHash, 0, 0f);
                _animator.Update(0f);
                return true;
            }

            // Unity 프로젝트마다 HasState가 shortName/fullPathName을 다르게 잡는 경우가 있어서
            // 마지막으로 문자열 Play를 시도합니다. 실패해도 예외는 나지 않고 상태만 유지됩니다.
            _animator.Play(stateName, 0, 0f);
            _animator.Update(0f);

            return true;
        }
    }
}