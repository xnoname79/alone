using System.Collections.Generic;
using UnityEngine;

namespace LastSignal.Audio
{
    /// <summary>
    /// Nạp AudioClip từ Resources/Audio/ (mirror ModelLibrary) + 2 kênh phát persistent
    /// (one-shot + music) sống qua chuyển scene nhờ DontDestroyOnLoad — nên SFX cửa/loot
    /// và nhạc ending không bị cắt khi LoadScene.
    ///
    /// Clip thiếu -> Load trả null (caller tự bỏ qua). Không fabricate.
    /// </summary>
    public static class AudioLibrary
    {
        const string Path = "Audio/";
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public static AudioClip Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>(Path + name);
            _cache[name] = c; // cache cả null để khỏi Load lại
            return c;
        }

        // ---- Kênh persistent (2D) ----
        static AudioSource _oneShot;
        static AudioSource _music;

        static AudioSource MakePersistent(string name, bool loop)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D
            return src;
        }

        public static void PlayOneShot(string name, float volume = 1f)
        {
            var clip = Load(name);
            if (clip == null) return;
            if (_oneShot == null) _oneShot = MakePersistent("Audio_OneShot", false);
            _oneShot.PlayOneShot(clip, volume);
        }

        public static void PlayMusic(string name, float volume = 1f, bool loop = true)
        {
            var clip = Load(name);
            if (clip == null) return;
            if (_music == null) _music = MakePersistent("Audio_Music", loop);
            _music.loop = loop;
            _music.clip = clip;
            _music.volume = volume;
            _music.Play();
        }

        public static void StopMusic()
        {
            if (_music != null) _music.Stop();
        }

        // Ending on -> AudioDirector nuốt ambient scene về 0, để music trồi lên (tiếng cuối).
        public static bool MusicPlaying => _music != null && _music.isPlaying;
    }
}
