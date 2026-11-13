using UnityEngine;

namespace CandyRush.Integrated
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class CoinPickupSound : MonoBehaviour
    {
        AudioSource source;
        public AudioClip Clip { get; private set; }

        void Awake() => Initialize();

        public void Initialize()
        {
            if (source != null) return;
            Clip = Resources.Load<AudioClip>("Candy/Audio/CoinPickup");
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.volume = .8f;
        }

        public void PlayPickup()
        {
            if (Clip != null) source.PlayOneShot(Clip);
        }
    }
}
