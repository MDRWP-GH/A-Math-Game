using AMath.Core.Events;
using AMath.Networking;
using UnityEngine;

namespace AMath.UI.Audio
{
    /// <summary>
    /// Plays simple procedural SFX from match events. Volume follows the
    /// Sound Effects preference stored by the settings screen.
    /// </summary>
    public sealed class GameAudioPresenter : MonoBehaviour
    {
        private const string SoundEffectsKey = "amath.audio.soundEffects";

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
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundEffectsKey, 0.8f)) * volumeScale;
            if (volume <= 0.001f || _source == null) return;

            int sampleRate = 44100;
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
            _source.PlayOneShot(clip, volume);
        }
    }
}
