using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// The pluggable "brain" behind an avatar. Anything that can turn a player
    /// message into an <see cref="AvatarTurn"/> implements this:
    /// <list type="bullet">
    ///   <item><see cref="MockConversationProvider"/> – offline, no network.</item>
    ///   <item><see cref="ClaudeConversationProvider"/> – Anthropic Claude API.</item>
    ///   <item><see cref="DialogueTreeProvider"/> – authored branching tree that
    ///   can hand off to one of the above for free-form AI chat.</item>
    /// </list>
    /// Returns are <see cref="Awaitable{T}"/> so implementations can await network
    /// calls while staying on Unity's main thread. The controller swaps providers
    /// freely, so adding a new backend never touches the UI or character code.
    /// </summary>
    public interface IConversationProvider
    {
        /// <summary>Called once before a conversation starts.</summary>
        void Initialize(CharacterPersona persona);

        /// <summary>Produce the avatar's opening turn (greeting + first choices).</summary>
        Awaitable<AvatarTurn> BeginAsync(ConversationState state);

        /// <summary>Produce the avatar's response to a player message.</summary>
        Awaitable<AvatarTurn> RespondAsync(ConversationState state, string playerMessage);
    }
}
