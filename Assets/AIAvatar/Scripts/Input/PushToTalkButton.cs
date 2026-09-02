using UnityEngine;
using UnityEngine.EventSystems;

namespace AIAvatar
{
    /// <summary>
    /// UI "누르고 말하기" 마이크 버튼. 버튼을 누르고 있는 동안(마우스 클릭 또는 XR 레이)
    /// 씬의 <see cref="SpeechInputRelay"/> 를 통해 Whisper 녹음을 시작하고, 떼면 인식합니다.
    /// V 키를 누르고 있는 것과 동일한 효과라, 키보드가 없거나 VR에서 화면 버튼으로도
    /// 음성 입력을 할 수 있습니다. UGUI Button/Image 오브젝트에 붙이세요.
    /// </summary>
    [AddComponentMenu("AI Avatar/Input/Push-To-Talk Button")]
    public class PushToTalkButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("비우면 씬에서 SpeechInputRelay 를 자동 탐색")]
        [SerializeField] private SpeechInputRelay relay;

        private SpeechInputRelay Relay =>
            relay != null ? relay : (relay = FindFirstObjectByType<SpeechInputRelay>());

        public void OnPointerDown(PointerEventData e) => Relay?.BeginTalk();
        public void OnPointerUp(PointerEventData e) => Relay?.EndTalk();

        // 버튼이 비활성화되며 PointerUp 을 놓치는 경우를 대비해 녹음을 확실히 종료.
        private void OnDisable() { if (relay != null) relay.EndTalk(); }
    }
}
