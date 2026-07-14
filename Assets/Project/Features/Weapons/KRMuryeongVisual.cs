using System.Collections;
using UnityEngine;

namespace KillRitual.Weapons.Visual
{
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
                Debug.LogWarning("[KRMuryeongVisual] Animator가 없습니다.", this);
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

            // Trigger 대신 Parry 상태를 0프레임부터 강제 재생해, Idle 상태가 한 프레임이라도 먼저 보이는 것을 막습니다.
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
