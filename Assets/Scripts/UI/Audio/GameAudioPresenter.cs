using System.Collections.Generic;
using AMath.Core.Events;
using AMath.Networking;
using AMath.Settings;
using UnityEngine;

namespace AMath.UI.Audio
{
    /// <summary>
    /// Plays simple procedural SFX from match events. Volume follows the
    /// Sound Effects preference stored by the settings screen.
    /// </summary>
    public sealed class GameAudioPresenter : MonoBehaviour
    {
        private readonly Dictionary<(float frequency, float duration), AudioClip> _clips = new();

        private IEventBus _eventBus;
        private AudioSource _source;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        private void Start()
        {
            if (NetworkContext.Services == null) return;
            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _eventBus.Subscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Subscribe<MatchFinishedEvent>(OnMatchFinished);
            _eventBus.Subscribe<CommandRejectedEvent>(OnRejected);
            _eventBus.Subscribe<TileDraggedEvent>(OnTileDragged);
        }

        private void OnDestroy()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<TurnResolvedEvent>(OnTurnResolved);
            _eventBus.Unsubscribe<MatchFinishedEvent>(OnMatchFinished);
            _eventBus.Unsubscribe<CommandRejectedEvent>(OnRejected);
            _eventBus.Unsubscribe<TileDraggedEvent>(OnTileDragged);
        }

        private void OnTurnResolved(TurnResolvedEvent evt) =>
            Beep(evt.Record != null && evt.Record.ScoreDelta > 0 ? 740f : 420f, 0.08f, 0.14f);

        private void OnMatchFinished(MatchFinishedEvent _) => Beep(660f, 0.18f, 0.22f);

        private void OnRejected(CommandRejectedEvent _) => Beep(180f, 0.12f, 0.18f);

        private void OnTileDragged(TileDraggedEvent _) => Beep(520f, 0.04f, 0.08f);

        private void Beep(float frequency, float duration, float volumeScale)
        {
            // Read the live preference rather than PlayerPrefs: the settings
            // screen only flushes to disk when it closes, so the stored value
            // lags behind the slider the player just moved.
            float volume = GameSettings.SoundEffectsVolume * volumeScale;
            if (volume <= 0.001f || _source == null) return;

            _source.PlayOneShot(GetClip(frequency, duration), volume);
        }

        /// <summary>
        /// There are only a handful of distinct beeps, so each waveform is
        /// generated once instead of allocating a clip and a sample buffer on
        /// every tile tap.
        /// </summary>
        private AudioClip GetClip(float frequency, float duration)
        {
            var key = (frequency, duration);
            if (_clips.TryGetValue(key, out AudioClip cached) && cached != null)
                return cached;

            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("sfx", samples, 1, sampleRate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            clip.SetData(data, 0);
            _clips[key] = clip;
            return clip;
        }
    }
}
