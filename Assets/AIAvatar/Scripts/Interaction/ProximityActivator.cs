using UnityEngine;
using UnityEngine.Events;

namespace AIAvatar
{
    /// <summary>
    /// Shows the dialogue UI and (optionally) starts the conversation when the
    /// player gets close to the avatar, hiding it again when they walk away.
    /// Distance is measured horizontally (ignores height). Put this on the avatar.
    /// The <see cref="onActivated"/>/<see cref="onDeactivated"/> events are wired
    /// for future hooks (greeting animation, turning to face the player, SFX…).
    /// </summary>
    [AddComponentMenu("AI Avatar/Proximity Activator")]
    public class ProximityActivator : MonoBehaviour
    {
        [Tooltip("플레이어(보통 HMD/메인 카메라). 비우면 런타임에 Camera.main 사용")]
        [SerializeField] private Transform player;

        [Tooltip("이 거리 안으로 들어오면 활성화 (m)")]
        [SerializeField] private float activateDistance = 2.0f;

        [Tooltip("벗어남 판정에 더하는 여유 거리 (깜빡임 방지)")]
        [SerializeField] private float deactivateBuffer = 0.6f;

        [Tooltip("켜고 끌 대화 UI 루트 (보통 Dialogue Canvas)")]
        [SerializeField] private GameObject dialogueRoot;

        [SerializeField] private ConversationController controller;

        [Tooltip("활성화될 때 대화를 시작할지")]
        [SerializeField] private bool startConversationOnActivate = true;

        [Tooltip("다시 다가올 때마다 대화를 처음부터 재시작할지 (false면 이어서 표시)")]
        [SerializeField] private bool restartOnReturn = false;

        public UnityEvent onActivated;
        public UnityEvent onDeactivated;

        public bool IsActive { get; private set; }
        private bool started;

        private void Awake()
        {
            if (dialogueRoot != null) dialogueRoot.SetActive(false);
        }

        private void Update()
        {
            if (player == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                player = cam.transform;
            }

            float d = HorizontalDistance(player.position, transform.position);
            if (!IsActive && d <= activateDistance) Activate();
            else if (IsActive && d >= activateDistance + deactivateBuffer) Deactivate();
        }

        public void Activate()
        {
            IsActive = true;
            if (dialogueRoot != null) dialogueRoot.SetActive(true);
            if (startConversationOnActivate && controller != null && (!started || restartOnReturn))
            {
                started = true;
                _ = controller.StartConversationAsync();
            }
            onActivated?.Invoke();
        }

        public void Deactivate()
        {
            IsActive = false;
            if (controller != null) controller.StopSpeaking();
            if (dialogueRoot != null) dialogueRoot.SetActive(false);
            onDeactivated?.Invoke();
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, activateDistance);
        }
    }
}
