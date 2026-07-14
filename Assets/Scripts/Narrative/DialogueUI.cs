using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using LastSignal.Core;

namespace LastSignal.Narrative
{
    /// <summary>
    /// UI hội thoại AI với typing effect + hỗ trợ lựa chọn (choices).
    /// Dùng Input System (action 'Advance' = Space) để qua dòng / tua nhanh typing.
    /// Emotion điều chỉnh tốc độ gõ (về sau map thêm filter giọng + màu UI).
    ///
    /// Singleton để NotePickup và các hệ thống khác gọi nhanh: DialogueUI.Instance.Show(...).
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }

        [Header("Input")]
        public InputActionReference advanceAction;

        [Header("UI References")]
        public GameObject dialoguePanel;
        public TMP_Text speakerText;
        public TMP_Text contentText;
        [Tooltip("Container chứa các nút lựa chọn (ẩn nếu dòng không có choices).")]
        public Transform choicesContainer;
        public Button choiceButtonPrefab;

        [Header("Typing")]
        public float baseTypingSpeed = 0.03f;

        [Header("References (tùy chọn)")]
        public GameState gameState;

        private bool _isTyping;
        private string _fullText;
        private Coroutine _typeRoutine;
        private DialogueLine _currentLine;
        private Action _onClosed;
        private readonly List<Button> _choiceButtons = new List<Button>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (gameState == null) gameState = GameState.Instance;
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            ResolveAdvance();
        }

        // Action đã resolve (ưu tiên action runtime do BindAdvance bơm vào).
        private InputAction _advance;

        /// <summary>Bơm InputAction trực tiếp (dùng khi tạo runtime, vd CabinBootstrap).</summary>
        public void BindAdvance(InputAction advance)
        {
            _advance = advance;
            _advance?.Enable();
        }

        void ResolveAdvance()
        {
            if (_advance == null && advanceAction != null) _advance = advanceAction.action;
        }

        void OnEnable() { ResolveAdvance(); _advance?.Enable(); }
        void OnDisable() => _advance?.Disable();

        /// <summary>Hiển thị nhanh một dòng đơn giản (note, lời AI ngắn).</summary>
        public void Show(string speaker, string content, Action onClosed = null)
        {
            Show(new DialogueLine { speaker = speaker, content = content }, onClosed);
        }

        /// <summary>Phát lần lượt nhiều dòng (vd lời AI mở màn nhiều câu). Space để qua từng dòng.</summary>
        public void ShowSequence(IList<DialogueLine> lines, Action onClosed = null)
        {
            if (lines == null || lines.Count == 0) { onClosed?.Invoke(); return; }
            int i = 0;
            Action playNext = null;
            playNext = () =>
            {
                if (i >= lines.Count) { onClosed?.Invoke(); return; }
                var line = lines[i++];
                Show(line, playNext);
            };
            playNext();
        }

        public void Show(DialogueLine line, Action onClosed = null)
        {
            _currentLine = line;
            _onClosed = onClosed;

            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (speakerText != null) speakerText.text = line.speaker;
            ClearChoices();

            _fullText = line.content;
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _typeRoutine = StartCoroutine(TypeText(line));
        }

        IEnumerator TypeText(DialogueLine line)
        {
            _isTyping = true;
            if (contentText != null) contentText.text = "";
            float speed = SpeedForEmotion(line.emotion);

            foreach (char c in _fullText)
            {
                if (contentText != null) contentText.text += c;
                yield return new WaitForSeconds(speed);
            }
            _isTyping = false;

            // Sau khi gõ xong, nếu có choices thì hiện nút; nếu không, chờ Advance để đóng.
            if (line.choices != null && line.choices.Length > 0)
                ShowChoices(line.choices);
        }

        void Update()
        {
            if (dialoguePanel == null || !dialoguePanel.activeSelf) return;
            bool advance = _advance != null && _advance.WasPressedThisFrame();
            if (!advance) return;

            if (_isTyping)
            {
                // Tua nhanh: hiện hết text ngay.
                if (_typeRoutine != null) StopCoroutine(_typeRoutine);
                if (contentText != null) contentText.text = _fullText;
                _isTyping = false;
                if (_currentLine?.choices != null && _currentLine.choices.Length > 0)
                    ShowChoices(_currentLine.choices);
            }
            else if (_currentLine == null || _currentLine.choices == null || _currentLine.choices.Length == 0)
            {
                Close();
            }
        }

        void ShowChoices(DialogueChoice[] choices)
        {
            ClearChoices();
            if (choicesContainer == null || choiceButtonPrefab == null) return;

            foreach (var choice in choices)
            {
                var btn = Instantiate(choiceButtonPrefab, choicesContainer);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = choice.text;
                var captured = choice;
                btn.onClick.AddListener(() => SelectChoice(captured));
                _choiceButtons.Add(btn);
            }
        }

        void SelectChoice(DialogueChoice choice)
        {
            var gs = gameState != null ? gameState : GameState.Instance;
            if (gs != null)
            {
                if (!string.IsNullOrEmpty(choice.setsFlag)) gs.SetFlag(choice.setsFlag);
                if (gs.stress != null && choice.stressDelta != 0f) gs.stress.Add(choice.stressDelta);
            }
            Close();
        }

        void ClearChoices()
        {
            foreach (var b in _choiceButtons) if (b != null) Destroy(b.gameObject);
            _choiceButtons.Clear();
        }

        public void Close()
        {
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _isTyping = false;
            ClearChoices();
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            var cb = _onClosed;
            _onClosed = null;
            _currentLine = null;
            cb?.Invoke();
        }

        // Emotion -> tốc độ gõ. "panicked"/"breaking" gõ nhanh giật; "sad"/"warm" gõ chậm.
        float SpeedForEmotion(string emotion)
        {
            switch (emotion)
            {
                case "cold": return baseTypingSpeed * 1.2f;
                case "warm": return baseTypingSpeed * 1.4f;
                case "sad": return baseTypingSpeed * 1.8f;
                case "panicked":
                case "breaking": return baseTypingSpeed * 0.5f;
                default: return baseTypingSpeed;
            }
        }
    }
}
