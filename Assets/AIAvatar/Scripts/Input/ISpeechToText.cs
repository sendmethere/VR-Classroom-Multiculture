using System;
using UnityEngine;

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
    /// Optional glue: wire a "hold to talk" button or controller input to this and
    /// it forwards final transcripts to the controller. Uses any ISpeechToText on
    /// the same object (defaults to the stub).
    /// </summary>
    [AddComponentMenu("AI Avatar/Input/Speech Input Relay (stub)")]
    public class SpeechInputRelay : MonoBehaviour
    {
        [SerializeField] private ConversationController controller;
        [SerializeField] private MonoBehaviour speechToTextBehaviour; // ISpeechToText

        private ISpeechToText stt;

        private void Awake()
        {
            stt = speechToTextBehaviour as ISpeechToText
                  ?? GetComponent<ISpeechToText>();
            if (stt != null) stt.OnFinalText += HandleFinalText;
        }

        private void OnDestroy()
        {
            if (stt != null) stt.OnFinalText -= HandleFinalText;
        }

        public void BeginTalk() => stt?.StartListening();
        public void EndTalk() => stt?.StopListening();

        private void HandleFinalText(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                controller?.SubmitPlayerMessage(text);
        }
    }
}
