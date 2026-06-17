using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Classroom.Scenario
{
    /// <summary>
    /// 관찰 세션의 "감독". <see cref="ScenarioScript"/>의 대사를 위에서 아래로 한 줄씩
    /// 순차 재생합니다. 매 줄마다:
    ///   1) 말하는 캐릭터를 찾고,
    ///   2) 다른 캐릭터들은 말하는 쪽으로 고개를 돌리고(돌아보기),
    ///   3) 말풍선을 띄운 뒤,
    ///   4) 글자 수 기반 시간만큼 기다렸다가(또는 <see cref="Advance"/> 호출 시 즉시) 다음 줄로.
    /// <see cref="ScenarioTrigger"/>의 [!] 와 상호작용하면 <see cref="Play"/>가 호출됩니다.
    /// </summary>
    [AddComponentMenu("Classroom Scenario/Scenario Director")]
    public class ScenarioDirector : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private ScenarioScript script;
        [Tooltip("등장 캐릭터들. id로 대사와 매칭됨 (셋업이 자동 연결)")]
        [SerializeField] private List<ScenarioCharacter> characters = new();
        [Tooltip("lookAtId가 없을 때 화자가 바라볼 모둠 중앙")]
        [SerializeField] private Transform groupCenter;

        [Header("Timing")]
        [Tooltip("한 줄 기본 표시 시간(초)")]
        [SerializeField] private float baseReadTime = 1.4f;
        [Tooltip("글자당 추가 시간(초)")]
        [SerializeField] private float perCharTime = 0.07f;
        [Tooltip("줄 사이 간격(초)")]
        [SerializeField] private float gapBetweenLines = 0.25f;

        [Header("Advance")]
        [Tooltip("시간이 지나면 자동으로 다음 줄로 (관찰용 권장)")]
        [SerializeField] private bool autoAdvance = true;
        [Tooltip("스페이스/마우스 클릭으로 다음 줄로 넘기기 허용 (PC 테스트용)")]
        [SerializeField] private bool allowKeyboardAdvance = true;

        [Header("Events")]
        public UnityEvent onStarted;
        public UnityEvent onFinished;

        public bool IsPlaying { get; private set; }

        private readonly Dictionary<string, ScenarioCharacter> byId = new();
        private bool advanceRequested;

        private void Awake() => RebuildIndex();

        private void RebuildIndex()
        {
            byId.Clear();
            foreach (var c in characters)
                if (c != null && !string.IsNullOrEmpty(c.Id))
                    byId[c.Id] = c;
        }

        /// <summary>장면 재생 시작. 이미 재생 중이면 무시.</summary>
        public void Play()
        {
            if (IsPlaying) return;
            if (script == null || script.lines == null || script.lines.Count == 0)
            {
                Debug.LogWarning("[Scenario] 재생할 스크립트(대사)가 없습니다.", this);
                return;
            }
            RebuildIndex();
            StartCoroutine(Run());
        }

        /// <summary>현재 줄을 즉시 끝내고 다음 줄로 넘김(또는 타자기 즉시 완성).</summary>
        public void Advance() => advanceRequested = true;

        private IEnumerator Run()
        {
            IsPlaying = true;
            onStarted?.Invoke();

            foreach (var c in characters)
                if (c != null) c.ResetLook();

            ScenarioCharacter previous = null;

            foreach (var line in script.lines)
            {
                if (line == null || string.IsNullOrEmpty(line.speakerId)) continue;

                var speaker = Resolve(line.speakerId);
                if (speaker == null)
                {
                    Debug.LogWarning($"[Scenario] '{line.speakerId}' id의 캐릭터를 찾지 못했습니다. 건너뜁니다.", this);
                    continue;
                }

                Transform lookTarget = ResolveLookTarget(line, speaker);

                // 1) 화자: lookAt 대상(또는 모둠 중앙)을 바라봄 + 동작
                speaker.LookAt(lookTarget);
                speaker.PlayGesture(line.gesture, lookTarget);

                // 2) 청자들: 화자 쪽으로 돌아봄 (귓속말/혼잣말은 시선 고정 안 함)
                if (line.Style == BubbleStyle.Normal)
                    foreach (var other in characters)
                        if (other != null && other != speaker)
                            other.LookAt(speaker.Head);

                // 3) 이전 말풍선 숨기고 현재 말풍선 표시
                if (previous != null && previous != speaker) previous.HideBubble();
                var cast = script.FindCast(line.speakerId);
                string name = cast != null ? cast.displayName : line.speakerId;
                Color color = cast != null ? cast.color : Color.white;
                speaker.Say(name, color, line.text, line.Style);
                previous = speaker;

                // 4) 대기
                float hold = baseReadTime + Length(line.text) * perCharTime + Mathf.Max(0f, line.extraHold);
                yield return WaitLine(speaker, hold);

                if (gapBetweenLines > 0f) yield return new WaitForSeconds(gapBetweenLines);
            }

            if (previous != null) previous.HideBubble();
            foreach (var c in characters)
                if (c != null) c.ResetLook();

            IsPlaying = false;
            onFinished?.Invoke();
        }

        // 한 줄을 기다림: 자동 시간 경과 또는 사용자 입력(있으면)으로 종료.
        private IEnumerator WaitLine(ScenarioCharacter speaker, float hold)
        {
            advanceRequested = false;
            float t = 0f;
            while (true)
            {
                if (PollAdvanceInput() || advanceRequested)
                {
                    advanceRequested = false;
                    // 첫 입력: 타자기 진행 중이면 즉시 완성. 다 나왔으면 다음 줄로.
                    if (speaker.IsTyping) { speaker.CompleteTypewriter(); }
                    else yield break;
                }

                t += Time.deltaTime;
                if (autoAdvance && t >= hold && !speaker.IsTyping) yield break;
                yield return null;
            }
        }

        private bool PollAdvanceInput()
        {
            if (!allowKeyboardAdvance) return false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
#endif
            return false;
        }

        private ScenarioCharacter Resolve(string id) =>
            byId.TryGetValue(id, out var c) ? c : null;

        private Transform ResolveLookTarget(ScenarioLine line, ScenarioCharacter speaker)
        {
            if (!string.IsNullOrEmpty(line.lookAtId))
            {
                var t = Resolve(line.lookAtId);
                if (t != null) return t.Head;
            }
            return groupCenter != null ? groupCenter : null;
        }

        private static int Length(string s) => string.IsNullOrEmpty(s) ? 0 : s.Length;

#if UNITY_EDITOR
        public void EditorWire(ScenarioScript s, List<ScenarioCharacter> cast, Transform center)
        {
            script = s;
            characters = cast;
            groupCenter = center;
        }
#endif
    }
}
