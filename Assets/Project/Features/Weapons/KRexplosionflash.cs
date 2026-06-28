// Assets/Project/Scripts/03_Weapons/KRExplosionFlash.cs
using UnityEngine;

namespace KillRitual.Weapons
{
    /// <summary>
    /// 별도의 파티클 시스템이나 머티리얼 에셋을 미리 만들어두지 않아도 바로 사용할 수 있는
    /// 간단한 폭발 시각효과입니다. 구체가 빠르게 커지면서 투명해지다가 스스로 사라집니다.
    ///
    /// [사용 방법]
    ///   1. Hierarchy 우클릭 → 3D Object → Sphere 생성
    ///   2. 그 Sphere의 Collider 컴포넌트는 제거 (순수 시각효과이므로 충돌 판정이 필요 없음)
    ///   3. 이 컴포넌트(KRExplosionFlash)를 부착
    ///   4. 프리팹으로 만들어서 KRProjectileWeapon의 "Explosion Vfx Prefab" 슬롯에 연결
    ///
    /// 머티리얼은 Awake에서 런타임에 직접 생성하므로, 인스펙터에서 셰이더나 머티리얼을
    /// 따로 설정하지 않아도 알파 블렌딩(투명도 변화)이 정상 동작합니다.
    /// 나중에 실제 파티클 이펙트로 교체하고 싶다면, 이 컴포넌트가 붙은 프리팹을
    /// 파티클 프리팹으로 그대로 바꿔서 같은 슬롯에 연결하면 됩니다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class KRExplosionFlash : MonoBehaviour
    {
        [Tooltip("완전히 사라지기까지 걸리는 시간(초)")]
        [Min(0.05f)]
        [SerializeField] private float _duration = 0.35f;

        [Tooltip("시작 시 구체의 로컬 스케일")]
        [Min(0.01f)]
        [SerializeField] private float _startScale = 0.1f;

        [Tooltip("최대로 커졌을 때의 로컬 스케일. 폭발 반경과 비슷하게 맞추면 자연스럽습니다.")]
        [Min(0.01f)]
        [SerializeField] private float _endScale = 5f;

        [Tooltip("플래시 색상. 화(火)는 주황, 금(金) BFG는 녹색 계열 등 속성에 맞게 프리팹을 따로 만들어두면 좋습니다.")]
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.1f);

        private Renderer _renderer;
        private Material _runtimeMaterial;
        private float _elapsed;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            // 알파 블렌딩이 가능한 셰이더를 프로젝트 렌더 파이프라인에 맞게 순서대로 탐색합니다.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Standard")
                          ?? Shader.Find("Sprites/Default");

            _runtimeMaterial = new Material(shader);
            ConfigureTransparency(_runtimeMaterial);
            _runtimeMaterial.color = _color;
            _renderer.material = _runtimeMaterial;

            transform.localScale = Vector3.one * _startScale;
        }

        /// <summary>
        /// URP Lit / Standard 셰이더 모두에서 투명도가 정상 동작하도록 필요한 키워드와
        /// 블렌드 모드를 설정합니다. 셰이더에 해당 프로퍼티가 없으면 안전하게 건너뜁니다.
        /// </summary>
        private static void ConfigureTransparency(Material mat)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // URP: 0=Opaque, 1=Transparent

            mat.SetOverrideTag("RenderType", "Transparent");

            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);

            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // 이즈아웃: 초반에 빠르게 커지다가 점점 커지는 속도가 줄어듭니다 (폭발 충격파 느낌).
            float scaleT = 1f - Mathf.Pow(1f - t, 2f);
            float scale = Mathf.Lerp(_startScale, _endScale, scaleT);
            transform.localScale = Vector3.one * scale;

            // 후반에 급격히 투명해지도록 알파를 제곱 곡선으로 감쇠시킵니다.
            Color c = _color;
            c.a = Mathf.Lerp(1f, 0f, t * t);
            _runtimeMaterial.color = c;

            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // 런타임에 직접 생성한 머티리얼은 자동으로 정리되지 않으므로 명시적으로 해제합니다.
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }
    }
}