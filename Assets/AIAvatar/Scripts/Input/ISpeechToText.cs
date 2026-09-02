using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AIAvatar
{
    /// <summary>
    /// Speech-to-text contract. Intentionally a stub for now (음성 입력은 나중에):
    /// implement this with your STT of choice (platform STT, Whisper, a cloud API)
    /// and feed <see cref="OnFinalText"/> into <see cref="ConversationController.SubmitPlayerMessage"/>.
    /// </summary>
    public interface ISpeechToText
    {
        bool IsListening { get; }

        /// <summary>Begin capturing microphone audio.</summary>
        void StartListening();

        /// <summary>Stop capturing; a final result should arrive via <see cref="OnFinalText"/>.</summary>
        void StopListening();

        /// <summary>Partial (in-progress) transcription, if the backend supports it.</summary>
        event Action<string> OnPartialText;

        /// <summary>Final transcription for the utterance.</summary>
        event Action<string> OnFinalText;
    }

    /// <summary>
    /// Drop-in no-op implementation so the rest of the system compiles and can be
    /// wired now. Swap for a real backend later without touching the controller.
    /// </summary>
    [AddComponentMenu("AI Avatar/Input/Null Speech To Text (stub)")]
    public class NullSpeechToText : MonoBehaviour, ISpeechToText
    {
        public bool IsListening { get; private set; }
        public event Action<string> OnPartialText;
        public event Action<string> OnFinalText;

        public void StartListening()
        {
            IsListening = true;
            Debug.Log("[AIAvatar] STT StartListening — 아직 구현되지 않음 (NullSpeechToText). " +
                      "ISpeechToText를 실제 백엔드로 구현하세요.");
        }

        public void StopListening()
        {
            IsListening = false;
            // No real transcription; raise empty events so subscribers stay valid.
            OnPartialText?.Invoke(string.Empty);
            OnFinalText?.Invoke(string.Empty);
        }
    }

    /// <summary>
    /// "Hold to talk" glue. While the push-to-talk key is held (default V) it records
    /// through any <see cref="ISpeechToText"/> (e.g. <see cref="WhisperSpeechToText"/>)
    /// and, when released, forwards the final transcript to a
    /// <see cref="ConversationController"/>. If no controller is assigned it targets
    /// whichever conversation the player is currently near (the active
    /// <see cref="ProximityActivator"/>), so a single relay on the player rig serves
    /// every interview character. A UI button can also call
    /// <see cref="BeginTalk"/>/<see cref="EndTalk"/> directly.
    /// </summary>
    [AddComponentMenu("AI Avatar/Input/Speech Input Relay (Push-To-Talk)")]
    public class SpeechInputRelay : MonoBehaviour
    {
        [Tooltip("비우면 플레이어가 다가가 있는(활성) 대화를 자동으로 대상으로 함")]
        [SerializeField] private ConversationController controller;
        [SerializeField] private MonoBehaviour speechToTextBehaviour; // ISpeechToText

        [Header("Push-to-talk")]
        [Tooltip("누르고 있는 동안 녹음, 떼면 인식 (New Input System)")]
        [SerializeField] private Key pushToTalkKey = Key.V;
        [Tooltip("controller 가 비어 있을 때 활성 ProximityActivator 로 대상을 자동 탐색")]
        [SerializeField] private bool autoTargetActiveConversation = true;

        private ISpeechToText stt;
        private bool talking;

        private void Awake()
        {
            stt = speechToTextBehaviour as ISpeechToText
                  ?? GetComponent<ISpeechToText>();
            if (stt == null)
                Debug.LogWarning("[AIAvatar] SpeechInputRelay: ISpeechToText 구현이 없습니다. " +
                                 "같은 오브젝트에 WhisperSpeechToText 를 추가하세요.", this);
            else
                stt.OnFinalText += HandleFinalText;
        }

        private void OnDestroy()
        {
            if (stt != null) stt.OnFinalText -= HandleFinalText;
        }

        private void Update()
        {
            if (stt == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            var keyCtrl = kb[pushToTalkKey];
            if (keyCtrl == null) return;

            if (keyCtrl.wasPressedThisFrame && !talking) BeginTalk();
            else if (keyCtrl.wasReleasedThisFrame && talking) EndTalk();
        }

        public void BeginTalk()
        {
            if (stt == null) return;
            talking = true;
            stt.StartListening();
        }

        public void EndTalk()
        {
            if (stt == null) return;
            talking = false;
            stt.StopListening();
        }

        private void HandleFinalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var target = ResolveController();
            if (target != null) target.SubmitPlayerMessage(text);
            else Debug.Log("[AIAvatar] 인식된 음성을 받을 활성 대화를 찾지 못했습니다: " + text);
        }

        private ConversationController ResolveController()
        {
            if (controller != null) return controller;
            if (!autoTargetActiveConversation) return null;

            // 플레이어가 다가가 있는(활성) 면담을 대상으로.
            var zones = FindObjectsByType<ProximityActivator>(FindObjectsSortMode.None);
            foreach (var z in zones)
                if (z != null && z.IsActive && z.Controller != null && !z.Controller.HasEnded)
                    return z.Controller;
            return null;
        }
    }
}
