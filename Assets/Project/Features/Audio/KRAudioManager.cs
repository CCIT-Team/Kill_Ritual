// Assets/Project/Scripts/01_Core/Audio/KRAudioManager.cs
using UnityEngine;
using UnityEngine.Audio;

namespace KillRitual.Core.Audio
{
    [DisallowMultipleComponent]
    public sealed class KRAudioManager : MonoBehaviour
    {
        public static KRAudioManager Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        [Header("Mixer")]
        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _uiGroup;

        [Header("Sources")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _uiSource;

        [Header("SFX Pool")]
        [Min(1)]
        [SerializeField] private int _sfxPoolSize = 32;

        [SerializeField] private float _defaultMinDistance = 1f;
        [SerializeField] private float _defaultMaxDistance = 35f;

        private AudioSource[] _sfxPool;
        private int _sfxPoolCursor;

        private const string MasterVolumeParameter = "MasterVolume";
        private const string BGMVolumeParameter = "BGMVolume";
        private const string SFXVolumeParameter = "SFXVolume";
        private const string UIVolumeParameter = "UIVolume";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSources();
            InitializeSFXPool();
        }

        private void InitializeSources()
        {
            if (_bgmSource == null)
                _bgmSource = CreateChildSource("BGM Source", _bgmGroup, true);

            if (_uiSource == null)
                _uiSource = CreateChildSource("UI Source", _uiGroup, false);

            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.outputAudioMixerGroup = _bgmGroup;

            _uiSource.playOnAwake = false;
            _uiSource.loop = false;
            _uiSource.spatialBlend = 0f;
            _uiSource.outputAudioMixerGroup = _uiGroup;
        }

        private void InitializeSFXPool()
        {
            _sfxPoolSize = Mathf.Max(1, _sfxPoolSize);
            _sfxPool = new AudioSource[_sfxPoolSize];

            for (int i = 0; i < _sfxPoolSize; i++)
            {
                AudioSource source = CreateChildSource($"SFX Source {i:00}", _sfxGroup, false);
                source.spatialBlend = 1f;
                source.minDistance = _defaultMinDistance;
                source.maxDistance = _defaultMaxDistance;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                _sfxPool[i] = source;
            }
        }

        private AudioSource CreateChildSource(string objectName, AudioMixerGroup mixerGroup, bool loop)
        {
            GameObject sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(transform);
            sourceObject.transform.localPosition = Vector3.zero;
            sourceObject.transform.localRotation = Quaternion.identity;
            sourceObject.transform.localScale = Vector3.one;

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.outputAudioMixerGroup = mixerGroup;

            return source;
        }

        public void PlayBGM(AudioClip clip, bool loop = true, float volume = 1f)
        {
            if (clip == null || _bgmSource == null) return;

            if (_bgmSource.clip == clip && _bgmSource.isPlaying)
                return;

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.volume = Mathf.Clamp01(volume);
            _bgmSource.pitch = 1f;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.outputAudioMixerGroup = _bgmGroup;
            _bgmSource.Play();
        }

        public void StopBGM()
        {
            if (_bgmSource == null) return;

            _bgmSource.Stop();
            _bgmSource.clip = null;
        }

        public void PlayUI(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _uiSource == null) return;

            _uiSource.pitch = Mathf.Max(0.01f, pitch);
            _uiSource.outputAudioMixerGroup = _uiGroup;
            _uiSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void PlaySFX2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetSFXSource();
            if (source == null) return;

            PrepareSFXSource(source, clip, transform.position, 0f, volume, pitch);
            source.Play();
        }

        public void PlaySFXAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetSFXSource();
            if (source == null) return;

            PrepareSFXSource(source, clip, position, 1f, volume, pitch);
            source.Play();
        }

        private void PrepareSFXSource(AudioSource source, AudioClip clip, Vector3 position, float spatialBlend, float volume, float pitch)
        {
            source.Stop();

            source.transform.position = position;
            source.outputAudioMixerGroup = _sfxGroup;
            source.clip = clip;

            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Max(0.01f, pitch);
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.loop = false;
            source.playOnAwake = false;
            source.minDistance = _defaultMinDistance;
            source.maxDistance = _defaultMaxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        private AudioSource GetSFXSource()
        {
            if (_sfxPool == null || _sfxPool.Length == 0)
                return null;

            for (int i = 0; i < _sfxPool.Length; i++)
            {
                int index = (_sfxPoolCursor + i) % _sfxPool.Length;

                if (!_sfxPool[index].isPlaying)
                {
                    _sfxPoolCursor = (index + 1) % _sfxPool.Length;
                    return _sfxPool[index];
                }
            }

            AudioSource forcedSource = _sfxPool[_sfxPoolCursor];
            _sfxPoolCursor = (_sfxPoolCursor + 1) % _sfxPool.Length;
            return forcedSource;
        }

        public void SetMasterVolume(float normalizedVolume)
        {
            SetMixerVolume(MasterVolumeParameter, normalizedVolume);
        }

        public void SetBGMVolume(float normalizedVolume)
        {
            SetMixerVolume(BGMVolumeParameter, normalizedVolume);
        }

        public void SetSFXVolume(float normalizedVolume)
        {
            SetMixerVolume(SFXVolumeParameter, normalizedVolume);
        }

        public void SetUIVolume(float normalizedVolume)
        {
            SetMixerVolume(UIVolumeParameter, normalizedVolume);
        }

        private void SetMixerVolume(string parameterName, float normalizedVolume)
        {
            if (_audioMixer == null) return;

            float clamped = Mathf.Clamp(normalizedVolume, 0.0001f, 1f);
            float decibel = Mathf.Log10(clamped) * 20f;

            _audioMixer.SetFloat(parameterName, decibel);
        }
    }
}