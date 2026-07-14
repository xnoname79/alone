using UnityEngine;
using UnityEngine.SceneManagement;
using LastSignal.Core;
using LastSignal.Player;

namespace LastSignal.Audio
{
    /// <summary>
    /// Âm thanh THEO PLAYER, tự bootstrap 1 GameObject persistent (như StressPostFXDriver).
    /// Gộp 3 tầng cùng vòng đời (đều bám player + đổi theo scene/stress):
    ///   1. AMBIENT   — 1 lớp nền loop mỗi scene, đổi + fade khi sceneLoaded.
    ///   2. FOOTSTEP  — poll FirstPersonController: grounded + đang đi -> PlayOneShot ngẫu nhiên.
    ///   3. BREATH    — poll StressDirector.Stress -> tiếng thở loop to/gấp dần theo tier (pillar #1).
    ///
    /// Không đụng gameplay/post-fx. Clip nạp qua AudioLibrary (Resources/Audio). Thiếu clip
    /// -> tầng đó im, game vẫn chạy.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioDirector : MonoBehaviour
    {
        static AudioDirector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("AudioDirector");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioDirector>();
        }

        // ---- Ambient ----
        AudioSource _ambient;
        string _ambientClip;              // clip đang phát (tránh restart cùng scene)
        float _ambientTarget;             // volume mục tiêu (fade)

        // ---- Footstep ----
        AudioSource _step;
        AudioClip[] _footsteps;
        AudioClip _land;
        FirstPersonController _fpc;
        bool _wasGrounded = true;
        float _stepTimer;

        // ---- Breath ----
        AudioSource _breath;
        float _breathVol, _breathPitch, _breathMul = 1f;

        // ---- Heartbeat (chỉ tier cao, không fallback clip) ----
        AudioSource _heart;
        float _heartVol, _heartPitch;

        // Ambient theo tên scene. Scene lạ -> im. (số = maxVolume)
        (string clip, float vol) AmbientFor(string scene)
        {
            switch (scene)
            {
                case "Cabin_Interior":      return ("Ambient_Sci-Fi", 0.35f); // hum ấm an toàn giả
                case "DeadShip_Comms":      return ("Ambiant_Loop", 0.4f);    // đài chết lạnh
                case "DeadShip_Medical":    return ("Ambient_Sci-Fi", 0.22f); // trầm, flatline
                case "DeadShip_Cargo":      return ("Ambient_Sci-Fi", 0.16f); // echo rỗng
                case "DeadShip_Residential":return ("Ambiant_Loop", 0.3f);    // ấm đã nguội
                case "DeadShip_Archive":    return ("Machin_Loop", 0.5f);     // server rền
                default:                    return (null, 0f);
            }
        }

        void Awake()
        {
            _ambient = MakeSource("Ambient", true);
            _step = MakeSource("Footstep", false);
            _breath = MakeSource("Breath", true);

            _footsteps = new AudioClip[10];
            for (int i = 0; i < 10; i++)
                _footsteps[i] = AudioLibrary.Load("Player_Footstep_" + (i + 1).ToString("00"));
            _land = AudioLibrary.Load("Player_Land");

            // Breath: chưa có clip thở riêng -> dùng SFX_AmbienceClose làm nền thở tạm.
            // ponytail: placeholder; thay bằng breath_loop/heartbeat khi Director cấp asset.
            var breathClip = AudioLibrary.Load("breath_loop") ?? AudioLibrary.Load("SFX_AmbienceClose");
            if (breathClip != null) { _breath.clip = breathClip; _breath.volume = 0f; _breath.Play(); }

            // Heartbeat: KHÔNG fallback -> thiếu clip thì layer im.
            _heart = MakeSource("Heartbeat", true);
            var heartClip = AudioLibrary.Load("heartbeat_loop");
            if (heartClip != null) { _heart.clip = heartClip; _heart.volume = 0f; _heart.Play(); }

            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyAmbient(SceneManager.GetActiveScene().name);
        }

