using UnityEngine;

namespace KillRitual.Player.Visual
{
    /// <summary>
    /// 1인칭 손/무기 오브젝트 전체를 FirstPersonHands 레이어로 설정한다.
    /// 메인 카메라에서는 제외하고, 손 전용 카메라에서만 렌더링하기 위해 사용.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRFirstPersonHandsLayerSetter : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] private string _handsLayerName = "FirstPersonHands";

        [Header("Options")]
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private bool _includeInactiveChildren = true;

        private void Awake()
        {
            if (_applyOnAwake)
                Apply();
        }

        [ContextMenu("Apply Hands Layer")]
        public void Apply()
        {
            int layer = LayerMask.NameToLayer(_handsLayerName);

            if (layer < 0)
            {
                Debug.LogError(
                    $"[KRFirstPersonHandsLayerSetter] Layer '{_handsLayerName}' not found. " +
                    $"Unity의 Edit Layer에서 먼저 레이어를 만들어야 합니다.",
                    this
                );
                return;
            }

            Transform[] children = GetComponentsInChildren<Transform>(_includeInactiveChildren);

            foreach (Transform child in children)
            {
                child.gameObject.layer = layer;
            }
        }
    }
}