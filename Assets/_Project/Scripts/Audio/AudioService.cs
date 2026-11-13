using System;
using Cysharp.Threading.Tasks;
using SmashFest.Data;
using SmashFest.Progress;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace SmashFest.Audio
{
    public class AudioService : IAudioService, IInitializable, IDisposable
    {
        private const float BurstWindow = 0.08f;
        private const int MaxSoundsPerBurst = 4;

        private readonly SoundLibrarySO library;
        private readonly IPlayerProgressService progress;
        private readonly SoundEmitterPool emitters;

        private SoundEmitter music;
        private float burstStart;
        private int burstCount;

        public AudioService(SoundEmitterPool emitters, IPlayerProgressService progress, [InjectOptional] SoundLibrarySO library)
        {
            this.emitters = emitters;
            this.progress = progress;
            this.library = library;
        }

        public void Initialize()
        {
            progress.SettingsChanged += ApplySettings;
            ApplySettings();
        }

        public void Dispose() => progress.SettingsChanged -= ApplySettings;

        public void PlayClick() => Play(library != null ? library.uiClicks : null, library != null ? library.uiClickVolume : 0f);

        public void PlayCannonFire() => Play(library != null ? library.cannonFire : null, 0.85f);

        public void PlayBallThud(float strength) => Play(library != null ? library.ballThuds : null, Mathf.Lerp(0.25f, 0.7f, strength));

        public void PlayHit(BlockMaterialType material, float strength)
        {
            MaterialSounds sounds = library != null ? library.Find(material) : null;
            if (sounds != null)
                Play(sounds.hits, sounds.volume * Mathf.Lerp(0.3f, 0.9f, strength));
        }

        public void PlayBreak(BlockMaterialType material)
        {
            MaterialSounds sounds = library != null ? library.Find(material) : null;
            if (sounds != null)
                Play(sounds.breaks, sounds.volume);

            if (material == BlockMaterialType.CeramicJar)
                Play(library != null ? library.liquidSplats : null, 0.8f);
        }

        public void PlayLevelWin() => PlayJingle(library != null ? library.levelWin : null);

        public void PlayLevelFail() => PlayJingle(library != null ? library.levelFail : null);

        private void ApplySettings()
        {
            bool musicOn = progress.MusicEnabled && library != null && library.music != null;
            if (!musicOn)
            {
                emitters.Release(music);
                music = null;
                return;
            }

            if (music == null)
                music = emitters.Get();

            if (music.IsPlaying && music.Clip == library.music)
                music.SetVolume(library.musicVolume);
            else
                music.Play(library.music, library.musicVolume, 1f, true);
        }

        private void Play(AudioClip[] clips, float volume)
        {
            if (!progress.SoundEnabled || clips == null || clips.Length == 0 || !CanPlayNow())
                return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
                PlayClip(clip, volume, Random.Range(0.92f, 1.08f));
        }

        private void PlayJingle(AudioClip clip)
        {
            if (progress.SoundEnabled && clip != null)
                PlayClip(clip, library.jingleVolume, 1f);
        }

        private void PlayClip(AudioClip clip, float volume, float pitch)
        {
            SoundEmitter emitter = emitters.Get();
            emitter.Play(clip, volume * library.effectsVolume, pitch, false);
            ReleaseWhenDone(emitter).Forget();
        }

        private async UniTaskVoid ReleaseWhenDone(SoundEmitter emitter)
        {
            int playId = emitter.PlayId;
            await UniTask.Delay(TimeSpan.FromSeconds(emitter.Duration), DelayType.UnscaledDeltaTime);
            if (emitter != null && emitter.PlayId == playId)
                emitters.Release(emitter);
        }

        private bool CanPlayNow()
        {
            if (Time.unscaledTime - burstStart > BurstWindow)
            {
                burstStart = Time.unscaledTime;
                burstCount = 0;
            }

            burstCount++;
            return burstCount <= MaxSoundsPerBurst;
        }
    }
}
