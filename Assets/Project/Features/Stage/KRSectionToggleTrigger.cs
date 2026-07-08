using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillRitual.Core.Events;
using KillRitual.Core.Managers;
using KillRitual.Enemies;

namespace KillRitual.Stage
{
    /// <summary>
    /// 박스 트리거를 밟으면 지정한 섹션 Empty들을 켜고 끄는 단순 트리거.
    ///
    /// 사용 방식:
    /// - 빈 오브젝트 생성
    /// - BoxCollider 추가
    /// - Is Trigger 체크
    /// - 이 스크립트 부착
    /// - Objects To Enable / Objects To Disable에 섹션 Empty 연결
    ///
    /// 섹션 예:
    /// - Section_Forest
    /// - Section_Cave
    /// - Section_Village
    /// - Section_Palace
    /// - Section_Boss
    ///
    /// [2026-07-07 추가 — 전투 시작/종료 연동]
    /// "전투 시작/종료를 environment 섹션 기준으로 판단하고 싶다"는 요청에 따라, 이미 있던
    /// KRCombatZone(별도 박스 콜라이더 + 전멸 체크 폴링)을 또 만들지 않고, 이 섹션 트리거
    /// 하나에 옵션으로 통합했습니다. _linkCombat을 켠 섹션만 아래 동작이 적용되고, 꺼두면
    /// (기본값) 기존 동작 그대로입니다 — 기존에 배치된 섹션 트리거들은 영향 없습니다.
    ///
    /// 동작: 트리거 진입 시 _objectsToEnable의 자식 KREnemyBase들을 스캔해 전투 참가자로
    /// 등록하고 KRCombatStartEvent를 발행합니다(참가자가 1마리 이상일 때만). 이후 "참가자가
    /// 전부 죽었고(모두 IsDead) + 플레이어가 이 트리거 밖으로 나감" 두 조건을 동시에 만족하는
    /// 순간에만 KRCombatEndEvent를 발행합니다(둘 중 하나만으로는 발행 안 함 — 요청하신
    /// "적이 모두 죽고 섹션을 벗어나면" 조건 그대로). 이 때문에 _linkCombat이 켜진 섹션은
    /// OnTriggerExit을 감지해야 해서, _disableTriggerAfterUse로 오브젝트 자체를 꺼버리는 시점을
    /// "전투 종료 이벤트를 발행한 뒤"로 늦춥니다(그 전까지는 콜라이더가 켜져 있어야 Exit이 잡힘).
    /// </summary>
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
        [Tooltip("켜면 이 섹션 진입/이탈을 전투 시작/종료 기준으로 사용합니다. " +
                 "끄면(기본값) 이 트리거는 예전처럼 섹션 오브젝트 on/off만 담당합니다.")]
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

        // [2026-07-07 추가] 전투 연동(_linkCombat) 상태입니다. _objectsToEnable 자식들 중
        // 살아있는 KREnemyBase를 담아두고, 전멸 여부를 OnTriggerExit 시점에 확인하는 데 씁니다.
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

            // [2026-07-07 추가 - 디버깅용] 섹션 트리거 진입 자체를 콘솔에 남깁니다.
            if (_linkCombat)
                Debug.Log($"[KRSectionToggleTrigger] {name}: 섹션 진입 감지 (전투 연동 ON)");

            // [2026-07-07 추가] 전투 연동이 켜진 섹션이면, 시각 오브젝트 on/off와 별개로
            // 즉시 전투 참가자를 스캔해 KRCombatStartEvent를 발행합니다(연출 코루틴이 프레임에
            // 걸쳐 나눠 처리되는 것과 무관하게 "전투 시작"은 진입 즉시로 취급).
            if (_linkCombat)
                TryStartCombat();

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(ToggleRoutine());
        }

        /// <summary>
        /// [2026-07-07 추가] _objectsToEnable의 자식(비활성 포함) KREnemyBase 중 살아있는
        /// 대상을 전투 참가자로 등록하고, 1마리 이상이면 KRCombatStartEvent를 발행합니다.
        /// (KRCombatZone.TryStartCombat()과 같은 규칙 — 빈 섹션이면 아무 것도 하지 않음.)
        /// </summary>
        private void TryStartCombat()
        {
            // [2026-07-07 추가] _triggerOnce가 꺼져 있어 OnTriggerEnter가 여러 번 불려도,
            // 이미 전투가 진행 중이면 참가자 목록을 다시 스캔하거나 시작 이벤트를 중복 발행하지 않습니다.
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
                // [2026-07-07 추가 - 디버깅용]
                Debug.Log($"[KRSectionToggleTrigger] {name}: 전투 참가자 없음 → 전투 시작 안 함");
                return;
            }

            _combatActive = true;
            KRManagers.Event.Publish(new KRCombatStartEvent(_combatParticipants.Count));

            // [2026-07-07 추가 - 디버깅용]
            Debug.Log($"[KRSectionToggleTrigger] {name}: ▶ 전투 시작 (참가자 {_combatParticipants.Count}명) - KRCombatStartEvent 발행");
        }

        /// <summary>
        /// [2026-07-07 추가] 요청하신 "적이 모두 죽고 섹션을 벗어나면" 조건 그대로 —
        /// 전투가 진행 중이고, 참가자가 전부 죽었을 때만 플레이어의 트리거 이탈을
        /// "전투 종료"로 인정합니다. 아직 살아있는 참가자가 있으면 나가도 아무 일도 없습니다
        /// (다시 들어와서 마저 처치하거나, 그냥 두고 떠나는 것도 자유입니다 — 종료 이벤트만 안 뜸).
        /// </summary>
        private void OnTriggerExit(Collider other)
        {
            if (!_linkCombat || !_combatActive) return;
            if (!other.CompareTag(_playerTag)) return;

            // [2026-07-07 추가 - 디버깅용] 이탈 시점에 전멸 여부를 "폴링(확인)"하는 순간입니다.
            Debug.Log($"[KRSectionToggleTrigger] {name}: 섹션 이탈 감지 → 전멸 여부 확인 중...");

            if (!AllCombatParticipantsDead())
            {
                Debug.Log($"[KRSectionToggleTrigger] {name}: 아직 생존자 있음 → 전투 종료 보류");
                return;
            }

            _combatActive = false;
            KRManagers.Event.Publish(new KRCombatEndEvent());

            // [2026-07-07 추가 - 디버깅용]
            Debug.Log($"[KRSectionToggleTrigger] {name}: ■ 전투 종료 (전멸 확인 + 섹션 이탈) - KRCombatEndEvent 발행");

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

            // [2026-07-07 변경] 전투 연동 섹션은 여기서 바로 트리거를 끄지 않습니다.
            // OnTriggerExit에서 "전투 종료" 판정까지 마친 뒤에 꺼야 하기 때문입니다
            // (연동 안 하는 섹션은 기존 그대로 여기서 바로 꺼짐).
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