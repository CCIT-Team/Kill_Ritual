using UnityEngine;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 이 컴포넌트가 붙은 오브젝트(필요 시 자식 포함)에는 그로기 아웃라인이
    /// 절대 표시되지 않도록 강제로 막습니다.
    ///
    /// KRGroggyOutline이 부모에서 Renderer를 긁어모아 아웃라인을 켜더라도,
    /// 셰이더의 _OutlineEnabled 프로퍼티를 매 프레임 0으로 덮어써서
    /// 이 Renderer만큼은 항상 꺼진 상태로 유지시킵니다.
    ///
    /// 사용법: 아웃라인이 생기면 안 되는 파츠(예: 납작한 마스크/아이콘 메시)에
    /// 그대로 붙이면 됩니다. 별도 설정 없이 바로 동작합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRGroggyOutlineDisabler : MonoBehaviour
    {
        [Tooltip("체크하면 이 오브젝트뿐 아니라 자식들의 Renderer까지 전부 아웃라인을 막습니다.")]
        [SerializeField] private bool _includeChildren = false;

        private static readonly int kOutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _renderers = _includeChildren
                ? GetComponentsInChildren<Renderer>(includeInactive: true)
                : new[] { GetComponent<Renderer>() };

            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            ForceDisableOutline();
        }

        // KRGroggyOutline이 자신의 Update/Refresh에서 _OutlineEnabled를 켤 수 있으므로,
        // 스크립트 실행 순서와 무관하게 "항상 마지막에" 덮어쓰기 위해 LateUpdate를 씁니다.
        private void LateUpdate()
        {
            ForceDisableOutline();
        }

        private void ForceDisableOutline()
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(kOutlineEnabledId, 0f);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}