using UnityEngine;

namespace KillRitual.Enemies
{
    /// <summary>
    /// 그로기 상태 진입/해제 시 오브젝트 테두리 색상을 켜고 끄는 컴포넌트입니다.
    /// KREnemyBase와 같은 오브젝트에 붙이고, KREnemyBase.EnterGroggy()/ExitGroggy()에서
    /// SetOutline(true/false)를 호출하면 됩니다.
    ///
    /// [셰이더 설정]
    /// 적 오브젝트의 머티리얼이 KillRitual/Outline 셰이더를 사용해야 합니다.
    /// Project창에서 머티리얼을 선택 후 Shader를 KillRitual/Outline으로 변경하세요.
    /// </summary>
    public sealed class KRGroggyOutline : MonoBehaviour
    {
        [Header("테두리 설정")]
        [Tooltip("그로기 상태일 때 표시할 테두리 색상.")]
        [SerializeField] private Color _outlineColor = new Color(1f, 0.5f, 0f, 1f);

        [Tooltip("테두리 두께. 오브젝트 크기에 따라 조절하세요.")]
        [Range(0f, 0.1f)]
        [SerializeField] private float _outlineWidth = 0.02f;

        private static readonly int s_outlineColorId   = Shader.PropertyToID("_OutlineColor");
        private static readonly int s_outlineWidthId   = Shader.PropertyToID("_OutlineWidth");
        private static readonly int s_outlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

        private Renderer   _renderer;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _mpb      = new MaterialPropertyBlock();

            // 시작 시 테두리를 끈 상태로 초기화합니다.
            SetOutline(false);
        }

        /// <summary>
        /// 테두리를 켜거나 끕니다.
        /// KREnemyBase.EnterGroggy()에서 SetOutline(true),
        /// ExitGroggy() / EnterDead()에서 SetOutline(false)를 호출하세요.
        /// </summary>
        public void SetOutline(bool enabled)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(s_outlineColorId,     _outlineColor);
            _mpb.SetFloat(s_outlineWidthId,     _outlineWidth);
            _mpb.SetFloat(s_outlineEnabledId,   enabled ? 1f : 0f);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
