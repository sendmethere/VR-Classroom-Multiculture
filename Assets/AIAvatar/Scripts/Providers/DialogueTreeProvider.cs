using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// Hybrid provider: walks an authored <see cref="DialogueTree"/> and hands off
    /// to a free-form AI provider when a node is flagged for it — or when the
    /// player types something that doesn't match any scripted choice (if
    /// <see cref="freeTextEntersAI"/> is on). Pick scripted choices for a guided
    /// branch, or just start talking to drop into AI conversation.
    /// </summary>
    [AddComponentMenu("AI Avatar/Providers/Dialogue Tree Provider")]
    public class DialogueTreeProvider : MonoBehaviour, IConversationProvider
    {
        [SerializeField] private DialogueTree tree;

        [Tooltip("AI 핸드오프에 사용할 프로바이더 (Mock 또는 Claude 컴포넌트를 드래그)")]
        [SerializeField] private MonoBehaviour aiProviderBehaviour;

        [Tooltip("선택지에 없는 자유 입력이 들어오면 AI 대화로 전환")]
        [SerializeField] private bool freeTextEntersAI = true;

        private IConversationProvider ai;
        private CharacterPersona persona;
        private string currentNodeId;
        private bool inAIMode;

        public void Initialize(CharacterPersona persona)
        {
            this.persona = persona;
            ai = aiProviderBehaviour as IConversationProvider;
            ai?.Initialize(persona);
            currentNodeId = tree != null ? tree.rootNodeId : null;
            inAIMode = false;
        }

        public async Awaitable<AvatarTurn> BeginAsync(ConversationState state)
        {
            if (tree == null)
            {
                Debug.LogWarning("[AIAvatar] DialogueTreeProvider has no tree; falling back to AI/greeting.");
                if (ai != null) return await ai.BeginAsync(state);
                return AvatarTurn.Say(persona != null ? persona.greeting : "...");
            }

            var node = tree.Find(currentNodeId);
            if (node == null && tree.nodes.Count > 0) node = tree.nodes[0];

            if (node != null && node.handoffToAI && ai != null)
            {
                inAIMode = true;
                return await ai.BeginAsync(state);
            }
            return TurnFromNode(node);
        }

        public async Awaitable<AvatarTurn> RespondAsync(ConversationState state, string message)
        {
            if (inAIMode && ai != null)
                return await ai.RespondAsync(state, message);

            var node = tree != null ? tree.Find(currentNodeId) : null;
            if (node == null)
                return AvatarTurn.Say("(대화 트리 노드를 찾을 수 없어요.)");

            // Try to match a scripted choice (exact, case-insensitive).
            DialogueChoice picked = null;
            foreach (var c in node.choices)
            {
                if (c != null && string.Equals(c.text?.Trim(), message?.Trim(),
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    picked = c;
                    break;
                }
            }

            if (picked == null)
            {
                // Free text / unmatched — optionally drop into AI conversation.
                if (freeTextEntersAI && ai != null)
                {
                    inAIMode = true;
                    return await ai.RespondAsync(state, message);
                }
                return TurnFromNode(node, prefix: "음, 아래에서 골라주세요.\n");
            }

            if (string.IsNullOrEmpty(picked.nextNodeId))
                return new AvatarTurn { reply = "(대화 종료)", endConversation = true };

            currentNodeId = picked.nextNodeId;
            var next = tree.Find(currentNodeId);

            if (next != null && next.handoffToAI && ai != null)
            {
                inAIMode = true;
                return await ai.RespondAsync(state, message);
            }
            return TurnFromNode(next);
        }

        private AvatarTurn TurnFromNode(DialogueNode node, string prefix = null)
        {
            if (node == null)
                return new AvatarTurn { reply = "(노드 없음)", endConversation = true };

            var turn = new AvatarTurn
            {
                reply = (prefix ?? "") + node.avatarLine,
                directives = new AvatarDirectives { emotion = node.emotion, action = node.action }
            };
            foreach (var c in node.choices)
                if (c != null && !string.IsNullOrWhiteSpace(c.text))
                    turn.choices.Add(c.text);

            // A node with no choices and no handoff is a leaf → end.
            if (turn.choices.Count == 0 && !node.handoffToAI)
                turn.endConversation = true;

            return turn;
        }
    }
}
