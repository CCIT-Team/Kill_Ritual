// Assets/Project/Features/Weapons/KRExplosionFlash.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    [RequireComponent(typeof(Renderer))]
    public sealed class KRExplosionFlash : MonoBehaviour
    {
        [Tooltip("완전히 사라지기까지 걸리는 시간(초)")]
        [Min(0.05f)]
        [SerializeField] private float _duration = 0.35f;

        [Tooltip("시작 시 구체의 로컬 스케일")]
        [Min(0.01f)]
        [SerializeField] private float _startScale = 0.1f;

        [Tooltip("최대로 커졌을 때의 로컬 스케일")]
        [Min(0.01f)]
        [SerializeField] private float _endScale = 5f;

        [Tooltip("플래시 색상")]
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.1f);

        private Renderer _renderer;
        private Material _runtimeMaterial;
        private float _elapsed;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            // Built-in 렌더 파이프라인 기준으로 Standard 셰이더만 탐색하며, 충돌을 일으킬 수 있는 URP 셰이더는 제외합니다.
            Shader shader = Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[KRExplosionFlash] 셰이더를 찾지 못했습니다.");
                return;
            }

            _runtimeMaterial = new Material(shader);
            ConfigureTransparency(_runtimeMaterial);
            _runtimeMaterial.color = _color;
            _renderer.material = _runtimeMaterial;

            transform.localScale = Vector3.one * _startScale;
        }

        private static void ConfigureTransparency(Material mat)
        {
            // Built-in Standard 셰이더 투명 모드(0=Opaque, 1=Cutout, 2=Fade, 3=Transparent)입니다.
            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 3f);

            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);

            // Standard 셰이더 투명도 키워드 — 셋 다 정확히 설정해야 합니다.
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // 이즈아웃: 초반에 빠르게 커지다가 점점 커지는 속도가 줄어듭니다.
            float scaleT = 1f - Mathf.Pow(1f - t, 2f);
            float scale = Mathf.Lerp(_startScale, _endScale, scaleT);
            transform.localScale = Vector3.one * scale;

            // 후반에 급격히 투명해집니다.
            Color c = _color;
            c.a = Mathf.Lerp(1f, 0f, t * t);
            _runtimeMaterial.color = c;

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }
    }
}