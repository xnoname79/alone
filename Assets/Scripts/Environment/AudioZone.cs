using UnityEngine;

namespace LastSignal.Environment
{
    /// <summary>
    /// Vùng âm thanh ambient với fade in/out khi player vào/ra.
    /// Dùng cho ambient theo khu vực (cabin hum, gió rít xác tàu, server drone archive).
    /// Xem GDD section 'audio' — âm thanh là trụ cột chủ đạo.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(Collider))]
    public class AudioZone : MonoBehaviour
    {
        [Header("Fade")]
        public float fadeInDuration = 1.5f;
        public float fadeOutDuration = 2f;
        [Range(0f, 1f)] public float maxVolume = 1f;

        [Header("Trigger")]
        public string playerTag = "Player";

        private AudioSource _source;
        private bool _playerInside;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.volume = 0f;
            _source.loop = true;
            _source.playOnAwake = false;
        }

        void Update()
        {
            float target = _playerInside ? maxVolume : 0f;
            float dur = _playerInside ? fadeInDuration : fadeOutDuration;
            float speed = dur > 0f ? (maxVolume / dur) : maxVolume;
            _source.volume = Mathf.MoveTowards(_source.volume, target, speed * Time.deltaTime);

            if (_source.volume > 0.001f && !_source.isPlaying) _source.Play();
            else if (_source.volume <= 0.001f && _source.isPlaying) _source.Stop();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag)) _playerInside = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag)) _playerInside = false;
        }
    }
}
