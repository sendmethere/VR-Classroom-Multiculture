using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AIAvatar
{
    /// <summary>
    /// 근접 시 플레이어를 바라볼 수 있는 아바타가 구현하는 훅.
    /// (ScenarioCharacter 가 구현 → 몸을 돌려 눈을 맞춤)
    /// </summary>
    public interface IProximityGazeTarget
    {
        void LookAtPlayer(Transform player);
        void ResetGaze();
    }

    /// <summary>
    /// 플레이어가 가까이 오면 대화 UI를 켜고(선택적으로) 대화를 시작하며, 멀어지면 끕니다.
    /// 거리는 수평(높이 무시)으로 잽니다. 아바타에 부착하세요.
    /// 여러 아바타가 서로 가까이 있어도 <b>전역에서 가장 가까운 한 명만</b> 활성화되어,
    /// 대화창이 동시에 여러 개 뜨지 않습니다. 활성 아바타는 플레이어를 바라봅니다.
    /// </summary>
    [AddComponentMenu("AI Avatar/Proximity Activator")]
    public class ProximityActivator : MonoBehaviour
    {
        [Tooltip("플레이어(보통 HMD/메인 카메라). 비우면 런타임에 Camera.main 사용")]
        [SerializeField] private Transform player;

        [Tooltip("이 거리 안으로 들어오면 활성화 후보 (m)")]
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

        [Tooltip("활성화되면 이 아바타가 플레이어를 바라보게 함(가능한 경우)")]
        [SerializeField] private bool facePlayerWhenActive = true;

        public UnityEvent onActivated;
        public UnityEvent onDeactivated;

        public bool IsActive { get; private set; }

        /// <summary>The conversation this proximity zone drives (for input routing).</summary>
        public ConversationController Controller => controller;

        private bool started;
        private IProximityGazeTarget gaze;

        // 동시에 하나만 활성화되도록 모든 액티베이터를 전역 등록.
        private static readonly List<ProximityActivator> Registry = new();

        private void Awake()
        {
            gaze = GetComponent<IProximityGazeTarget>();
            if (dialogueRoot != null) dialogueRoot.SetActive(false);
        }

        private void OnEnable() { if (!Registry.Contains(this)) Registry.Add(this); }
        private void OnDisable() { Registry.Remove(this); if (IsActive) Deactivate(); }

        private void Update()
        {
            if (player == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                player = cam.transform;
            }

            float dSelf = HorizontalDistance(player.position, transform.position);

            // 전역에서 '활성 범위 안'에 있는 가장 가까운 액티베이터를 찾는다.
            ProximityActivator closest = null;
            float closestDist = float.MaxValue;
            foreach (var a in Registry)
            {
                if (a == null || !a.isActiveAndEnabled) continue;
                float ad = HorizontalDistance(player.position, a.transform.position);
                if (ad <= a.activateDistance && ad < closestDist) { closestDist = ad; closest = a; }
            }

            if (IsActive)
            {
                // 플레이어가 벗어났거나, 다른 아이가 확실히 더 가까우면 비활성화
                if (dSelf >= activateDistance + deactivateBuffer) Deactivate();
                else if (closest != null && closest != this && closestDist < dSelf - 0.2f) Deactivate();
            }
            else if (closest == this)
            {
                Activate();
            }
        }

        public void Activate()
        {
            // 혹시 다른 활성 대화가 있으면 먼저 닫아 항상 하나만 유지.
            foreach (var a in Registry)
                if (a != null && a != this && a.IsActive) a.Deactivate();

            IsActive = true;
            if (dialogueRoot != null) dialogueRoot.SetActive(true);
            if (facePlayerWhenActive && gaze != null && player != null) gaze.LookAtPlayer(player);
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
            if (gaze != null) gaze.ResetGaze();
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
