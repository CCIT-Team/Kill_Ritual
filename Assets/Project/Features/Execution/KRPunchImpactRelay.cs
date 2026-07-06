// Assets/Project/Features/Execution/KRPunchImpactRelay.cs
using UnityEngine;

namespace KillRitual.Player.Combat
{
    /// <summary>
    /// PunchHand 오브젝트에 붙는 아주 작은 중계(릴레이) 컴포넌트입니다.
    ///
    /// [왜 필요한가]
    /// 유니티 애니메이션 이벤트는 그 클립을 재생 중인 Animator가 "붙어있는 바로 그 GameObject"의
    /// 컴포넌트만 호출할 수 있고, 부모/자식 오브젝트까지 자동으로 찾아가지는 않습니다.
    /// Punch.anim을 재생하는 Animator는 Player가 아니라 자식 오브젝트인 PunchHand에 붙어 있고,
    /// 실제 처치/히트스톱 로직(KRAbsorptionSystem)은 Player 오브젝트에 있어서 애니메이션 이벤트가
    /// 직접 호출할 수 없습니다. 그래서 PunchHand에 이 스크립트를 붙여 이벤트를 받은 뒤,
    /// 부모 계층의 KRAbsorptionSystem으로 그대로 전달(중계)만 합니다. 로직은 전혀 없습니다.
    ///
    /// [연결 방법]
    /// 1. PunchHand 오브젝트(Animator가 있는 바로 그 오브젝트)에 이 컴포넌트를 추가합니다.
    /// 2. Punch.anim의 타격 프레임(현재 0.25초 지점)에 있는 애니메이션 이벤트의 Function을
    ///    "OnPunchImpact"로 지정합니다(이미 지정되어 있다면 그대로 두면 됩니다).
    /// </summary>
    public sealed class KRPunchImpactRelay : MonoBehaviour
    {
        private KRAbsorptionSystem _absorptionSystem;

        private void Awake()
        {
            _absorptionSystem = GetComponentInParent<KRAbsorptionSystem>();

            if (_absorptionSystem == null)
            {
                Debug.LogWarning(
                    "[KRPunchImpactRelay] 부모 계층에서 KRAbsorptionSystem을 찾지 못했습니다. " +
                    "Punch.anim의 타격 애니메이션 이벤트가 아무 동작도 하지 않습니다.");
            }
        }

        /// <summary>Punch.anim의 타격 프레임에서 애니메이션 이벤트로 호출됩니다.</summary>
        public void OnPunchImpact()
        {
            _absorptionSystem?.NotifyPunchImpact();
        }
    }
}
