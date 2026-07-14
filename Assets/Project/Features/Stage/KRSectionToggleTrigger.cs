using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Enemies;

namespace KillRitual.Stage
{
    [RequireComponent(typeof(Collider))]
    public sealed class KRSectionToggleTrigger : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField] private string _playerTag = "Player";
        [SerializeField] private bool _triggerOnce = true;
        [SerializeField] private bool _disableTriggerAfterUse = true;

        [Header("Objects To Enable")]
        [Tooltip("트리거를 밟았을 때 켤 섹션 Empty들")]
        [SerializeField] private GameObject[] _objectsToEnable;

        [Header("Objects To Disable")]
        [Tooltip("트리거를 밟았을 때 끌 섹션 Empty들")]
        [SerializeField] private GameObject[] _objectsToDisable;

        [Header("전투 연동 (선택)")]
        [Tooltip("켜면 이 섹션 진입/이탈을 전투 시작/종료 기준으로 쓰고, 끄면(기본값) 섹션 오브젝트 on/off만 담당합니다.")]
        [SerializeField] private bool _linkCombat = false;

        [Tooltip("전투 참가자로 스캔할 적의 레이어. 비워두면(everything) 전부 스캔합니다.")]
        [SerializeField] private LayerMask _enemyLayerMask = ~0;

        [Header("Performance")]
        [Tooltip("켜고 끄는 처리를 한 프레임에 몰아서 하지 않고 나눠서 처리")]
        [SerializeField] private bool _spreadOverFrames = true;

        [Tooltip("오브젝트 하나 처리 후 기다릴 프레임 수. 보통 1이면 충분.")]
        [Min(0)]
        [SerializeField] private int _framesBetweenChanges = 1;

        [Tooltip("먼저 켜고 나중에 끌지 여부. 빈 공간이 보이는 것을 막기 위해 켜는 처리를 먼저 권장.")]
        [SerializeField] private bool _enableBeforeDisable = true;

        private bool _hasTriggered;
        private Coroutine _routine;

        private readonly List<KREnemyBase> _combatParticipants = new List<KREnemyBase>();
        private bool _combatActive;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void Awake()
        {
            Collider col = GetComponent<Collider>();

            if (!col.isTrigger)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggerOnce && _hasTriggered)
                return;

            if (!other.CompareTag(_playerTag))
                return;

            _hasTriggered = true;

            if (_linkCombat)
                TryStartCombat();

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(ToggleRoutine());
        }

        private void TryStartCombat()
        {
            if (_combatActive) return;

            _combatParticipants.Clear();

            if (_objectsToEnable != null)
            {
                foreach (GameObject root in _objectsToEnable)
                {
                    if (root == null) continue;

                    KREnemyBase[] enemies = root.GetComponentsInChildren<KREnemyBase>(includeInactive: true);
                    foreach (KREnemyBase enemy in enemies)
                    {
                        if (enemy == null || enemy.IsDead) continue;
                        if (((1 << enemy.gameObject.layer) & _enemyLayerMask) == 0) continue;
                        if (!_combatParticipants.Contains(enemy))
                            _combatParticipants.Add(enemy);
                    }
                }
            }

            if (_combatParticipants.Count == 0)
            {
                return;
            }

            _combatActive = true;
            KRManagers.Event.Publish(new KRCombatStartEvent(_combatParticipants.Count));

        }

        private void OnTriggerExit(Collider other)
        {
            if (!_linkCombat || !_combatActive) return;
            if (!other.CompareTag(_playerTag)) return;

            if (!AllCombatParticipantsDead())
            {
                return;
            }

            _combatActive = false;
            KRManagers.Event.Publish(new KRCombatEndEvent());

            // 전투 연동 섹션은 종료 이벤트를 발행한 뒤에야 트리거를 꺼도 안전합니다
            // (그 전에 꺼버리면 콜라이더가 비활성화돼 이 OnTriggerExit 자체가 호출되지 않습니다).
            if (_disableTriggerAfterUse)
                gameObject.SetActive(false);
        }

        private bool AllCombatParticipantsDead()
        {
            _combatParticipants.RemoveAll(enemy => enemy == null);

            for (int i = 0; i < _combatParticipants.Count; i++)
            {
                if (!_combatParticipants[i].IsDead) return false;
            }

            return true;
        }

        private IEnumerator ToggleRoutine()
        {
            if (_enableBeforeDisable)
            {
                yield return SetObjectsActiveRoutine(_objectsToEnable, true);
                yield return SetObjectsActiveRoutine(_objectsToDisable, false);
            }
            else
            {
                yield return SetObjectsActiveRoutine(_objectsToDisable, false);
                yield return SetObjectsActiveRoutine(_objectsToEnable, true);
            }

            _routine = null;

            if (_disableTriggerAfterUse && !_linkCombat)
                gameObject.SetActive(false);
        }

        private IEnumerator SetObjectsActiveRoutine(GameObject[] targets, bool active)
        {
            if (targets == null)
                yield break;

            foreach (GameObject target in targets)
            {
                if (target == null)
                    continue;

                if (target.activeSelf != active)
                    target.SetActive(active);

                if (_spreadOverFrames)
                    yield return WaitFrames(_framesBetweenChanges);
            }
        }

        private IEnumerator WaitFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
                yield return null;
        }
    }
}