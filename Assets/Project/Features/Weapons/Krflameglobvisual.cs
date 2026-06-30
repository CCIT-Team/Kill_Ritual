// Assets/Project/Scripts/03_Weapons/KRFlameGlobVisual.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 즉발 레이캐스트 무기(특히 화염방사기 느낌의 토(土) 스컬크러셔)에서, 가는 트레이서 선 대신
    /// "작은 불덩이가 짧은 시간 동안 실제로 날아가는 것처럼" 보이게 만드는 시각효과입니다.
    ///
    /// 데미지는 이미 발사 순간 레이캐스트로 즉시 적용되어 있으므로, 이 컴포넌트는 순수하게
    /// 눈속임용 비행 애니메이션만 담당합니다(반응성 있는 즉발 판정 + 화염방사기 같은 비주얼을
    /// 동시에 얻기 위한 트릭입니다).
    ///
    /// [동작 방식]
    ///   시작점(총구)에서 끝점(명중/사거리 끝)까지 _travelDuration(기본 0.12초) 동안 이동하면서
    ///   크기가 점점 줄어들고(불씨가 흩어지는 느낌) 색이 옅어집니다. 여러 발이 짧은 간격으로
    ///   연달아 나가면(KRRampingHitscanWeapon의 펠릿 시차와 결합) 화염이 뿜어져 나가는 듯한
    ///   인상을 줍니다.
    ///
    /// [프리팹 구성 요구사항]
    ///   1. Hierarchy 우클릭 → 3D Object → Sphere 생성
    ///   2. Sphere Collider 컴포넌트는 제거 (순수 시각효과)
    ///   3. 이 컴포넌트(KRFlameGlobVisual)를 부착 — 머티리얼은 런타임에 자동 생성되므로
    ///      별도의 머티리얼/셰이더 설정이 필요 없습니다.
    ///   4. 프리팹으로 만들어 KRRampingHitscanWeapon의 "Flame Glob Prefab" 슬롯에 연결
    /// </summary>
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

        /// <summary>
        /// 불덩이 비행을 재생합니다. 외부(KRRampingHitscanWeapon)에서 Instantiate 직후 호출합니다.
        /// </summary>
        /// <param name="start">총구(FirePoint) 월드 좌표</param>
        /// <param name="end">명중 지점 또는 사거리 끝 월드 좌표 (이미 데미지가 적용된 지점)</param>
        /// <param name="color">화염방사기 색상 (보통 주황~빨강 계열)</param>
        /// <param name="travelDuration">총구에서 끝점까지 날아가는 데 걸리는 시간(초). 0 이하면 인스펙터 기본값을 사용합니다.</param>
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