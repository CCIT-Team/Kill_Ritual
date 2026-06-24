// Assets/Project/Scripts/03_Weapons/KRWeaponBase.cs
using UnityEngine;
using KillRitual.Core.Damage;
using KillRitual.Player.Combat;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 모든 원거리 무기 스크립트의 공통 기반 클래스입니다.
    ///
    /// [아키텍처 변경] 기존의 "KRElementDataSO 1개 + KRCombatSystem 중앙 디스패처" 방식을
    /// 폐기하고, 무기마다 자신의 GameObject에 직접 붙는 컴포넌트 방식으로 전환했습니다.
    /// 무기 1개 = 컴포넌트 1개이며, 같은 발사 방식(Hitscan/Projectile)을 공유하는 무기는
    /// 같은 C# 클래스를 재사용하되 인스펙터 값만 다르게 설정합니다.
    ///
    /// [배치 규칙] 이 컴포넌트(또는 자식 클래스)가 붙은 GameObject는 반드시 KRCombatSystem이
    /// 붙어있는 Player 오브젝트의 자식 계층 안에 있어야 합니다. Awake 시 GetComponentInParent로
    /// KRCombatSystem을 찾아 자원 지갑·사거리 마스크·공격 배율 등의 공용 상태를 참조합니다.
    ///
    /// [입력 흐름] KRCombatSystem이 좌/우클릭 상태를 매 프레임 폴링한 뒤, 현재 장착된 무기의
    /// NotifyHeld()(누르는 동안) 또는 NotifyReleased()(뗄 때)를 호출해 줍니다. 무기 스크립트는
    /// 입력을 직접 폴링하지 않고, 오직 이 두 진입점을 통해서만 동작합니다.
    /// </summary>
    public abstract class KRWeaponBase : MonoBehaviour
    {
        [Header("기본 정보")]
        [Tooltip("이 무기의 오행 속성. 자원 지갑에서 어느 속성 자원을 소모할지 결정합니다.")]
        [SerializeField] protected KRDamageType _element = KRDamageType.Fire;

        [Tooltip("디버그 로그/인스펙터 표시용 이름 (예: \"샷건\", \"스컬크러셔\")")]
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

        /// <summary>이 무기가 속한 KRCombatSystem(플레이어 전투 컨트롤러)에 대한 참조. Awake에서 자동 탐색됩니다.</summary>
        protected KRCombatSystem _combatSystem;

        // 무기별로 독립적인 "다음 발사 가능 시각". 무기 1개 = 컴포넌트 1개이므로
        // 별도의 슬롯 인덱스 배열 없이 인스턴스 필드 하나로 자연스럽게 독립 쿨다운이 구현됩니다.
        private float _nextFireReadyTime;

        public KRDamageType Element => _element;

        protected virtual void Awake()
        {
            _combatSystem = GetComponentInParent<KRCombatSystem>();

            if (_combatSystem == null)
            {
                Debug.LogWarning($"[{_weaponName}] 부모 계층에서 KRCombatSystem을 찾지 못했습니다. " +
                                  $"이 무기는 KRCombatSystem이 붙은 Player 오브젝트의 자식이어야 합니다.");
            }
        }

        /// <summary>
        /// 발사 버튼이 눌려있는 동안 매 프레임 호출됩니다.
        /// 기본 구현은 "쿨다운+자원이 허락하면 즉시 발사"하는 일반적인 무기 동작입니다.
        /// 가속 연사(Ramping)나 차징 발사(Charge)가 필요한 무기는 이 메서드를 오버라이드합니다.
        /// </summary>
        public virtual void NotifyHeld()
        {
            TryFireNow();
        }

        /// <summary>
        /// 발사 버튼을 뗀 프레임에 호출됩니다. 기본 무기는 별도 상태가 없어 아무 동작도 하지 않지만,
        /// 가속/충전 상태를 가진 무기는 이 시점에 해당 상태를 초기화합니다.
        /// </summary>
        public virtual void NotifyReleased()
        {
        }

        /// <summary>
        /// 쿨다운과 자원 잔량을 확인한 뒤 통과하면 실제 발사를 실행합니다.
        /// 발사에 성공하면 true, 쿨다운/자원 부족으로 무산되면 false를 반환합니다.
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
            _nextFireReadyTime = Time.time + (GetEffectiveCooldown() / speedMultiplier);

            float finalDamage = _damage * _combatSystem.AttackMultiplier;
            DoFire(finalDamage);
            return true;
        }

        /// <summary>
        /// 실제 적용할 쿨다운 값을 반환합니다. 기본값은 _cooldown 그대로이며,
        /// 가속 연사 무기(KRRampingHitscanWeapon)가 이 메서드를 오버라이드해 동적으로 줄입니다.
        /// </summary>
        protected virtual float GetEffectiveCooldown() => _cooldown;

        /// <summary>
        /// 실제 충돌 판정(레이캐스트 또는 투사체 생성)을 수행합니다. 자식 클래스가 구현합니다.
        /// </summary>
        protected abstract void DoFire(float damage);

        /// <summary>
        /// 무기의 발사 기준점(FirePoint)을 가져옵니다. 플레이 모드와 에디터(기즈모) 양쪽에서
        /// 모두 안전하게 동작하도록, 캐시된 참조가 없으면 즉시 다시 탐색합니다.
        /// </summary>
        protected Transform ResolveFirePoint()
        {
            KRCombatSystem cs = _combatSystem != null ? _combatSystem : GetComponentInParent<KRCombatSystem>();
            return cs != null ? cs.FirePoint : transform;
        }
    }
}
