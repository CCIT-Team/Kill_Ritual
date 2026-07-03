// Assets/Project/Scripts/03_Weapons/KRExplosionFlash.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 간단한 폭발 플래시 시각효과.
    /// 구체가 빠르게 커지면서 투명해지다가 사라집니다.
    ///
    /// Built-in Render Pipeline 기준.
    /// URP 셰이더를 찾지 않습니다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class KRExplosionFlash : MonoBehaviour
    {
        [Header("Lifetime")]
        [Tooltip("완전히 사라지기까지 걸리는 시간(초)")]
        [Min(0.05f)]
        [SerializeField] private float _duration = 0.35f;

        [Header("Scale")]
        [Tooltip("시작 시 구체의 로컬 스케일")]
        [Min(0.01f)]
        [SerializeField] private float _startScale = 0.1f;

        [Tooltip("최대로 커졌을 때의 로컬 스케일")]
        [Min(0.01f)]
        [SerializeField] private float _endScale = 5f;

        [Header("Color")]
        [Tooltip("플래시 색상")]
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.1f, 1f);

        [Tooltip("시작 투명도. 1이면 불투명, 0이면 완전 투명")]
        [Range(0f, 1f)]
        [SerializeField] private float _startAlpha = 0.45f;

        [Tooltip("끝 투명도. 보통 0으로 둡니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _endAlpha = 0f;

        private Renderer _renderer;
        private Material _runtimeMaterial;
        private float _elapsed;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            Shader shader = Shader.Find("Standard")
                          ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                          ?? Shader.Find("Sprites/Default");

            _runtimeMaterial = new Material(shader);
            ConfigureBuiltInTransparency(_runtimeMaterial);

            Color startColor = _color;
            startColor.a = _startAlpha;

            _runtimeMaterial.color = startColor;
            _renderer.material = _runtimeMaterial;

            transform.localScale = Vector3.one * _startScale;
        }

        private static void ConfigureBuiltInTransparency(Material mat)
        {
            if (mat == null) return;

            mat.SetOverrideTag("RenderType", "Transparent");

            // Built-in Standard Shader의 Rendering Mode = Transparent
            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 3f);

            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);

            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            float scaleT = 1f - Mathf.Pow(1f - t, 2f);
            float scale = Mathf.Lerp(_startScale, _endScale, scaleT);
            transform.localScale = Vector3.one * scale;

            Color currentColor = _color;

            // 처음부터 반투명하게 시작해서 0으로 사라짐
            currentColor.a = Mathf.Lerp(_startAlpha, _endAlpha, t * t);

            if (_runtimeMaterial != null)
                _runtimeMaterial.color = currentColor;

            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }
    }
}