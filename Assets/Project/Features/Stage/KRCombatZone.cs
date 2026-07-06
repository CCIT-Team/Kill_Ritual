// Assets/Project/Features/Stage/KRCombatZone.cs
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Enemies;

namespace KillRitual.Stage
{
    /// <summary>
    /// 전투 구간(웨이브) 트리거 볼륨입니다.
    ///
    /// [동작 방식]
    /// 플레이어가 이 존(Box Collider Trigger)에 들어오면, 존의 범위(Bounds) 안에 있는
    /// KREnemyBase를 전부 "이번 전투 참가자"로 스캔해 등록하고 KRCombatStartEvent를 발행합니다.
    /// 이후 일정 주기로 참가자들이 전부 사망(IsDead)했는지 체크하고, 전멸하면
    /// KRCombatEndEvent를 발행합니다.
    ///
    /// 이 컴포넌트는 "전투 시작/종료" 신호만 제공하는 최소 범위 구현입니다.
    /// 적 스폰, 웨이브 순서 제어(1웨이브 클리어 후 2웨이브 소환 등)는 범위에 포함하지 않았습니다.
    /// 나중에 진짜 스테이지/웨이브 매니저를 만들 때는 이 이벤트를 그대로 재사용하거나,
    /// 이 컴포넌트를 웨이브 매니저의 하위 유닛으로 흡수시키면 됩니다.
    ///
    /// [설정 방법]
    /// 1. 전투를 시작하고 싶은 구역에 빈 GameObject를 만들고 이름을 "CombatZone" 등으로 지정
    /// 2. Box Collider 추가 후 Is Trigger = true
    /// 3. 이 컴포넌트 추가
    /// 4. Box Collider 크기/위치를 전투가 벌어질 구역 전체를 덮도록 배치
    ///    (그 안에 미리 배치된 적들이 전투 참가자로 스캔됩니다. 존 밖에 있는 적은 무시됩니다)
    /// 5. Layer는 ExecutionZone 또는 Ignore Raycast 권장 (플레이어/적과 물리 충돌 없이 트리거만 감지)
    /// 6. _enemyLayerMask는 적이 실제로 사용하는 레이어(예: Damgeable)로 설정
    /// </summary>
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

        /// <summary>
        /// 존 범위 안의 살아있는 KREnemyBase를 스캔해 전투 참가자로 등록하고,
        /// 한 마리 이상 발견되면 KRCombatStartEvent를 발행합니다.
        /// 참가자가 없으면(이미 다 잡았거나 빈 구역이면) 아무 것도 하지 않습니다.
        /// </summary>
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
