// Assets/Project/Scripts/03_Weapons/KRFlameGlobVisual.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    [RequireComponent(typeof(Renderer))]
    public sealed class KRFlameGlobVisual : MonoBehaviour
    {
        [Tooltip("총구에서 명중 지점까지 날아가는 데 걸리는 시간(초). 짧을수록 빠른 탄속처럼 보입니다.")]
        [Min(0.01f)]
        [SerializeField] private float _travelDuration = 0.12f;

        [Tooltip("시작 시 불덩이의 로컬 스케일")]
        [Min(0.001f)]
        [SerializeField] private float _startScale = 0.18f;

        [Tooltip("도착 직전 줄어드는 최소 스케일 (완전히 0으로 두면 너무 갑자기 사라져 보일 수 있습니다)")]
        [Min(0f)]
        [SerializeField] private float _endScale = 0.05f;

        private Renderer _renderer;
        private Material _runtimeMaterial;
        private Vector3 _startPos;
        private Vector3 _endPos;
        private Color _color;
        private float _elapsed;
        private bool _isPlaying;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Standard")
                          ?? Shader.Find("Sprites/Default");

            _runtimeMaterial = new Material(shader);
            ConfigureEmissiveTransparent(_runtimeMaterial);
            _renderer.material = _runtimeMaterial;
        }

        public void Play(Vector3 start, Vector3 end, Color color, float travelDuration = -1f)
        {
            if (travelDuration > 0f)
            {
                _travelDuration = travelDuration;
            }

            _startPos = start;
            _endPos = end;
            _color = color;
            _elapsed = 0f;
            _isPlaying = true;

            transform.position = start;
            transform.localScale = Vector3.one * _startScale;

            ApplyColor(1f);
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _travelDuration);

            // 이즈인: 처음엔 천천히, 도착할수록 빠르게 — 발사 순간의 "확 뿜어나가는" 느낌을 줍니다.
            float travelT = t * t;
            transform.position = Vector3.Lerp(_startPos, _endPos, travelT);

            float scale = Mathf.Lerp(_startScale, _endScale, t);
            transform.localScale = Vector3.one * scale;

            ApplyColor(1f - t * 0.6f); // 완전히 0으로 페이드하지 않고 도착 직전까지 어느 정도 불씨가 보이게 유지

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyColor(float alpha)
        {
            Color c = _color;
            c.a = Mathf.Clamp01(alpha);
            _runtimeMaterial.color = c;

            if (_runtimeMaterial.HasProperty("_EmissionColor"))
            {
                _runtimeMaterial.EnableKeyword("_EMISSION");
                _runtimeMaterial.SetColor("_EmissionColor", c * 2f); // 발광감을 주어 불씨처럼 보이게 함
            }
        }

        private static void ConfigureEmissiveTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // URP: 0=Opaque, 1=Transparent

            mat.SetOverrideTag("RenderType", "Transparent");

            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);

            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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