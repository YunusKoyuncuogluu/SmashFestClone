using UnityEngine;

namespace SmashFest.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        public int PlayId { get; private set; }
        public bool IsPlaying => source.isPlaying;
        public AudioClip Clip => source.clip;
        public float Duration => source.clip != null ? source.clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch)) : 0f;

        public void Play(AudioClip clip, float volume, float pitch, bool loop)
        {
            PlayId++;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = loop;
            source.Play();
        }

        public void SetVolume(float volume) => source.volume = volume;

        public void Stop() => source.Stop();
    }
}
