using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// Text-to-speech contract. The controller calls <see cref="Speak"/> with each
    /// avatar line. Swap implementations (REST/cloud, Android native, …) without
    /// touching the conversation code.
    /// </summary>
    public interface ITextToSpeech
    {
        bool IsSpeaking { get; }
        void Speak(string text);
        void StopSpeaking();
    }

    /// <summary>No-op TTS (logs only). Useful before a backend/key is configured.</summary>
    [AddComponentMenu("AI Avatar/Speech/Null Text To Speech (stub)")]
    public class NullTextToSpeech : MonoBehaviour, ITextToSpeech
    {
        public bool IsSpeaking => false;
        public void Speak(string text) => Debug.Log($"[AIAvatar] (TTS stub) \"{text}\"");
        public void StopSpeaking() { }
    }
}
