using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// The visible character. For now it's a placeholder cylinder whose colour
    /// reflects emotion, but every reaction goes through a small, named API
    /// (<see cref="SetEmotion"/>, <see cref="PlayAction"/>, <see cref="MoveTo"/>)
    /// plus the <see cref="AvatarDirectiveHandler"/> extension hook — so swapping
    /// in a rigged model with facial blendshapes, an Animator, and a NavMeshAgent
    /// later is purely additive.
    /// </summary>
    [AddComponentMenu("AI Avatar/Avatar Character")]
    public class AvatarCharacter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("색으로 감정을 표현할 렌더러 (원통 placeholder)")]
        [SerializeField] private Renderer bodyRenderer;

        [Tooltip("플레이어를 바라볼 때 회전시킬 부위 (없으면 transform 사용)")]
        [SerializeField] private Transform head;

        [Tooltip("바라볼 대상 (보통 메인 카메라 / HMD)")]
        [SerializeField] private Transform lookTarget;

        [Header("Look")]
        [SerializeField] private bool lookAtTarget = true;
        [SerializeField] private float lookSpeed = 4f;

        // Extension handlers found on this object / children.
        private AvatarDirectiveHandler[] handlers;

        private MaterialPropertyBlock mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private Color baseColor = Color.white;
        private bool wantLook;

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            handlers = GetComponentsInChildren<AvatarDirectiveHandler>(true);
            if (lookTarget == null && Camera.main != null)
                lookTarget = Camera.main.transform;
        }

        /// <summary>Apply visual identity from a persona (called on start).</summary>
        public void ApplyPersona(CharacterPersona persona)
        {
            if (persona == null) return;
            baseColor = persona.baseColor;
            SetRendererColor(baseColor);
            if (handlers != null)
                foreach (var h in handlers)
                    if (h) h.OnPersona(persona);
        }

        /// <summary>Apply a turn's directives: emotion, action, movement, gaze.</summary>
        public void ApplyDirectives(AvatarDirectives d)
        {
            if (d == null) return;
            SetEmotion(d.emotion);
            if (!string.IsNullOrEmpty(d.action)) PlayAction(d.action);
            if (d.hasMoveTarget) MoveTo(d.moveTarget);
            wantLook = d.lookAtPlayer;

            if (handlers != null)
                foreach (var h in handlers)
                    if (h) h.Handle(d);
        }

        // --- Named reaction API (placeholder implementations) -----------------

        /// <summary>Tint the cylinder to convey emotion. Replace with blendshapes later.</summary>
        public void SetEmotion(string emotion)
        {
            SetRendererColor(EmotionToColor(emotion, baseColor));
        }

        /// <summary>Future: Animator.SetTrigger(action). For now logs the intent.</summary>
        public void PlayAction(string action)
        {
            Debug.Log($"[AIAvatar] action ▶ {action}");
        }

        /// <summary>Future: NavMeshAgent.SetDestination(target). For now logs it.</summary>
        public void MoveTo(Vector3 target)
        {
            Debug.Log($"[AIAvatar] moveTo ▶ {target}");
        }

        private void Update()
        {
            if (!lookAtTarget || !wantLook || lookTarget == null) return;
            var aim = head != null ? head : transform;
            Vector3 dir = lookTarget.position - aim.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var rot = Quaternion.LookRotation(dir);
            aim.rotation = Quaternion.Slerp(aim.rotation, rot, Time.deltaTime * lookSpeed);
        }

        private void SetRendererColor(Color c)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            bodyRenderer.SetPropertyBlock(mpb);
        }

        private static Color EmotionToColor(string emotion, Color fallback)
        {
            switch ((emotion ?? "").Trim().ToLowerInvariant())
            {
                case "happy":     return new Color(0.45f, 0.85f, 0.45f);
                case "sad":       return new Color(0.40f, 0.55f, 0.85f);
                case "angry":     return new Color(0.90f, 0.35f, 0.35f);
                case "surprised": return new Color(0.95f, 0.85f, 0.35f);
                case "thinking":  return new Color(0.70f, 0.55f, 0.90f);
                case "neutral":   return fallback;
                default:          return fallback;
            }
        }
    }
}
