using UnityEngine;

namespace CandyRush.Integrated
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class RaceResultSound : MonoBehaviour
    {
        AudioSource source;
        AudioClip victory, gameOver;
        bool played;

        void Awake() => Initialize();

        public void Initialize()
        {
            if (source != null) return;
            victory = Resources.Load<AudioClip>("Candy/Audio/RaceVictory");
            gameOver = Resources.Load<AudioClip>("Candy/Audio/RaceGameOver");
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.volume = .8f;
        }

        public AudioClip ClipForPosition(int position)
        {
            if (position <= 0) return null;
            return position <= 3 ? victory : gameOver;
        }

        public void PlayForPosition(int position)
        {
            AudioClip clip = ClipForPosition(position);
            if (played || clip == null) return;
            played = true;
            source.PlayOneShot(clip);
        }

        public void ResetResult()
        {
            played = false;
            if (source != null) source.Stop();
        }
    }
}
