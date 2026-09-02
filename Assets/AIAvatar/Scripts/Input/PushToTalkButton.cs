using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace AIAvatar
{
    /// <summary>
    /// UI "누르고 말하기" 마이크 버튼. 버튼을 누르고 있는 동안(마우스 클릭 또는 XR 레이)
    /// 씬의 <see cref="SpeechInputRelay"/> 를 통해 Whisper 녹음을 시작하고, 떼면 인식합니다.
    /// V 키를 누르고 있는 것과 동일한 효과이며, 녹음 중에는 라벨/색이 바뀌어 상태를 보여줍니다.
    /// 라벨 안에 '(V키로 말하기)' 보조 안내도 표시합니다.
    /// </summary>
    [AddComponentMenu("AI Avatar/Input/Push-To-Talk Button")]
    public class PushToTalkButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("비우면 씬에서 SpeechInputRelay 를 자동 탐색")]
        [SerializeField] private SpeechInputRelay relay;
        [Tooltip("비우면 자식의 TMP_Text 자동 사용")]
        [SerializeField] private TMP_Text label;

        [Header("표시 텍스트 (rich text 지원)")]
        [TextArea] [SerializeField] private string idleText = "● 말하기\n<size=60%>(V키로 말하기)</size>";
        [TextArea] [SerializeField] private string recordingText = "● 녹음 중…\n<size=60%>(떼면 인식)</size>";
        [TextArea] [SerializeField] private string transcribingText = "받아쓰는 중…";
        [SerializeField] private Color recordingColor = new Color(0.85f, 0.15f, 0.15f);
        [SerializeField] private Color transcribingColor = new Color(0.35f, 0.45f, 0.85f);

        private SpeechInputRelay Relay =>
            relay != null ? relay : (relay = FindFirstObjectByType<SpeechInputRelay>());

        private Color idleColor;
        private int lastState = -1; // 0 idle, 1 recording, 2 transcribing

        private void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (label != null) idleColor = label.color; // 원래 색 보존
            Apply(0);
        }

        public void OnPointerDown(PointerEventData e) => Relay?.BeginTalk();
        public void OnPointerUp(PointerEventData e) => Relay?.EndTalk();

        // 버튼이 비활성화되며 PointerUp 을 놓치는 경우를 대비해 녹음을 확실히 종료.
        private void OnDisable()
        {
            if (relay != null) relay.EndTalk();
            Apply(0);
        }

        // 녹음/전사 상태(키보드 V 로 시작된 경우 포함)를 폴링해 라벨/색을 갱신.
        private void Update()
        {
            int state = 0;
            if (Relay != null)
            {
                if (Relay.IsListening) state = 1;
                else if (Relay.IsTranscribing) state = 2;
            }
            Apply(state);
        }

        private void Apply(int state)
        {
            if (state == lastState) return;
            lastState = state;
            if (label == null) return;
            switch (state)
            {
                case 1: label.text = recordingText;    label.color = recordingColor;    break;
                case 2: label.text = transcribingText; label.color = transcribingColor; break;
                default: label.text = idleText;        label.color = idleColor;         break;
            }
        }
    }
}
