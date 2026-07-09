// Assets/Project/Features/Enemies/BossSupplyEnemyLink.cs
using UnityEngine;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 보스전 중 소환된 보급 몬스터가 죽으면 KRBossSupplySpawner에 알립니다.
    /// </summary>
    public class BossSupplyEnemyLink : MonoBehaviour
    {
        private KRBossSupplySpawner _owner;
        private bool _notified;

        public void Init(KRBossSupplySpawner owner)
        {
            _owner = owner;
            _notified = false;
        }

        public void Die()
        {
            NotifyDead();
        }

        private void NotifyDead()
        {
            if (_notified) return;
            _notified = true;

            _owner?.NotifySupplyEnemyDied();
        }

        private void OnDisable()
        {
            if (_owner == null) return;
            NotifyDead();
        }

        private void OnDestroy()
        {
            if (_owner == null) return;
            NotifyDead();
        }
    }
}