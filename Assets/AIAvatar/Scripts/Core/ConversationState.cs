using System;
using System.Collections.Generic;

namespace AIAvatar
{
    public enum Speaker { Player, Avatar, System }

    /// <summary>A single line in the conversation transcript.</summary>
    [Serializable]
    public class ConversationMessage
    {
        public Speaker speaker;
        public string text;

        public ConversationMessage(Speaker speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    /// <summary>
    /// Provider-agnostic conversation state: the active persona plus the full
    /// transcript. Providers read this to build context (e.g. Claude maps it to
    /// user/assistant messages); the controller appends to it as the talk flows.
    /// </summary>
    public class ConversationState
    {
        public CharacterPersona Persona;
        public readonly List<ConversationMessage> History = new();

        public void Add(Speaker speaker, string text) =>
            History.Add(new ConversationMessage(speaker, text));

        public void Clear() => History.Clear();
    }
}
