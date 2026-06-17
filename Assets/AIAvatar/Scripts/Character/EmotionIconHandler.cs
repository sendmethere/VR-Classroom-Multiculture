using UnityEngine;
using UnityEngine.UI;

namespace AIAvatar
{
    /// <summary>
    /// Shows an emotion icon for the avatar's current <see cref="AvatarDirectives.emotion"/>.
    /// A concrete <see cref="AvatarDirectiveHandler"/> — proof that new reactions
    /// plug in without touching core code. The setup wizard assigns the sprites
    /// from Art/Icons/Emotions, but you can swap them in the Inspector.
    /// </summary>
    [AddComponentMenu("AI Avatar/Handlers/Emotion Icon Handler")]
    public class EmotionIconHandler : AvatarDirectiveHandler
    {
        [Tooltip("감정 아이콘을 표시할 UI Image (보통 대화 패널 위)")]
        public Image target;

        [Header("Sprites (파일명=감정키)")]
        public Sprite neutral;
        public Sprite happy;
        public Sprite sad;
        public Sprite angry;
        public Sprite surprised;
        public Sprite thinking;

        public override void OnPersona(CharacterPersona persona)
        {
            Apply("neutral");
        }

        public override void Handle(AvatarDirectives directives)
        {
            Apply(directives != null ? directives.emotion : "neutral");
        }

        private void Apply(string emotion)
        {
            if (target == null) return;
            var sprite = Pick(emotion);
            target.sprite = sprite;
            target.enabled = sprite != null;
        }

        private Sprite Pick(string emotion)
        {
            switch ((emotion ?? "").Trim().ToLowerInvariant())
            {
                case "happy":     return happy;
                case "sad":       return sad;
                case "angry":     return angry;
                case "surprised": return surprised;
                case "thinking":  return thinking;
                default:          return neutral;
            }
        }
    }
}
