// Assets/Project/Scripts/03_Weapons/KRWeaponBase.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Player.Combat;
using KillRitual.Weapons.Visual;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 모든 원거리 무기 스크립트의 공통 기반 클래스입니다.
    /// KRCombatSystem은 입력만 전달하고, 실제 발사 판정은 각 무기 클래스가 처리합니다.
    /// </summary>
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

        /// <summary>
        /// 이 무기가 현재 실제로 장착된 속성인지 여부입니다.
        /// 줌 무기처럼 Update에서 직접 입력을 읽는 특수 무기가 미장착 상태에서도 동작하지 않게 막는 데 사용합니다.
        /// </summary>
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

        /// <summary>
        /// KRCombatSystem이 버튼을 누르고 있는 동안 매 프레임 호출합니다.
        /// Tap 무기는 1회 클릭 1발, HoldAuto 무기는 누르고 있는 동안 쿨다운마다 발사합니다.
        /// </summary>
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
                    Debug.Log($"[{_weaponName}] Tap Held / Visual={_visual}");
                    TryFireNow();
                    break;

                case KRAttackInputType.HoldAuto:
                    if (!_buttonHeld)
                    {
                        _buttonHeld = true;
                        Debug.Log($"[{_weaponName}] Hold Start / Visual={_visual} / Slot={_visualAttackSlot}");
                        _visual?.PlayHoldStart(_visualAttackSlot);
                    }

                    TryFireNow();
                    break;

                case KRAttackInputType.ChargeRelease:
                    _buttonHeld = true;
                    Debug.Log($"[{_weaponName}] Charge Held / Visual={_visual}");
                    break;
            }
        }

        /// <summary>
        /// 버튼을 뗐을 때 호출됩니다.
        /// Tap 무기는 여기서 다시 발사 가능 상태가 됩니다.
        /// </summary>
        public virtual void NotifyReleased()
        {
            if (_inputType == KRAttackInputType.HoldAuto && _buttonHeld)
            {
                Debug.Log($"[{_weaponName}] Hold End / Visual={_visual} / Slot={_visualAttackSlot}");
                _visual?.PlayHoldEnd(_visualAttackSlot);
            }

            _buttonHeld = false;
        }

        /// <summary>
        /// 무기 전환 등으로 입력이 강제로 취소될 때 호출됩니다.
        /// </summary>
        public virtual void NotifyCancelled()
        {
            NotifyReleased();
        }

        /// <summary>
        /// 쿨다운과 자원 잔량을 확인한 뒤 통과하면 실제 발사를 실행합니다.
        /// 발사 성공 시 true, 쿨다운/자원 부족이면 false를 반환합니다.
        /// </summary>
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

        /// <summary>
        /// 실제 적용할 쿨다운 값을 반환합니다.
        /// KRRampingHitscanWeapon은 이 메서드를 오버라이드해 연사 속도를 가속합니다.
        /// </summary>
        protected virtual float GetEffectiveCooldown() => _cooldown;

        /// <summary>
        /// 실제 충돌 판정 또는 투사체 생성을 수행합니다.
        /// </summary>
        protected abstract void DoFire(float damage);

        /// <summary>
        /// 발사 성공 후 시각 피드백을 재생합니다.
        /// 샷건은 Primary_Tap, 슈퍼샷건은 Secondary_Tap 같은 식으로 연결합니다.
        /// </summary>
        protected virtual void PlayFireVisual()
        {
            if (!_playVisualOnFire || _visual == null)
            {
                return;
            }

            _visual.PlayTap(_visualAttackSlot);
        }

        /// <summary>
        /// 무기의 발사 기준점(FirePoint)을 가져옵니다.
        /// </summary>
        protected Transform ResolveFirePoint()
        {
            KRCombatSystem cs = _combatSystem != null ? _combatSystem : GetComponentInParent<KRCombatSystem>();
            return cs != null ? cs.FirePoint : transform;
        }
    }
}