using System;
using System.Collections.Generic;
using UnityEngine;

namespace Classroom.Scenario
{
    /// <summary>
    /// 한 대사 줄(beat)에 곁들이는 간단한 연기 동작. 모델이 placeholder여도 동작 훅은
    /// 모두 존재하므로, 나중에 Animator/블렌드셰이프가 달린 모델로 바꿔 끼우면
    /// <see cref="ScenarioCharacter.PlayGesture"/>만 채우면 됩니다.
    /// </summary>
    public enum Gesture
    {
        None,        // 특별한 동작 없음
        LookAtTarget,// lookAtId 대상을 쳐다봄(돌아보기)
        Whisper,     // 귓속말 — lookAtId 쪽으로 기울이고 말풍선이 작아짐
        Thought,     // 혼잣말/속마음 — 생각 말풍선
        HeadDown,    // 고개를 떨굼
        Shrug,       // 기죽음/움츠림
        LookAround,  // 주위를 둘러봄
        Stammer,     // 말을 더듬음
    }

    /// <summary>
    /// 말풍선의 표시 스타일. <see cref="Gesture"/>에서 파생되어 <see cref="SpeechBubble"/>이 사용.
    /// </summary>
    public enum BubbleStyle { Normal, Whisper, Thought }

    /// <summary>
    /// 시나리오의 한 줄: 누가 / 무슨 말을 / 어떤 동작·감정으로 하는지.
    /// <see cref="ScenarioDirector"/>가 위에서 아래로 순차 재생합니다.
    /// </summary>
    [Serializable]
    public class ScenarioLine
    {
        [Tooltip("말하는 인물의 id (CastMember.id / ScenarioCharacter.Id 와 일치)")]
        public string speakerId;

        [TextArea(1, 4)]
        [Tooltip("말풍선에 표시될 대사")]
        public string text;

        [Tooltip("말하는 사람이 쳐다볼 대상 id (비우면 모둠 중앙을 봄). 귓속말 상대이기도 함")]
        public string lookAtId;

        [Tooltip("이 줄에 곁들일 간단한 연기 동작")]
        public Gesture gesture = Gesture.None;

        [Tooltip("감정 색/표정: neutral, happy, sad, angry, surprised, thinking")]
        public string emotion = "neutral";

        [Tooltip("이 줄을 화면에 더 오래 머물게 할 추가 시간(초). 글자 수 기반 기본 시간에 더해짐")]
        public float extraHold = 0f;

        public BubbleStyle Style => gesture switch
        {
            Gesture.Whisper => BubbleStyle.Whisper,
            Gesture.Thought => BubbleStyle.Thought,
            _ => BubbleStyle.Normal,
        };
    }

    /// <summary>한 명의 등장인물 정의(표시 이름 + 말풍선 색).</summary>
    [Serializable]
    public class CastMember
    {
        [Tooltip("내부 식별자 (ScenarioLine.speakerId 가 가리킴)")]
        public string id;

        [Tooltip("말풍선에 표시할 이름")]
        public string displayName;

        [Tooltip("이름표/말풍선 강조 색")]
        public Color color = new(0.20f, 0.28f, 0.45f);
    }

    /// <summary>
    /// 작성된 관찰 세션 스크립트. 메뉴 Assets ▸ Create ▸ Classroom ▸ Scenario Script 로
    /// 만들거나, Tools ▸ Classroom Scenario ▸ Build Observation Scene 이 자동 생성합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Classroom/Scenario Script", fileName = "ClassroomScenario")]
    public class ScenarioScript : ScriptableObject
    {
        [Tooltip("장면 제목 (로그/디버그용)")]
        public string title = "관찰 세션";

        [Tooltip("등장인물 명단")]
        public List<CastMember> cast = new();

        [Tooltip("순차 재생될 대사 목록")]
        public List<ScenarioLine> lines = new();

        public CastMember FindCast(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return cast.Find(c => c != null && c.id == id);
        }
    }
}
