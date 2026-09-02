using UnityEngine;
using UnityEngine.UI;

namespace Classroom.Scenario
{
    /// <summary>
    /// 관찰 세션을 건너뛰는 버튼 로직. 세션이 재생 중일 때만 버튼을 보여주고,
    /// 누르면 <see cref="ScenarioDirector.Skip"/> 를 호출해 즉시 면담 단계로 넘어갑니다.
    /// (UI 는 에디터 메뉴 'Add Observation Skip Button' 이 자동 생성/연결)
    /// </summary>
    [AddComponentMenu("Classroom Scenario/Scenario Skip Button")]
    public class ScenarioSkipButton : MonoBehaviour
    {
        [SerializeField] private ScenarioDirector director;
        [Tooltip("보이기/숨기기 대상 (보통 버튼 루트)")]
        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private Button button;

        private void Awake()
        {
            if (director == null) director = FindFirstObjectByType<ScenarioDirector>();
            if (button != null) button.onClick.AddListener(OnClick);
            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (director != null) director.Skip();
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            bool show = director != null && director.IsPlaying;
            var target = buttonRoot != null ? buttonRoot : gameObject;
            if (target.activeSelf != show) target.SetActive(show);
        }

#if UNITY_EDITOR
        public void EditorWire(ScenarioDirector d, GameObject root, Button b)
        {
            director = d; buttonRoot = root; button = b;
        }
#endif
    }
}
