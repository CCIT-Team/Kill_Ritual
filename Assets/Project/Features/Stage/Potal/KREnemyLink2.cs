// Assets/Project/Features/Enemies/ArenaEnemyLink.cs
using UnityEngine;
using KillRitual.CombatZones;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 이 컴포넌트가 붙은 적이 죽으면 소속된 WaveCombatZone에 즉시 알립니다.
    /// KREnemyBase.EnterDead()가 despawnDelay 이전에 자동으로 Die()를 호출합니다.
    /// </summary>
    public class ArenaEnemyLink : MonoBehaviour
    {
        private WaveCombatZone _zone;
        private bool _isSkippable;
        private bool _reported;

        public void Init(WaveCombatZone zone, bool isSkippable)
        {
            _zone = zone;
            _isSkippable = isSkippable;
            _reported = false;
        }

        public void Die()
        {
            if (_reported) return;
            _reported = true;

            _zone?.NotifyEnemyDied(_isSkippable);
        }
    }
}