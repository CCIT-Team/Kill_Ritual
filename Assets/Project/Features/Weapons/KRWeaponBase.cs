// Assets/Project/Scripts/03_Weapons/KRWeaponBase.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Player.Combat;
using KillRitual.Weapons.Visual;

namespace KillRitual.Weapons
{
    public abstract class KRWeaponBase : MonoBehaviour
    {
        [Header("기본 정보")]
        [Tooltip("이 무기의 오행 속성. 자원 지갑에서 어느 속성 자원을 소모할지 결정합니다.")]
        [SerializeField] protected KRDamageType _element = KRDamageType.Fire;

        [Tooltip("디버그 로그/인스펙터 표시용 이름")]
        [SerializeField] protected string _weaponName = "Weapon";

        [Header("코어 수치")]
        [Tooltip("기본 데미지. KRCombatSystem의 AttackMultiplier가 곱연산됩니다.")]
        [Min(0f)]
        [SerializeField] protected float _damage = 10f;

        [Tooltip("최대 사거리(미터)")]
        [Min(0.1f)]
        [SerializeField] protected float _range = 50f;

        [Tooltip("독립 연사 제한시간(초). 다른 무기로 전환해도 이 쿨다운은 화면 뒤에서 계속 흐릅니다.")]
        [Min(0f)]
        [SerializeField] protected float _cooldown = 0.2f;

        [Tooltip("1회 발사당 소모되는 해당 속성(오행) 공용 자원량")]
        [Min(0f)]
        [SerializeField] protected float _resourceCost = 5f;

        [Header("입력 방식")]
        [Tooltip("Tap = 한 번 누를 때 한 번만 발사. HoldAuto = 누르고 있는 동안 쿨다운마다 계속 발사.")]
        [SerializeField] protected KRAttackInputType _inputType = KRAttackInputType.HoldAuto;

        [Header("시각 피드백")]
        [Tooltip("발사 성공 시 애니메이션/VFX 신호를 받을 컴포넌트입니다.")]
        [SerializeField] protected KRWeaponVisual _visual;

        [Tooltip("이 무기가 발사 성공 시 어떤 공격 슬롯 애니메이션을 재생할지 결정합니다. 좌클릭 무기는 Primary, 우클릭 무기는 Secondary.")]
        [SerializeField] protected KRAttackSlot _visualAttackSlot = KRAttackSlot.Primary;

        [Tooltip("true면 발사 성공 시 KRWeaponVisual에 신호를 보냅니다.")]
        [SerializeField] protected bool _playVisualOnFire = true;

        protected KRCombatSystem _combatSystem;

        private float _nextFireReadyTime;
        private bool _buttonHeld;

        public KRDamageType Element => _element;

        protected bool IsEquipped => _combatSystem != null && _combatSystem.CurrentElement == _element;

        protected virtual void Awake()
        {
            _combatSystem = GetComponentInParent<KRCombatSystem>();

            if (_visual == null)
            {
                _visual = GetComponentInParent<KRWeaponVisual>();
            }

            if (_visual == null)
            {
                _visual = GetComponentInChildren<KRWeaponVisual>();
            }

            if (_combatSystem == null)
            {

            }

            if (_visual == null)
            {

            }
        }

        public virtual void NotifyHeld()
        {
            switch (_inputType)
            {
                case KRAttackInputType.Tap:
                    if (_buttonHeld)
                    {
                        return;
                    }

                    _buttonHeld = true;
                    //Debug.Log($"[{_weaponName}] Tap Held / Visual={_visual}");
                    TryFireNow();
                    break;

                case KRAttackInputType.HoldAuto:
                    if (!_buttonHeld)
                    {
                        _buttonHeld = true;
                        //Debug.Log($"[{_weaponName}] Hold Start / Visual={_visual} / Slot={_visualAttackSlot}");
                        _visual?.PlayHoldStart(_visualAttackSlot);
                    }

                    TryFireNow();
                    break;

                case KRAttackInputType.ChargeRelease:
                    _buttonHeld = true;
                    //Debug.Log($"[{_weaponName}] Charge Held / Visual={_visual}");
                    break;
            }
        }

        public virtual void NotifyReleased()
        {
            if (_inputType == KRAttackInputType.HoldAuto && _buttonHeld)
            {
                Debug.Log($"[{_weaponName}] Hold End / Visual={_visual} / Slot={_visualAttackSlot}");
                _visual?.PlayHoldEnd(_visualAttackSlot);
            }

            _buttonHeld = false;
        }

        public virtual void NotifyCancelled()
        {
            NotifyReleased();
        }

        protected bool TryFireNow()
        {
            if (Time.time < _nextFireReadyTime)
            {
                return false;
            }

            if (_combatSystem == null || !_combatSystem.TryConsumeResource(_element, _resourceCost))
            {
                return false;
            }

            float speedMultiplier = _combatSystem.AttackSpeedMultiplier;
            speedMultiplier = Mathf.Max(0.01f, speedMultiplier);

            _nextFireReadyTime = Time.time + (GetEffectiveCooldown() / speedMultiplier);

            float finalDamage = _damage * _combatSystem.AttackMultiplier;

            DoFire(finalDamage);

            PlayFireVisual();

            return true;
        }

        protected virtual float GetEffectiveCooldown() => _cooldown;

        protected abstract void DoFire(float damage);

        protected virtual void PlayFireVisual()
        {
            if (!_playVisualOnFire || _visual == null)
            {
                return;
            }

            _visual.PlayTap(_visualAttackSlot);
        }

        protected Transform ResolveFirePoint()
        {
            KRCombatSystem cs = _combatSystem != null ? _combatSystem : GetComponentInParent<KRCombatSystem>();
            return cs != null ? cs.FirePoint : transform;
        }
    }
}