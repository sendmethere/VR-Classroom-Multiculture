using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// Offline provider — zero network. Uses the persona's greeting and a tiny
    /// bit of keyword logic so you can test the whole flow (UI, choices, gaze,
    /// emotions) without an API key. Great default while wiring the scene.
    /// </summary>
    [AddComponentMenu("AI Avatar/Providers/Mock Conversation Provider")]
    public class MockConversationProvider : MonoBehaviour, IConversationProvider
    {
        [SerializeField, Range(0f, 1.5f)] private float fakeLatency = 0.4f;

        private CharacterPersona persona;

        public void Initialize(CharacterPersona persona) => this.persona = persona;

        public async Awaitable<AvatarTurn> BeginAsync(ConversationState state)
        {
            await Awaitable.NextFrameAsync();
            return new AvatarTurn
            {
                reply = persona.greeting,
                choices = { },
                directives = new AvatarDirectives { emotion = "happy", action = "wave" }
            }.WithChoices(persona.openingChoices);
        }

        public async Awaitable<AvatarTurn> RespondAsync(ConversationState state, string message)
        {
            if (fakeLatency > 0f) await Awaitable.WaitForSecondsAsync(fakeLatency);

            string lower = (message ?? "").ToLowerInvariant();
            var turn = new AvatarTurn();

            if (lower.Contains("누구") || lower.Contains("who") || lower.Contains("이름"))
            {
                turn.reply = $"저는 {persona.displayName}예요. ({Snippet(persona.systemPrompt)})";
                turn.directives.emotion = "neutral";
            }
            else if (lower.Contains("안녕") || lower.Contains("hi") || lower.Contains("hello"))
            {
                turn.reply = "안녕하세요! 다시 만나 반가워요.";
                turn.directives.emotion = "happy";
                turn.directives.action = "nod";
            }
            else if (lower.Contains("어디") || lower.Contains("where"))
            {
                turn.reply = "여긴 VR 공간이에요. 둘러봐도 좋아요.";
                turn.directives.emotion = "neutral";
            }
            else if (lower.Contains("그만") || lower.Contains("bye") || lower.Contains("안녕히"))
            {
                turn.reply = "좋아요, 언제든 다시 말 걸어주세요!";
                turn.directives.emotion = "happy";
                turn.endConversation = true;
                return turn;
            }
            else
            {
                turn.reply = $"“{message}” 라고 하셨군요. (목업 응답) 조금 더 들려주실래요?";
                turn.directives.emotion = "thinking";
            }

            return turn.WithChoices("계속 얘기해줘", "주제를 바꾸자", "그만 할게");
        }

        private static string Snippet(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
        }
    }

    internal static class AvatarTurnExtensions
    {
        public static AvatarTurn WithChoices(this AvatarTurn turn, params string[] choices)
        {
            if (choices != null)
                foreach (var c in choices)
                    if (!string.IsNullOrWhiteSpace(c)) turn.choices.Add(c);
            return turn;
        }
    }
}
