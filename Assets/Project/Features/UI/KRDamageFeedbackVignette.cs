// Assets/Project/Scripts/UI/KRScreenDamageVignette.cs
using UnityEngine;
using UnityEngine.UI;

namespace KillRitual.UI
{
    /// <summary>
    /// 화면 피격/저체력 비네트 전용 UI 컴포넌트입니다.
    ///
    /// 역할:
    ///   1. 피격 순간에는 강한 빨간 테두리 플래시를 표시합니다.
    ///   2. 체력이 낮을수록 약한 빨간 테두리를 지속 표시합니다.
    ///   3. 치명 체력 이하에서는 약한 펄스를 추가합니다.
    ///   4. Image RectTransform을 현재 화면 전체에 자동으로 맞춥니다.
    ///
    /// 사용 방식:
    ///   - Canvas 아래에 전체 화면 Image를 하나 만듭니다.
    ///   - 해당 Image에 KR_UI_DamageVignette Material을 연결합니다.
    ///   - 이 컴포넌트를 같은 오브젝트에 붙입니다.
    ///   - KRPlayerDamageFeedback의 Screen Damage Vignette 슬롯에 이 컴포넌트를 연결합니다.
    ///
    /// 주의:
    ///   - 스프라이트 테두리 이미지를 쓰지 않습니다.
    ///   - 전체 화면 Image + UI Shader Material로 처리합니다.
    ///   - 런타임에 Material 인스턴스를 만들어서 원본 Material 에셋을 오염시키지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class KRScreenDamageVignette : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int EdgeStartId = Shader.PropertyToID("_EdgeStart");
        private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int VerticalStretchId = Shader.PropertyToID("_VerticalStretch");
        private static readonly int AlphaPowerId = Shader.PropertyToID("_AlphaPower");
        private static readonly int CenterClearRadiusId = Shader.PropertyToID("_CenterClearRadius");
        private static readonly int CenterClearSoftnessId = Shader.PropertyToID("_CenterClearSoftness");

        private const string DamageVignetteShaderName = "KillRitual/UI/DamageVignette";

        [Header("References")]
        [Tooltip("피격/저체력 비네트를 표시할 전체 화면 Image입니다. 비워두면 자기 자신에서 자동으로 찾습니다.")]
        [SerializeField] private Image _overlayImage;

        [Header("Layout")]
        [Tooltip("켜면 이 Image를 항상 현재 Canvas/Screen 전체에 맞춥니다.")]
        [SerializeField] private bool _forceFullScreenImage = true;

        [Tooltip("켜면 실행 중 해상도/게임뷰 크기가 바뀔 때도 다시 전체 화면으로 보정합니다.")]
        [SerializeField] private bool _refitOnRectTransformChange = true;

        [Header("Material")]
        [Tooltip("Image에 Material이 없거나 잘못된 Material이면 DamageVignette Shader로 런타임 Material을 자동 생성합니다.")]
        [SerializeField] private bool _createMaterialIfMissing = true;

        [Header("Color")]
        [Tooltip("피격/저체력 테두리 색상입니다.")]
        [SerializeField] private Color _damageColor = new Color(1f, 0f, 0f, 1f);

