using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Classroom.Scenario
{
    /// <summary>
    /// 장면 시작용 [ ! ] 오브젝트. 플레이어가 컨트롤러 레이로 선택(상호작용)하면
    /// <see cref="ScenarioDirector.Play"/>를 호출하고, 자기 자신은 숨깁니다.
    /// XR이 없는 PC 환경에서도 테스트할 수 있도록 마우스 클릭(OnMouseDown)도 지원합니다.
    /// 주의를 끌기 위해 위아래로 살짝 떠다니며 회전합니다.
    /// </summary>
    [AddComponentMenu("Classroom Scenario/Scenario Trigger")]
    public class ScenarioTrigger : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private ScenarioDirector director;

        [Tooltip("느낌표 비주얼 루트(트리거 후 숨김 대상). 비우면 이 오브젝트 전체")]
        [SerializeField] private GameObject visual;

        [Tooltip("한 번만 발동 (다시 못 누름)")]
        [SerializeField] private bool oneShot = true;

        [Tooltip("발동 후 비주얼 숨기기")]
        [SerializeField] private bool hideAfterTrigger = true;

        [Header("Idle motion")]
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float spinSpeed = 45f;

        [Header("Billboard label")]
        [Tooltip("느낌표 텍스트가 항상 플레이어를 향하게 함")]
        [SerializeField] private Transform labelToBillboard;

        public UnityEvent onTriggered;

        private bool used;
        private Vector3 visualHome;
        private XRBaseInteractable interactable;

        private void Awake()
        {
            if (visual == null) visual = gameObject;
            visualHome = (labelToBillboard != null ? labelToBillboard : transform).localPosition;

            interactable = GetComponentInChildren<XRBaseInteractable>();
            if (interactable != null)
                interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDestroy()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnSelectEntered(SelectEnterEventArgs _) => Activate();

        // PC(비-VR) 테스트: 콜라이더가 있으면 마우스 클릭으로도 발동.
        private void OnMouseDown() => Activate();

        /// <summary>장면을 시작한다. UI 버튼/다른 이벤트에서 직접 호출해도 됨.</summary>
        public void Activate()
        {
            if (used && oneShot) return;
            used = true;

            if (director != null) director.Play();
            else Debug.LogWarning("[Scenario] Trigger에 Director가 연결되지 않았습니다.", this);

            onTriggered?.Invoke();
            if (hideAfterTrigger && visual != null) visual.SetActive(false);
        }

        private void Update()
        {
            // 살짝 떠다니기 + 회전 (주의 환기)
            var t = labelToBillboard != null ? labelToBillboard : transform;
            float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            t.localPosition = visualHome + Vector3.up * y;

            if (spinSpeed != 0f && labelToBillboard == null)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            // 라벨이 따로 있으면 플레이어를 향하도록
            if (labelToBillboard != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 dir = labelToBillboard.position - cam.transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-5f)
                        labelToBillboard.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }
        }

#if UNITY_EDITOR
        public void EditorWire(ScenarioDirector dir, GameObject visualRoot, Transform label)
        {
            director = dir;
            visual = visualRoot;
            labelToBillboard = label;
        }
#endif
    }
}
