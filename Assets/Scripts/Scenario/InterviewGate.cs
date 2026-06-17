using UnityEngine;

namespace Classroom.Scenario
{
    /// <summary>
    /// 면담(근접 대화)이 <b>관찰 세션이 끝난 뒤에만</b> 열리도록 잠그는 게이트.
    /// 시작 시 등록된 컴포넌트(보통 각 캐릭터의 ProximityActivator)를 모두 꺼두고,
    /// <see cref="ScenarioDirector.onFinished"/> 에서 <see cref="Unlock"/> 가 호출되면 켭니다.
    /// 이렇게 하지 않으면 플레이어가 [ ! ] 로 다가갈 때 가까운 아이의 대화창이
    /// 장면 중간에 떠버립니다.
    /// </summary>
    [AddComponentMenu("Classroom Scenario/Interview Gate")]
    public class InterviewGate : MonoBehaviour
    {
        [Tooltip("관찰 세션이 끝나기 전까지 꺼둘 컴포넌트들 (각 캐릭터의 ProximityActivator)")]
        [SerializeField] private Behaviour[] activators;

        [Tooltip("시작 시 잠글지 (관찰 먼저, 면담 나중)")]
        [SerializeField] private bool lockedAtStart = true;

        public bool IsLocked { get; private set; }

        private void Awake()
        {
            if (lockedAtStart) SetLocked(true);
        }

        /// <summary>면담 잠금 해제 — Director.onFinished 에 연결.</summary>
        public void Unlock() => SetLocked(false);

        /// <summary>다시 잠그기(필요 시).</summary>
        public void Lock() => SetLocked(true);

        private void SetLocked(bool locked)
        {
            IsLocked = locked;
            if (activators == null) return;
            foreach (var a in activators)
                if (a != null) a.enabled = !locked;
        }

#if UNITY_EDITOR
        public void EditorWire(Behaviour[] gatedActivators, bool startLocked)
        {
            activators = gatedActivators;
            lockedAtStart = startLocked;
        }
#endif
    }
}