        [Header("Hit Flash")]
        [Tooltip("피격 순간 최소 플래시 강도입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _hitFlashMinIntensity = 0.45f;

        [Tooltip("큰 피해를 받았을 때의 최대 플래시 강도입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _hitFlashMaxIntensity = 0.85f;

        [Tooltip("피격 플래시가 사라지는 속도입니다. 클수록 빨리 사라집니다.")]
        [Min(0.1f)]
        [SerializeField] private float _hitFlashFadeSpeed = 5.5f;

        [Header("Low Health")]
        [Tooltip("이 체력 비율 이하부터 저체력 테두리가 표시됩니다. 0.4 = 40%.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowHealthStartRatio = 0.4f;

        [Tooltip("이 체력 비율 이하부터 치명 체력으로 간주합니다. 0.15 = 15%.")]
        [Range(0f, 1f)]
        [SerializeField] private float _criticalHealthRatio = 0.15f;

        [Tooltip("저체력 지속 테두리의 최대 강도입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowHealthMaxIntensity = 0.38f;

        [Tooltip("저체력 강도 변화를 부드럽게 만듭니다.")]
        [SerializeField] private bool _smoothLowHealthRamp = true;

        [Header("Critical Pulse")]
        [Tooltip("치명 체력 이하에서 약한 펄스를 표시합니다.")]
        [SerializeField] private bool _enableCriticalPulse = true;

        [Tooltip("치명 체력 이하에서 펄스가 반복되는 속도입니다.")]
        [Min(0.1f)]
        [SerializeField] private float _criticalPulseSpeed = 2.7f;

        [Tooltip("치명 체력 이하에서 추가되는 펄스 강도입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _criticalPulseAmount = 0.08f;

        [Header("Vignette Shape")]
        [Tooltip("비네트가 시작되는 위치입니다. 낮을수록 중앙 쪽까지 붉어집니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _edgeStart = 0.48f;

        [Tooltip("비네트 번짐 폭입니다. 높을수록 부드럽게 퍼집니다.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _edgeSoftness = 0.45f;

        [Tooltip("세로 비네트 형태 보정입니다.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float _verticalStretch = 0.85f;

        [Tooltip("가장자리 알파 곡선입니다. 높을수록 가장자리 쪽에 더 집중됩니다.")]
        [Range(0.2f, 5f)]
        [SerializeField] private float _alphaPower = 1.35f;

        [Tooltip("중앙 조준 영역을 보호하는 반경입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _centerClearRadius = 0.22f;

        [Tooltip("중앙 보호 영역에서 가장자리로 넘어가는 부드러움입니다.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _centerClearSoftness = 0.35f;

        [Header("Time")]
        [Tooltip("히트스탑/슬로우모션 중에도 UI 페이드가 정상 속도로 진행되게 합니다.")]
        [SerializeField] private bool _useUnscaledTime = true;

        private RectTransform _overlayRect;
        private Material _runtimeMaterial;

        private float _hitFlashIntensity;
        private float _lowHealthIntensity;
        private float _currentHealthRatio = 1f;

        private void Awake()
        {
            CacheReferences();
            ForceFullScreenIfNeeded();
            CreateRuntimeMaterial();
            ApplyStaticMaterialProperties();

            SetHealthRatio(1f);
            SetFinalIntensity(0f);
        }

        private void OnEnable()
        {
            CacheReferences();
            ForceFullScreenIfNeeded();
            ApplyStaticMaterialProperties();
            ApplyIntensity();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_criticalHealthRatio > _lowHealthStartRatio)
            {
                _criticalHealthRatio = _lowHealthStartRatio;
            }

            CacheReferences();
            ForceFullScreenIfNeeded();

            if (Application.isPlaying)
            {
                ApplyStaticMaterialProperties();
                SetHealthRatio(_currentHealthRatio);
                ApplyIntensity();
            }
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_refitOnRectTransformChange)
            {
                return;
            }

            ForceFullScreenIfNeeded();
        }

        private void Update()
        {
            float deltaTime = _useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            if (_hitFlashIntensity > 0f)
            {
                _hitFlashIntensity = Mathf.MoveTowards(
                    _hitFlashIntensity,
                    0f,
                    _hitFlashFadeSpeed * deltaTime
                );
            }

            ApplyIntensity();
        }

        /// <summary>
        /// 체력 비율을 갱신합니다.
        /// 1 = 풀피, 0 = 사망.
        /// </summary>
        public void SetHealthRatio(float ratio)
        {
            _currentHealthRatio = Mathf.Clamp01(ratio);

            if (_currentHealthRatio >= _lowHealthStartRatio)
            {
                _lowHealthIntensity = 0f;
                ApplyIntensity();
                return;
            }

            float t = Mathf.InverseLerp(
                _lowHealthStartRatio,
                _criticalHealthRatio,
                _currentHealthRatio
            );

            t = Mathf.Clamp01(t);

            if (_smoothLowHealthRamp)
            {
                // Smoothstep.
                t = t * t * (3f - 2f * t);
            }

            _lowHealthIntensity = Mathf.Lerp(0f, _lowHealthMaxIntensity, t);
            ApplyIntensity();
        }

        /// <summary>
        /// 피격 순간 플래시를 발생시킵니다.
        /// normalizedDamage는 0~1 기준이며, 클수록 강한 플래시가 나옵니다.
        /// </summary>
        public void Flash(float normalizedDamage)
        {
            normalizedDamage = Mathf.Clamp01(normalizedDamage);

            float targetIntensity = Mathf.Lerp(
                _hitFlashMinIntensity,
                _hitFlashMaxIntensity,
                normalizedDamage
            );

            _hitFlashIntensity = Mathf.Max(_hitFlashIntensity, targetIntensity);
            ApplyIntensity();
        }

        /// <summary>
        /// 화면 효과를 즉시 숨깁니다.
        /// </summary>
        public void HideInstantly()
        {
            _hitFlashIntensity = 0f;
            _lowHealthIntensity = 0f;
            _currentHealthRatio = 1f;
            SetFinalIntensity(0f);
        }

        private void CacheReferences()
        {
            if (_overlayImage == null)
            {
                _overlayImage = GetComponent<Image>();
            }

            if (_overlayImage == null)
            {
                return;
            }

            _overlayImage.raycastTarget = false;
            _overlayRect = _overlayImage.rectTransform;
        }

        private void ForceFullScreenIfNeeded()
        {
            if (!_forceFullScreenImage)
            {
                return;
            }

            ForceFullScreenImage();
        }

        private void ForceFullScreenImage()
        {
            if (_overlayRect == null)
            {
                return;
            }

            _overlayRect.anchorMin = Vector2.zero;
            _overlayRect.anchorMax = Vector2.one;
            _overlayRect.pivot = new Vector2(0.5f, 0.5f);

            _overlayRect.offsetMin = Vector2.zero;
            _overlayRect.offsetMax = Vector2.zero;

            _overlayRect.localScale = Vector3.one;
            _overlayRect.localRotation = Quaternion.identity;
        }

        private void CreateRuntimeMaterial()
        {
            if (_overlayImage == null)
            {
                Debug.LogWarning("[KRScreenDamageVignette] Image를 찾지 못했습니다.");
                return;
            }

            if (_runtimeMaterial != null)
            {
                _overlayImage.material = _runtimeMaterial;
                return;
            }

            Material sourceMaterial = _overlayImage.material;

            bool sourceIsValid = sourceMaterial != null && sourceMaterial.HasProperty(IntensityId);

            if (!sourceIsValid && _createMaterialIfMissing)
            {
                Shader shader = Shader.Find(DamageVignetteShaderName);

                if (shader != null)
                {
                    sourceMaterial = new Material(shader)
                    {
                        name = "KR_UI_DamageVignette_AutoCreated"
                    };

                    sourceIsValid = true;
                }
            }

            if (!sourceIsValid || sourceMaterial == null)
            {
                Debug.LogWarning(
                    "[KRScreenDamageVignette] DamageVignette Material이 연결되지 않았거나 Shader를 찾지 못했습니다. " +
                    "Image에 KillRitual/UI/DamageVignette Shader를 사용하는 Material을 연결하세요."
                );
                return;
            }

            _runtimeMaterial = Instantiate(sourceMaterial);
            _runtimeMaterial.name = $"{sourceMaterial.name}_Runtime";
            _overlayImage.material = _runtimeMaterial;
        }

        private void ApplyStaticMaterialProperties()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _runtimeMaterial.SetColor(TintId, _damageColor);
            _runtimeMaterial.SetFloat(EdgeStartId, _edgeStart);
            _runtimeMaterial.SetFloat(EdgeSoftnessId, _edgeSoftness);
            _runtimeMaterial.SetFloat(VerticalStretchId, _verticalStretch);
            _runtimeMaterial.SetFloat(AlphaPowerId, _alphaPower);
            _runtimeMaterial.SetFloat(CenterClearRadiusId, _centerClearRadius);
            _runtimeMaterial.SetFloat(CenterClearSoftnessId, _centerClearSoftness);
        }

        private void ApplyIntensity()
        {
            float pulseIntensity = GetCriticalPulseIntensity();

            // 피격 순간 플래시는 저체력 지속 경고보다 우선적으로 강하게 보이게 함.
            float finalIntensity = Mathf.Max(
                _hitFlashIntensity,
                _lowHealthIntensity + pulseIntensity
            );

            SetFinalIntensity(finalIntensity);
        }

        private float GetCriticalPulseIntensity()
        {
            if (!_enableCriticalPulse)
            {
                return 0f;
            }

            if (_currentHealthRatio > _criticalHealthRatio)
            {
                return 0f;
            }

            if (_currentHealthRatio <= 0f)
            {
                return 0f;
            }

            float time = _useUnscaledTime
                ? Time.unscaledTime
                : Time.time;

            float pulse = Mathf.Sin(time * _criticalPulseSpeed * Mathf.PI * 2f);
            pulse = pulse * 0.5f + 0.5f;

            return pulse * _criticalPulseAmount;
        }

        private void SetFinalIntensity(float intensity)
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            intensity = Mathf.Clamp01(intensity);
            _runtimeMaterial.SetFloat(IntensityId, intensity);

            if (_overlayImage != null)
            {
                _overlayImage.enabled = intensity > 0.001f;
            }
        }
    }
}