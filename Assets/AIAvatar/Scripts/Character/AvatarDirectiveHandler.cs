using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// Extension point for reacting to <see cref="AvatarDirectives"/>. Add a
    /// component deriving from this to the avatar and it will be invoked every
    /// turn. This is how future systems plug in WITHOUT editing core code:
    /// <list type="bullet">
    ///   <item>A blendshape/face handler reading <c>directives.emotion</c>.</item>
    ///   <item>An Animator handler firing triggers from <c>directives.action</c>.</item>
    ///   <item>A NavMesh handler moving to <c>directives.moveTarget</c>.</item>
    /// </list>
    /// <see cref="AvatarCharacter"/> discovers and calls all handlers on it.
    /// </summary>
    public abstract class AvatarDirectiveHandler : MonoBehaviour
    {
        /// <summary>Called once when a persona is applied (initial look/setup).</summary>
        public virtual void OnPersona(CharacterPersona persona) { }

        /// <summary>Called every turn with the latest directives.</summary>
        public abstract void Handle(AvatarDirectives directives);
    }
}
