using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// The injectable "role" of a conversation partner. Create assets via
    /// Assets ▸ Create ▸ AI Avatar ▸ Character Persona, edit the system prompt /
    /// greeting in the Inspector, and assign to a <see cref="ConversationController"/>.
    /// The system prompt is what the LLM uses as the character's identity, so this
    /// is the main "프롬프트 주입" surface. It can also be swapped at runtime via
    /// <see cref="ConversationController.SetPersona"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "AI Avatar/Character Persona", fileName = "NewPersona")]
    public class CharacterPersona : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Avatar";

        [Header("Role / System Prompt  (← 여기에 상대의 역할/성격을 주입)")]
        [TextArea(4, 18)]
        public string systemPrompt =
            "당신은 VR 공간 안의 친근한 대화 상대입니다. 한국어로 자연스럽고 짧게 대화하세요.";

        [Header("Opening")]
        [TextArea(2, 4)] public string greeting = "안녕하세요! 무엇이 궁금하신가요?";
        public string[] openingChoices = { "안녕하세요", "당신은 누구죠?", "여긴 어디죠?" };

        [Header("Behaviour")]
        [Tooltip("LLM이 매 턴 제안할 선택지 개수 (0이면 선택지 없이 자유 입력만)")]
        [Range(0, 4)] public int suggestedChoiceCount = 3;

        [Tooltip("LLM이 표정/동작 디렉티브를 함께 생성하도록 요청할지")]
        public bool requestDirectives = true;

        [Header("Appearance (placeholder cylinder)")]
        public Color baseColor = new(0.40f, 0.60f, 1.0f);
    }
}
