using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.UI.Common;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Audio
{
    /// <summary>Boot 显式装配的有限声部播放器；声音随机数与战斗随机数完全分离。</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class GameAudioService : MonoBehaviour
    {
        public GameAudioCatalog Catalog;
        public AudioSource[] Voices = Array.Empty<AudioSource>();
        public AudioSource[] Ambience = Array.Empty<AudioSource>();
        public const int ShortVoiceCount = 24;
        private const int ReservedVoices = 4;
        private const string PrefKey = "DeepSleep.Audio.";

        private sealed class Voice
        {
            public AudioSource Source;
            public AudioCueDefinition Definition;
            public float Gain, Started, Envelope = 1f, FadeSeconds;
            public int Handle, SourceId;
            public bool Paused, FadingOut;
        }
        private Voice[] _voices;
        private AudioCueDefinition[] _definitions;
        private int[] _lastClip;
        private float[] _lastPlay;
        private readonly System.Random _random = new(81371);
        private readonly Dictionary<long, float> _sourceTimes = new();
        private int _handle, _ambientSlot;
        private AudioCue? _ambienceCue;
        private readonly float[] _ambientGains = new float[2];
        private readonly AudioCueDefinition[] _ambientDefinitions = new AudioCueDefinition[2];
        private float _ambientFadeSeconds = 1f;
        private Camera _camera;
        private UnityEngine.Object _sceneOwner;
        private bool _worldPaused, _backgrounded, _ready;
        private bool _applicationPaused, _focusLost;
        private float _duckUntil;
        private float _menuGain = 1f;
        private bool _menuOpen;

        public float MasterVolume { get; private set; } = .8f;
        public float SfxVolume { get; private set; } = .8f;
        public float AmbienceVolume { get; private set; } = .35f;
        public int PlayedCount { get; private set; }
        public int SuppressedCount { get; private set; }
        public int PeakActiveVoices { get; private set; }
        public bool IsReady => _ready;
        public event Action<AudioCue> Played;

        private void Awake()
        {
            if (Catalog == null || !Catalog.TryValidate(out _) || Voices == null || Voices.Length != ShortVoiceCount ||
                Ambience == null || Ambience.Length != 2 || Array.Exists(Voices, v => v == null) || Array.Exists(Ambience, v => v == null))
            { Debug.LogError("[GameAudio] Explicit catalog, 24 voices and 2 ambience sources required.", this); enabled = false; return; }
            int count = Enum.GetValues(typeof(AudioCue)).Length;
            _definitions = new AudioCueDefinition[count]; _lastClip = new int[count]; _lastPlay = new float[count];
            for (int i = 0; i < count; i++) { _lastClip[i] = -1; _lastPlay[i] = float.NegativeInfinity; }
            foreach (var entry in Catalog.Entries) _definitions[(int)entry.Cue] = entry;
            _voices = new Voice[Voices.Length];
            for (int i = 0; i < Voices.Length; i++) _voices[i] = new Voice { Source = Voices[i] };
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey + "Master", .8f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey + "Sfx", .8f));
            AmbienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey + "Ambience", .35f));
            _ready = true;
        }

        public void BindScene(UnityEngine.Object owner, Camera camera)
        {
            StopWorld();
            _menuOpen = false; _menuGain = 1f;
            _sceneOwner = owner; _camera = camera; _worldPaused = false;
        }

        public void ReleaseScene(UnityEngine.Object owner)
        {
            if (_sceneOwner != owner) return;
            StopWorld(); _sceneOwner = null; _camera = null;
            ClearAmbience();
        }

        public bool Play(AudioCue cue, Vector2? position = null, int sourceId = 0, float gain = 1f) =>
            PlayInternal(cue, position, sourceId, gain, false) != 0;

        public int PlayTracked(AudioCue cue, Vector2? position = null, int sourceId = 0, float gain = 1f) =>
            PlayInternal(cue, position, sourceId, gain, false);

        public int StartLoop(AudioCue cue, Vector2? position = null, int sourceId = 0)
        {
            if (!_ready || !isActiveAndEnabled || _backgrounded) return 0;
            foreach (var voice in _voices)
                if (voice.Handle != 0 && voice.Source.loop && voice.Definition.Cue == cue && voice.SourceId == sourceId)
                {
                    if (!voice.Paused && !voice.Source.isPlaying) { Clear(voice); continue; }
                    // A cancelled fade resumes the existing sound from its present level.
                    voice.FadingOut = false; voice.FadeSeconds = .04f;
                    return voice.Handle;
                }
            return PlayInternal(cue, position, sourceId, 1f, true);
        }

        private int PlayInternal(AudioCue cue, Vector2? point, int sourceId, float gain, bool loop)
        {
            if (!_ready || !isActiveAndEnabled || _backgrounded || (uint)cue >= _definitions.Length) return 0;
            var definition = _definitions[(int)cue];
            if (definition.PauseWithWorld && (_worldPaused || Time.timeScale == 0f)) return 0;
            float now = Time.unscaledTime;
            long key = ((long)(int)cue << 32) | (uint)sourceId;
            if (now - _lastPlay[(int)cue] < definition.MinimumInterval ||
                (_sourceTimes.TryGetValue(key, out float last) && now - last < definition.MinimumInterval))
            { SuppressedCount++; return 0; }
            int active = 0, same = 0;
            Voice slot = null, weakest = null;
            int available = definition.Importance >= 80 ? _voices.Length : _voices.Length - ReservedVoices;
            for (int i = 0; i < _voices.Length; i++)
            {
                var voice = _voices[i];
                if (voice.Handle != 0 && !voice.Paused && !voice.Source.isPlaying) Clear(voice);
                if (voice.Handle == 0) { if (i < available && slot == null) slot = voice; continue; }
                active++;
                if (voice.Definition.Cue == cue) same++;
                if (i < available && !voice.Source.loop && !voice.Paused &&
                    (weakest == null || voice.Definition.Importance < weakest.Definition.Importance ||
                     (voice.Definition.Importance == weakest.Definition.Importance && voice.Started < weakest.Started))) weakest = voice;
            }
            if (same >= definition.MaximumVoices) { SuppressedCount++; return 0; }
            if (slot == null && weakest != null && weakest.Definition.Importance < definition.Importance)
            { slot = weakest; Clear(slot); active--; }
            if (slot == null) { SuppressedCount++; return 0; }
            AudioClip[] clips = definition.IsUi && UiThemePreferences.Current == PlayerRole.Harness && definition.HarnessClips.Length > 0
                ? definition.HarnessClips : definition.Clips;
            int index = _random.Next(clips.Length);
            if (clips.Length > 1 && index == _lastClip[(int)cue]) index = (index + 1) % clips.Length;
            _lastClip[(int)cue] = index; _lastPlay[(int)cue] = now;
            if (_sourceTimes.Count > 256) _sourceTimes.Clear();
            _sourceTimes[key] = now;
            var source = slot.Source;
            source.Stop(); source.clip = clips[index]; source.outputAudioMixerGroup = definition.Output;
            source.loop = loop; source.pitch = loop ? 1f : 1f + ((float)_random.NextDouble() * 2 - 1) * definition.PitchVariation;
            source.panStereo = point.HasValue && _camera != null ? Mathf.Clamp((_camera.WorldToViewportPoint(point.Value).x - .5f) * 1.2f, -.6f, .6f) : 0f;
            slot.Definition = definition; slot.SourceId = sourceId; slot.Gain = definition.Gain * Mathf.Clamp01(gain);
            slot.Envelope = 1; slot.FadeSeconds = 0; slot.FadingOut = false; slot.Started = now; slot.Paused = false;
            slot.Handle = ++_handle; if (slot.Handle == 0) slot.Handle = ++_handle;
            if (definition.Importance >= 80 && !definition.IsUi) _duckUntil = now + .18f;
            source.volume = Volume(slot); source.Play();
            PlayedCount++; PeakActiveVoices = Mathf.Max(PeakActiveVoices, active + 1); Played?.Invoke(cue);
            return slot.Handle;
        }

        public void StopLoop(int handle, float fadeSeconds = .08f)
        {
            if (!_ready || handle == 0) return;
            foreach (var voice in _voices)
                if (voice.Handle == handle)
                {
                    if (fadeSeconds <= 0f || voice.Paused) Clear(voice);
                    else { voice.FadingOut = true; voice.FadeSeconds = fadeSeconds; }
                    return;
                }
        }

        public void StopWorld()
        {
            if (!_ready) return;
            foreach (var voice in _voices) if (voice.Handle != 0 && !voice.Definition.IsUi) Clear(voice);
            _sourceTimes.Clear();
            for (int i = 0; i < _lastPlay.Length; i++) if (!_definitions[i].IsUi) _lastPlay[i] = float.NegativeInfinity;
            _duckUntil = 0;
        }

        public void SetWorldPaused(bool paused)
        {
            _worldPaused = paused;
            if (_ready) foreach (var voice in _voices) if (voice.Handle != 0) ApplyPause(voice);
        }

        public void SetLocalMenuOpen(bool open) => _menuOpen = open;

        public void SetAmbience(AudioCue cue, float fadeSeconds = 1f)
        {
            bool ambience = cue >= AudioCue.AmbienceSky && cue <= AudioCue.AmbienceRest ||
                cue >= AudioCue.AmbienceCyber && cue <= AudioCue.AmbienceArcade;
            if (!_ready || !isActiveAndEnabled || cue == _ambienceCue || !ambience) return;
            var entry = _definitions[(int)cue];
            _ambientFadeSeconds = Mathf.Max(.05f, fadeSeconds);
            for (int i = 0; i < 2; i++)
                if (_ambientDefinitions[i] == entry && Ambience[i].clip != null)
                {
                    // A -> B -> A while crossfading reverses the envelopes without
                    // restarting A or jumping its current volume back to zero.
                    _ambientSlot = i; _ambienceCue = cue; return;
                }
            _ambientSlot = 1 - _ambientSlot;
            var source = Ambience[_ambientSlot];
            source.Stop(); source.clip = entry.Clips[0]; source.outputAudioMixerGroup = entry.Output;
            source.loop = true; source.pitch = 1; source.volume = 0;
            _ambientGains[_ambientSlot] = 0; _ambientDefinitions[_ambientSlot] = entry;
            _ambienceCue = cue;
            source.Play(); if (_backgrounded) source.Pause();
        }

        public void SetVolumes(float master, float sfx, float ambience, bool save = true)
        {
            MasterVolume = Mathf.Clamp01(master); SfxVolume = Mathf.Clamp01(sfx); AmbienceVolume = Mathf.Clamp01(ambience);
            if (_ready)
            {
                foreach (var voice in _voices) if (voice.Handle != 0) voice.Source.volume = Volume(voice);
                RefreshAmbienceVolumes();
            }
            if (save)
            {
                PlayerPrefs.SetFloat(PrefKey + "Master", MasterVolume); PlayerPrefs.SetFloat(PrefKey + "Sfx", SfxVolume);
                PlayerPrefs.SetFloat(PrefKey + "Ambience", AmbienceVolume); PlayerPrefs.Save();
            }
        }

        private void Update() => UpdatePlayback(Time.unscaledDeltaTime);

        private void UpdatePlayback(float deltaTime)
        {
            if (!_ready) return;
            _menuGain = Mathf.MoveTowards(_menuGain, _menuOpen ? .55f : 1f, deltaTime * 4f);
            foreach (var voice in _voices)
            {
                if (voice.Handle == 0) continue;
                ApplyPause(voice);
                if (!voice.Paused && !voice.Source.isPlaying) { Clear(voice); continue; }
                if (voice.FadeSeconds > 0)
                {
                    voice.Envelope = Mathf.MoveTowards(voice.Envelope, voice.FadingOut ? 0f : 1f, deltaTime / voice.FadeSeconds);
                    if (voice.FadingOut && voice.Envelope <= 0) { Clear(voice); continue; }
                }
                voice.Source.volume = Volume(voice);
            }
            if (_ambienceCue.HasValue && !_backgrounded)
                for (int i = 0; i < 2; i++)
                {
                    _ambientGains[i] = Mathf.MoveTowards(_ambientGains[i], i == _ambientSlot ? 1f : 0f, deltaTime / _ambientFadeSeconds);
                    if (i != _ambientSlot && _ambientGains[i] == 0)
                    { Ambience[i].Stop(); Ambience[i].clip = null; _ambientDefinitions[i] = null; }
                }
            RefreshAmbienceVolumes();
        }

        private void ApplyPause(Voice voice)
        {
            bool pause = _backgrounded || (voice.Definition.PauseWithWorld && (_worldPaused || Time.timeScale == 0f));
            if (voice.Paused == pause) return;
            voice.Paused = pause;
            if (pause) voice.Source.Pause(); else voice.Source.UnPause();
        }

        private void RefreshAmbienceVolumes()
        {
            for (int i = 0; i < 2; i++)
                Ambience[i].volume = _ambientGains[i] * (_ambientDefinitions[i]?.Gain ?? 0f) * MasterVolume * AmbienceVolume;
        }

        private void ClearAmbience()
        {
            foreach (var source in Ambience) { source.Stop(); source.clip = null; source.volume = 0; }
            _ambienceCue = null; Array.Clear(_ambientGains, 0, 2); Array.Clear(_ambientDefinitions, 0, 2);
        }

        private float Volume(Voice voice) => voice.Gain * MasterVolume * SfxVolume *
            voice.Envelope *
            (voice.Definition.IsUi ? 1f : _menuGain) *
            (!voice.Definition.IsUi && voice.Definition.Importance < 80 && Time.unscaledTime < _duckUntil ? .72f : 1f);

        private static void Clear(Voice voice)
        { voice.Source.Stop(); voice.Source.clip = null; voice.Source.loop = false; voice.Handle = 0; voice.Paused = false; }

        private void OnApplicationPause(bool paused)
        { _applicationPaused = paused; SetBackground(_applicationPaused || _focusLost); }
        private void OnApplicationFocus(bool focused)
        {
            if (Application.isEditor) return;
            _focusLost = !focused; SetBackground(_applicationPaused || _focusLost);
        }
        private void SetBackground(bool value)
        {
            if (!_ready || _backgrounded == value) return;
            _backgrounded = value;
            foreach (var voice in _voices)
                if (voice.Handle != 0)
                {
                    if (value && !voice.Source.loop) Clear(voice);
                    else ApplyPause(voice);
                }
            foreach (var source in Ambience) { if (value) source.Pause(); else if (source.clip != null) source.UnPause(); }
        }
        private void OnDisable()
        {
            if (_voices != null) foreach (var voice in _voices) Clear(voice);
            if (_ready) ClearAmbience();
            else if (Ambience != null) foreach (var source in Ambience) if (source != null) source.Stop();
        }
    }
}
