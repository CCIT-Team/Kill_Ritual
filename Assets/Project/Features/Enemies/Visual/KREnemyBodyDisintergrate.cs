using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KillRitual.Enemies.Visual
{
    [DisallowMultipleComponent]
    public sealed class KREnemyBodyDisintegrate : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("디졸브를 적용할 렌더러들의 루트입니다. 비워두면 이 오브젝트 아래에서 자동 검색합니다.")]
        [SerializeField] private Transform _renderRoot;

        [Tooltip("ParticleSystemRenderer는 제외합니다. 보통 켜두는 게 맞습니다.")]
        [SerializeField] private bool _ignoreParticleRenderers = true;

        [Header("Shader")]
        [Tooltip("KillRitual/BuiltIn/BodyDisintegrate 셰이더를 연결하세요.")]
        [SerializeField] private Shader _disintegrateShader;

        [Tooltip("시작할 때 대상 Renderer의 Material Shader를 디스인티그레이션 셰이더로 교체합니다.")]
        [SerializeField] private bool _replaceShaderOnAwake = true;

        [Header("Timing")]
        [Tooltip("몸이 완전히 가루처럼 사라지는 데 걸리는 시간.")]
        [Min(0.05f)]
        [SerializeField] private float _duration = 0.65f;

        [Tooltip("디졸브 시작 전 대기 시간. Animation Event로 정확히 제어할 거면 0으로 두세요.")]
        [Min(0f)]
        [SerializeField] private float _startDelay = 0f;

        [Tooltip("완전히 사라진 뒤 Renderer를 꺼서 남은 잔상을 제거합니다.")]
        [SerializeField] private bool _disableRenderersAfterFinish = true;

        [Header("Look")]
        [Range(0f, 1f)]
        [SerializeField] private float _startAmount = 0f;

        [Range(0f, 1f)]
        [SerializeField] private float _endAmount = 1f;

        [SerializeField]
        private AnimationCurve _dissolveCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("가루 경계선 색. 일반 적은 회색/금색, 영혼 계열은 보라/청색 추천.")]
        [SerializeField] private Color _edgeColor = new Color(1f, 0.65f, 0.25f, 1f);

        [Min(0f)]
        [SerializeField] private float _edgeEmission = 2.5f;

        [Min(0.001f)]
        [SerializeField] private float _edgeWidth = 0.06f;

        [Min(0.01f)]
        [SerializeField] private float _noiseScale = 18f;

        [Tooltip("디졸브 경계가 살짝 몸 바깥으로 밀려나가는 정도.")]
        [Min(0f)]
        [SerializeField] private float _normalPush = 0.04f;

        [Tooltip("디졸브 경계가 위로 흩어지는 정도.")]
        [Min(0f)]
        [SerializeField] private float _upPush = 0.08f;

        private static readonly int PropDissolveAmount = Shader.PropertyToID("_DissolveAmount");
        private static readonly int PropEdgeColor = Shader.PropertyToID("_EdgeColor");
        private static readonly int PropEdgeEmission = Shader.PropertyToID("_EdgeEmission");
        private static readonly int PropEdgeWidth = Shader.PropertyToID("_EdgeWidth");
        private static readonly int PropNoiseScale = Shader.PropertyToID("_NoiseScale");
        private static readonly int PropNormalPush = Shader.PropertyToID("_NormalPush");
        private static readonly int PropUpPush = Shader.PropertyToID("_UpPush");

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material> _runtimeMaterials = new List<Material>();

        private Coroutine _routine;
        private bool _played;

        private void Reset()
        {
            _renderRoot = transform;
            _disintegrateShader = Shader.Find("KillRitual/BuiltIn/BodyDisintegrate");
        }

        private void Awake()
        {
            if (_renderRoot == null)
            {
                _renderRoot = transform;
            }

            if (_disintegrateShader == null)
            {
                _disintegrateShader = Shader.Find("KillRitual/BuiltIn/BodyDisintegrate");
            }

            CacheRenderers();
            PrepareRuntimeMaterials();
            ApplyStaticProperties();
            SetDissolveAmount(_startAmount);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeMaterials.Count; i++)
            {
                if (_runtimeMaterials[i] != null)
                {
                    Destroy(_runtimeMaterials[i]);
                }
            }

            _runtimeMaterials.Clear();
        }

        public void AnimEvent_StartBodyDisintegrate()
        {
            Play();
        }

        public void Play()
        {
            if (_played)
            {
                return;
            }

            _played = true;

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(CoDisintegrate());
        }

        public void ResetVisual()
        {
            _played = false;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            SetRenderersEnabled(true);
            ApplyStaticProperties();
            SetDissolveAmount(_startAmount);
        }

        private IEnumerator CoDisintegrate()
        {
            if (_startDelay > 0f)
            {
                yield return new WaitForSeconds(_startDelay);
            }

            SetRenderersEnabled(true);

            float elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / _duration);
                float curvedT = _dissolveCurve != null ? _dissolveCurve.Evaluate(t) : t;

                float amount = Mathf.Lerp(_startAmount, _endAmount, curvedT);
                SetDissolveAmount(amount);

                yield return null;
            }

            SetDissolveAmount(_endAmount);

            if (_disableRenderersAfterFinish)
            {
                SetRenderersEnabled(false);
            }

            _routine = null;
        }

        private void CacheRenderers()
        {
            _renderers.Clear();

            Renderer[] found = _renderRoot.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < found.Length; i++)
            {
                Renderer renderer = found[i];

                if (renderer == null)
                {
                    continue;
                }

                if (_ignoreParticleRenderers && renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                _renderers.Add(renderer);
            }
        }

        private void PrepareRuntimeMaterials()
        {
            _runtimeMaterials.Clear();

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;

                for (int j = 0; j < materials.Length; j++)
                {
                    if (materials[j] == null)
                    {
                        continue;
                    }

                    if (_replaceShaderOnAwake && _disintegrateShader != null)
                    {
                        materials[j].shader = _disintegrateShader;
                    }

                    _runtimeMaterials.Add(materials[j]);
                }

                renderer.materials = materials;
            }
        }

        private void ApplyStaticProperties()
        {
            for (int i = 0; i < _runtimeMaterials.Count; i++)
            {
                Material material = _runtimeMaterials[i];

                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty(PropEdgeColor))
                {
                    material.SetColor(PropEdgeColor, _edgeColor);
                }

                if (material.HasProperty(PropEdgeEmission))
                {
                    material.SetFloat(PropEdgeEmission, _edgeEmission);
                }

                if (material.HasProperty(PropEdgeWidth))
                {
                    material.SetFloat(PropEdgeWidth, _edgeWidth);
                }

                if (material.HasProperty(PropNoiseScale))
                {
                    material.SetFloat(PropNoiseScale, _noiseScale);
                }

                if (material.HasProperty(PropNormalPush))
                {
                    material.SetFloat(PropNormalPush, _normalPush);
                }

                if (material.HasProperty(PropUpPush))
                {
                    material.SetFloat(PropUpPush, _upPush);
                }
            }
        }

        private void SetDissolveAmount(float amount)
        {
            for (int i = 0; i < _runtimeMaterials.Count; i++)
            {
                Material material = _runtimeMaterials[i];

                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty(PropDissolveAmount))
                {
                    material.SetFloat(PropDissolveAmount, amount);
                }
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = enabled;
                }
            }
        }
    }
}