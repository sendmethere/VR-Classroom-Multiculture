using UnityEngine;

namespace Classroom.Scenario
{
    /// <summary>
    /// 시나리오 속 한 명의 "자리". 핵심은 <see cref="modelRoot"/> — 이 아래에 들어있는
    /// placeholder(캡슐+머리)를 지우고 여러분의 리깅된 캐릭터 모델을 끼워 넣으면 됩니다.
    /// (자세한 교체 방법은 Assets/Scripts/Scenario/README.md 참고)
    ///
    /// 외부에는 작은 이름-기반 API만 노출합니다:
    /// <see cref="Say"/>, <see cref="LookAt"/>, <see cref="ResetLook"/>,
    /// <see cref="PlayGesture"/>. 동작 구현은 placeholder 수준이지만(머리 회전 등)
    /// 훅이 모두 있으므로 Animator로 교체하는 것은 순수 추가 작업입니다.
    /// </summary>
    [AddComponentMenu("Classroom Scenario/Scenario Character")]
    public class ScenarioCharacter : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("ScenarioLine.speakerId / lookAtId 와 일치시킬 식별자 (예: 선영)")]
        [SerializeField] private string id;

        [Header("References")]
        [Tooltip("← 여기 아래의 placeholder를 지우고 여러분의 캐릭터 모델을 넣으세요")]
        [SerializeField] private Transform modelRoot;

        [Tooltip("말풍선 위치/시선 회전 기준이 되는 머리 부위. 모델 교체 시 머리 본으로 연결")]
        [SerializeField] private Transform head;

        [Tooltip("이 캐릭터의 말풍선")]
        [SerializeField] private SpeechBubble bubble;

        [Header("Look")]
        [Tooltip("시선/몸 회전 속도")]
        [SerializeField] private float turnSpeed = 6f;

        public string Id => id;
        public Transform Head => head != null ? head : transform;
        public SpeechBubble Bubble => bubble;

        private Quaternion homeRot;       // 시작 자세(모둠 중앙을 보는 방향)
        private Quaternion homeHeadLocal; // 머리 기본 로컬 회전
        private Transform lookTarget;     // 현재 바라보는 대상(null이면 home으로 복귀)
        private float headPitch;          // 고개 숙임 정도(도)
        private float wantHeadPitch;

        private void Awake()
        {
            homeRot = transform.rotation;
            if (head != null) homeHeadLocal = head.localRotation;
        }

        public void SetId(string newId) => id = newId;

        // ── 발화 ────────────────────────────────────────────────────────────
        public void Say(string speakerName, Color color, string text, BubbleStyle style)
        {
            if (bubble != null) bubble.Show(speakerName, color, text, style);
        }

        public void HideBubble()
        {
            if (bubble != null) bubble.Hide();
        }

        public bool IsTyping => bubble != null && bubble.IsTyping;
        public void CompleteTypewriter() { if (bubble != null) bubble.CompleteTypewriter(); }

        // ── 시선/동작 ───────────────────────────────────────────────────────

        /// <summary>대상 쪽으로 몸을 돌려 바라봄(수평 회전).</summary>
        public void LookAt(Transform target) => lookTarget = target;

        /// <summary>시작 자세로 복귀.</summary>
        public void ResetLook()
        {
            lookTarget = null;
            wantHeadPitch = 0f;
        }

        /// <summary>간단한 연기 동작. placeholder에서는 머리/몸 회전으로 표현.</summary>
        public void PlayGesture(Gesture gesture, Transform speakerOrTarget)
        {
            switch (gesture)
            {
                case Gesture.LookAtTarget:
                case Gesture.Whisper:
                    if (speakerOrTarget != null) lookTarget = speakerOrTarget;
                    wantHeadPitch = 0f;
                    break;
                case Gesture.HeadDown:
                    wantHeadPitch = 28f; // 고개를 떨군다
                    break;
                case Gesture.Shrug:
                    wantHeadPitch = 14f; // 기죽음 — 살짝 움츠림
                    break;
                case Gesture.LookAround:
                    lookTarget = null;   // 주위를 둘러봄(자유 회전, 여기선 home 복귀로 단순화)
                    wantHeadPitch = 0f;
                    break;
                case Gesture.Thought:
                case Gesture.Stammer:
                case Gesture.None:
                default:
                    wantHeadPitch = 0f;
                    break;
            }

            // 확장 지점: Animator가 있다면 여기서 SetTrigger(gesture) 호출
            // GetComponentInChildren<Animator>()?.SetTrigger(gesture.ToString());
        }

        private void Update()
        {
            // 몸 회전(돌아보기)
            Quaternion targetRot = homeRot;
            if (lookTarget != null)
            {
                Vector3 dir = lookTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f)
                    targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

            // 고개 숙임
            if (head != null)
            {
                headPitch = Mathf.MoveTowards(headPitch, wantHeadPitch, 90f * Time.deltaTime);
                head.localRotation = homeHeadLocal * Quaternion.Euler(headPitch, 0f, 0f);
            }
        }

#if UNITY_EDITOR
        // 에디터 셋업에서 참조를 연결할 때 사용.
        public void EditorWire(string newId, Transform modelRootRef, Transform headRef, SpeechBubble bubbleRef)
        {
            id = newId;
            modelRoot = modelRootRef;
            head = headRef;
            bubble = bubbleRef;
        }
#endif
    }
}
