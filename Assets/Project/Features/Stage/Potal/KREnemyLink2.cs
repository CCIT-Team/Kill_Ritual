// Assets/Project/Features/CombatZones/ArenaEnemyLink.cs
using UnityEngine;

namespace KillRitual.CombatZones
{
    /// <summary>
    /// 전투 구역에 등록된 적이 죽었을 때 WaveCombatZone에 알리는 연결 컴포넌트입니다.
    /// 실제 사망 판정은 적 체력/AI 쪽에서 발생하고,
    /// 이 컴포넌트는 카운트 감소 알림만 담당합니다.
    /// </summary>
    public class ArenaEnemyLink : MonoBehaviour
    {
        private WaveCombatZone _owner;
        private bool _isSkippable;
        private bool _isSupplyEnemy;
        private bool _notified;

        /// <summary>일반 웨이브 몬스터(초기 배치 / 트래시 보충) 등록용.</summary>
        public void Init(WaveCombatZone owner, bool isSkippable)
        {
            _owner = owner;
            _isSkippable = isSkippable;
            _isSupplyEnemy = false;
            _notified = false;
        }

        /// <summary>
        /// 체력/탄약 부족 시 긴급 소환되는 보급용 몬스터 등록용.
        /// 웨이브 클리어 판정(핵심/잡몹 카운트)에는 영향을 주지 않습니다.
        /// </summary>
        public void InitAsSupplyEnemy(WaveCombatZone owner)
        {
            _owner = owner;
            _isSupplyEnemy = true;
            _notified = false;
        }

        /// <summary>
        /// 적 사망 시 기존 코드에서 호출하던 함수가 있다면 이 함수를 연결하면 됩니다.
        /// </summary>
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