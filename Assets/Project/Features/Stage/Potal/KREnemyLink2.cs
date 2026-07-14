// Assets/Project/Features/CombatZones/ArenaEnemyLink.cs
using UnityEngine;

namespace KillRitual.CombatZones
{
    public class ArenaEnemyLink : MonoBehaviour
    {
        private WaveCombatZone _owner;
        private bool _isSkippable;
        private bool _isSupplyEnemy;
        private bool _notified;

        public void Init(WaveCombatZone owner, bool isSkippable)
        {
            _owner = owner;
            _isSkippable = isSkippable;
            _isSupplyEnemy = false;
            _notified = false;
        }

        public void InitAsSupplyEnemy(WaveCombatZone owner)
        {
            _owner = owner;
            _isSupplyEnemy = true;
            _notified = false;
        }

        public void Die()
        {
            NotifyDead();
        }

        public void NotifyDead()
        {
            if (_notified)
                return;

            _notified = true;

            if (_owner == null)
                return;

            if (_isSupplyEnemy)
                _owner.NotifySupplyEnemyDied();
            else
                _owner.NotifyEnemyDied(_isSkippable);
        }

        private void OnDisable()
        {
            // 풀링을 쓰는 적이라면 죽을 때 Destroy가 아니라 SetActive(false)될 수 있습니다.
            // 단, Init 전 비활성화나 구역 외 비활성화는 owner가 없으므로 무시됩니다.
            if (_owner == null)
                return;

            NotifyDead();
        }

        private void OnDestroy()
        {
            if (_owner == null)
                return;

            NotifyDead();
        }
    }
}