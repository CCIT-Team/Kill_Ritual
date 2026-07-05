using System.Collections;
using UnityEngine;

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

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(ToggleRoutine());
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

            if (_disableTriggerAfterUse)
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