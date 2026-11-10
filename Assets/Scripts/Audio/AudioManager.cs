using System.Collections.Generic;
using System.Threading;
using AR.Core;
using UnityEngine;

namespace AR.Audio
{
    /// <summary>BGM + pooled SFX voices with a per-clip cooldown (bullet hell fires the same clip constantly).</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int VoiceCount = 12;
        private const float ClipCooldown = 0.05f;

        private AudioSource _bgm;
        private readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        private readonly Dictionary<AudioClip, float> _lastPlayed = new();
        private int _nextVoice;
        private string _bgmAddress = "";

        [field: SerializeField] public float SfxVolume { get; set; } = 0.8f;
        [field: SerializeField] public float BgmVolume { get; set; } = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            for (int i = 0; i < VoiceCount; i++)
            {
                var v = gameObject.AddComponent<AudioSource>();
                v.playOnAwake = false;
                _voices[i] = v;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Fire-and-forget SFX by address ("SFX/hit"). Missing clips are silently skipped.</summary>
        public void PlaySfx(string address, float pitchJitter = 0.05f)
        {
            if (string.IsNullOrEmpty(address)) return;
            _ = PlaySfxAsync(address, pitchJitter, destroyCancellationToken);
        }

        private async Awaitable PlaySfxAsync(string address, float pitchJitter, CancellationToken ct)
        {
            try
            {
                var clip = await AssetLoader.LoadAsync<AudioClip>(address, ct);
                if (clip != null) PlayClip(clip, pitchJitter);
            }
            catch (System.OperationCanceledException) { }
        }

        public void PlayClip(AudioClip clip, float pitchJitter = 0.05f)
        {
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(clip, out var last) && now - last < ClipCooldown) return;
            _lastPlayed[clip] = now;
            var v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % VoiceCount;
            v.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            v.PlayOneShot(clip, SfxVolume);
        }

        /// <summary>"" keeps the current track, "stop" stops it, anything else is a BGM address.</summary>
        public async Awaitable SetBgmAsync(string address, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(address) || address == _bgmAddress) return;
            _bgmAddress = address;
            if (address == "stop")
            {
                _bgm.Stop();
                return;
            }
            var clip = await AssetLoader.LoadAsync<AudioClip>(address, ct);
            if (_bgmAddress != address) return; // changed while loading
            _bgm.Stop();
            if (clip == null) return;
            _bgm.clip = clip;
            _bgm.volume = BgmVolume;
            _bgm.Play();
        }
    }
}
