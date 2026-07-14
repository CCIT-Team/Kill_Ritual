// Assets/Project/Features/Stage/KRCombatZone.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Enemies;

namespace KillRitual.Stage
{
    [RequireComponent(typeof(Collider))]
    public sealed class KRCombatZone : MonoBehaviour
    {
        [Tooltip("전투 참가자로 스캔할 적의 레이어. 적이 사용하는 레이어(예: Damgeable)만 포함하세요.")]
        [SerializeField] private LayerMask _enemyLayerMask = ~0;

        [Tooltip("전투 종료(전멸) 판정을 얼마나 자주 체크할지(초). 매 프레임 체크할 필요는 없습니다.")]
        [Min(0.1f)]
        [SerializeField] private float _checkInterval = 0.5f;

        // 한 프레임에 여러 KRCombatZone이 동시에 발동하지 않는다는 전제로 버퍼를 공유합니다.
        // (KRCombatSystem._aimRaycastBuffer와 동일한 패턴)
        private static readonly Collider[] _overlapBuffer = new Collider[64];

        private readonly List<KREnemyBase> _participants = new List<KREnemyBase>();
        private Collider _zoneCollider;
        private bool _combatActive;
        private float _nextCheckTime;

        private void Awake()
        {
            _zoneCollider = GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_combatActive) return;

            // 플레이어인지 확인 (KRCombatSystem을 보유한 오브젝트만 전투 시작 트리거로 인정)
            if (other.GetComponentInParent<KillRitual.Player.Combat.KRCombatSystem>() == null) return;

            TryStartCombat();
        }

        private void TryStartCombat()
        {
            _participants.Clear();

            Bounds bounds = _zoneCollider.bounds;
            int count = Physics.OverlapBoxNonAlloc(
                bounds.center, bounds.extents, _overlapBuffer, Quaternion.identity, _enemyLayerMask);

            for (int i = 0; i < count; i++)
            {
                KREnemyBase enemy = _overlapBuffer[i].GetComponentInParent<KREnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (!_participants.Contains(enemy))
                    _participants.Add(enemy);
            }

            if (_participants.Count == 0) return;

            _combatActive = true;
            _nextCheckTime = Time.time + _checkInterval;

            KRManagers.Event.Publish(new KRCombatStartEvent(_participants.Count));
        }

        private void Update()
        {
            if (!_combatActive) return;
            if (Time.time < _nextCheckTime) return;

            _nextCheckTime = Time.time + _checkInterval;
            CheckForCombatEnd();
        }

        private void CheckForCombatEnd()
        {
            // 씬 언로드 등으로 파괴된 참가자를 정리합니다.
            _participants.RemoveAll(enemy => enemy == null);

            for (int i = 0; i < _participants.Count; i++)
            {
                if (!_participants[i].IsDead) return; // 아직 살아있는 참가자가 있으면 계속 진행 중
            }

            _combatActive = false;
            KRManagers.Event.Publish(new KRCombatEndEvent());
        }

        private void OnDrawGizmosSelected()
        {
            Collider col = _zoneCollider != null ? _zoneCollider : GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = _combatActive
                ? new Color(1f, 0.3f, 0.2f, 0.25f)
                : new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}
