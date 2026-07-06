using System.Collections;
using UnityEngine;

namespace KillRitual.Weapons.Visual
{
    /// <summary>
    /// ���� �и� �ð� ���� ���� ������Ʈ.
    /// 
    /// Idle ��� ���� Parry ��Ǹ� 1ȸ ����ϴ� ����.
    /// ���ÿ��� ������Ʈ�� ���� �ʰ� Renderer�� ����,
    /// Animator�� ��Ȱ��ȭ�ؼ� ���� ������ �ڵ� ���ø��� ���´�.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KRMuryeongVisual : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator _animator;

        [Header("State Name")]
        [SerializeField] private string _parryStateName = "Muryeong_Parry";

        [Header("Visual Renderers")]
        [SerializeField] private Renderer[] _renderers;

        [Header("Particles")]
        [SerializeField] private ParticleSystem[] _particles;

        [Header("Auto Hide")]
        [SerializeField] private bool _hideOnAwake = true;
        [SerializeField] private bool _autoHideByTime = true;
        [SerializeField] private float _autoHideDelay = 0.45f;

        [Header("Animator Control")]
        [SerializeField] private bool _disableAnimatorWhileHidden = true;

        private int _parryStateHash;
        private Coroutine _hideRoutine;

        /// <summary>
        /// [2026-07-06 추가] 무령(방울)이 실제로 다시 숨겨지는 시점(HideNow() 호출 시점)에 발행됩니다.
        /// 자동 타이머(HideAfterDelay)와 애니메이션 이벤트(AnimEvent_HideMuryeong) 두 경로 모두
        /// HideNow()를 거치므로, 이 이벤트 하나로 두 경로를 전부 커버합니다.
        /// KRMuryeongController가 이 이벤트를 구독해서, 무령 사용 중 숨겨뒀던 원래 무기 손을
        /// 정확히 무령이 사라지는 순간에 다시 보여줍니다.
        /// </summary>
        public event System.Action OnHidden;

        private void Awake()
        {
            CacheReferences();

            _parryStateHash = Animator.StringToHash(_parryStateName);

            StopAndClearParticles();

            if (_hideOnAwake)
                SetVisible(false);

            if (_disableAnimatorWhileHidden && _animator != null)
                _animator.enabled = false;
        }

        private void OnEnable()
        {
            StopAndClearParticles();

            if (_hideOnAwake)
                SetVisible(false);

            if (_disableAnimatorWhileHidden && _animator != null)
                _animator.enabled = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheReferences();
        }
#endif

        private void CacheReferences()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);

            if (_particles == null || _particles.Length == 0)
                _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void PlayParry()
        {
            if (_animator == null)
            {
                Debug.LogWarning("[KRMuryeongVisual] Animator�� �����ϴ�.", this);
                return;
            }

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            StopAndClearParticles();
            SetVisible(true);

            if (_disableAnimatorWhileHidden)
                _animator.enabled = true;

            // Trigger�� ���� �ʰ� Parry ���¸� 0�����Ӻ��� ���� ����Ѵ�.
            // �� ����� Idle ���� 1ȸ�� �������� ���⿡�� �� �������̴�.
            _animator.Play(_parryStateHash, 0, 0f);
            _animator.Update(0f);

            if (_autoHideByTime)
                _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(_autoHideDelay);

            HideNow();
            _hideRoutine = null;
        }

        private void HideNow()
        {
            StopAndClearParticles();
            SetVisible(false);

            if (_disableAnimatorWhileHidden && _animator != null)
                _animator.enabled = false;

            // [2026-07-06 추가] 실제로 숨겨진 시점에 구독자(KRMuryeongController 등)에게 알립니다.
            OnHidden?.Invoke();
        }

        public void SetVisible(bool visible)
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = visible;
            }
        }

        private void StopAndClearParticles()
        {
            if (_particles == null)
                return;

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem ps = _particles[i];

                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
            }
        }

        /// <summary>
        /// �и� �ִϸ��̼� �� ����Ʈ�� ���;� �ϴ� �����ӿ� Animation Event�� ȣ��.
        /// </summary>
        public void AnimEvent_PlayShockwave()
        {
            if (_particles == null)
                return;

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem ps = _particles[i];

                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.Play(true);
            }
        }

        /// <summary>
        /// �и� �ִϸ��̼� ������ �����ӿ� Animation Event�� ȣ�� ����.
        /// Auto Hide Delay�� ���� ��쿡�� �ʼ� �ƴ�.
        /// </summary>
        public void AnimEvent_HideMuryeong()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            HideNow();
        }
    }
}