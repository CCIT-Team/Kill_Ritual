// Assets/Project/Features/Execution/KRPunchImpactRelay.cs
using UnityEngine;

namespace KillRitual.Player.Combat
{
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

        public void OnPunchImpact()
        {
            _absorptionSystem?.NotifyPunchImpact();
        }
    }
}