        AudioSource MakeSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f; // 2D
            s.volume = 0f;
            return s;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _fpc = null; // player mới mỗi scene -> tìm lại
            ApplyAmbient(scene.name);
        }

        void ApplyAmbient(string scene)
        {
            var (clip, vol) = AmbientFor(scene);
            _ambientTarget = vol;
            if (clip == _ambientClip) return; // cùng nền -> giữ phát, chỉ đổi target vol
            _ambientClip = clip;
            var ac = AudioLibrary.Load(clip);
            _ambient.Stop();
            if (ac != null) { _ambient.clip = ac; _ambient.volume = 0f; _ambient.Play(); }
        }

        // Duck: ADA là giọng duy nhất -> cắt xuyên khi dialogue mở. Ending -> music là tiếng cuối.
        bool DialogueOpen()
        {
            var dlg = LastSignal.Narrative.DialogueUI.Instance;
            return dlg != null && dlg.dialoguePanel != null && dlg.dialoguePanel.activeSelf;
        }

        void Update()
        {
            bool dialogue = DialogueOpen();
            // Ending music phát -> ambient scene về 0, để music trồi.
            float ambientMul = AudioLibrary.MusicPlaying ? 0f : (dialogue ? 0.4f : 1f);
            _breathMul = dialogue ? 0.5f : 1f;

            // Ambient fade tới target (đã nhân duck).
            _ambient.volume = Mathf.MoveTowards(_ambient.volume, _ambientTarget * ambientMul, Time.deltaTime * 0.5f);

            UpdateFootsteps();
            UpdateBreath();
            UpdateHeart();
        }

        void UpdateFootsteps()
        {
            if (_fpc == null) _fpc = Object.FindObjectOfType<FirstPersonController>();
            if (_fpc == null) return;
            var cc = _fpc.GetComponent<CharacterController>();
            if (cc == null) return;

            bool grounded = cc.isGrounded;
            float speed = _fpc.CurrentSpeed;

            // Land: vừa chạm đất sau khi rơi.
            if (grounded && !_wasGrounded && _land != null) _step.PlayOneShot(_land, 0.6f);
            _wasGrounded = grounded;

            if (grounded && speed > 0.6f)
            {
                _stepTimer -= Time.deltaTime;
                if (_stepTimer <= 0f)
                {
                    // nhanh hơn khi chạy (speed cao) — interval 0.32..0.55s.
                    _stepTimer = Mathf.Lerp(0.55f, 0.32f, Mathf.InverseLerp(1f, 4f, speed));
                    var c = _footsteps[Random.Range(0, _footsteps.Length)];
                    if (c != null) _step.PlayOneShot(c, 0.5f);
                }
            }
            else _stepTimer = 0.1f; // đứng yên -> reset để bước đầu tiên phát ngay
        }

        void UpdateBreath()
        {
            if (_breath.clip == null) return;
            var dir = StressDirector.Instance;
            var tier = dir != null ? dir.CurrentTier : StressSystem.Tier.Calm;

            // Thở: to + gấp dần theo tier. Calm gần im, Breaking hổn hển.
            float vol, pitch;
            switch (tier)
            {
                case StressSystem.Tier.Breaking:     vol = 0.5f;  pitch = 1.35f; break;
                case StressSystem.Tier.Hallucinating:vol = 0.35f; pitch = 1.15f; break;
                case StressSystem.Tier.Unease:       vol = 0.2f;  pitch = 1.0f;  break;
                default:                             vol = 0.05f; pitch = 0.9f;  break; // Calm
            }
            _breathVol = Mathf.MoveTowards(_breathVol, vol, Time.deltaTime * 0.4f);
            _breathPitch = Mathf.MoveTowards(_breathPitch, pitch, Time.deltaTime * 0.5f);
            _breath.volume = _breathVol * _breathMul; // duck khi dialogue
            _breath.pitch = _breathPitch;
        }

        // Tim đập chồng lên thở gấp, CHỈ Hallucinating+ (tier>=2). Thiếu clip -> im.
        void UpdateHeart()
        {
            if (_heart == null || _heart.clip == null) return;
            var dir = StressDirector.Instance;
            float raw = dir != null ? dir.Stress : 0f;       // 0..100
            var tier = dir != null ? dir.CurrentTier : StressSystem.Tier.Calm;

            float vol, pitch;
            switch (tier)
            {
                case StressSystem.Tier.Breaking:      vol = 0.45f; break;
                case StressSystem.Tier.Hallucinating: vol = 0.2f;  break;
                default:                              vol = 0f;    break; // Calm/Unease: tim im
            }
            pitch = Mathf.Lerp(0.9f, 1.3f, Mathf.InverseLerp(70f, 100f, raw)); // đập nhanh theo stress

            _heartVol = Mathf.MoveTowards(_heartVol, vol, Time.deltaTime * 0.4f);
            _heartPitch = Mathf.MoveTowards(_heartPitch, pitch, Time.deltaTime * 0.5f);
            _heart.volume = _heartVol * _breathMul; // duck chung khi dialogue
            _heart.pitch = _heartPitch;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }
    }
}
