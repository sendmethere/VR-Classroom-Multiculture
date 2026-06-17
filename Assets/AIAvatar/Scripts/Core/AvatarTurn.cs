using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// One response "turn" produced by an <see cref="IConversationProvider"/>.
    /// Carries what the avatar says, the choices offered to the player, and a set
    /// of structured <see cref="AvatarDirectives"/> the character can act on.
    /// This is the single contract every backend (mock / Claude / dialogue tree)
    /// returns, so the UI and character code never care where the turn came from.
    /// </summary>
    [Serializable]
    public class AvatarTurn
    {
        [Tooltip("아바타가 말하는 대사")]
        [TextArea(2, 6)] public string reply;

        [Tooltip("플레이어가 고를 수 있는 응답 후보 (비어 있으면 자유 입력만)")]
        public List<string> choices = new();

        [Tooltip("표정/동작/이동 등 캐릭터 연출 힌트 (확장 지점)")]
        public AvatarDirectives directives = new();

        [Tooltip("true면 대화가 종료됨을 의미")]
        public bool endConversation;

        public static AvatarTurn Say(string reply, params string[] choices)
        {
            var turn = new AvatarTurn { reply = reply };
            if (choices != null) turn.choices.AddRange(choices);
            return turn;
        }
    }

    /// <summary>
    /// Optional, structured hints the avatar can react to. Today only
    /// <see cref="emotion"/> drives the placeholder cylinder's colour, but every
    /// field is here so future systems (facial blendshapes, Animator triggers,
    /// NavMesh movement, head gaze) can be added without changing the provider
    /// contract. Providers fill in whatever they can; consumers ignore what they
    /// don't yet support.
    /// </summary>
    [Serializable]
    public class AvatarDirectives
    {
        [Tooltip("감정: neutral, happy, sad, angry, surprised, thinking ...")]
        public string emotion = "neutral";

        [Tooltip("동작/애니메이션 id: wave, nod, shrug ... (지금은 로그만)")]
        public string action;

        [Tooltip("보조 제스처 id (미래 확장)")]
        public string gesture;

        [Tooltip("이동 목표가 지정됐는지")]
        public bool hasMoveTarget;

        [Tooltip("이동 목표 위치 (미래: NavMesh)")]
        public Vector3 moveTarget;

        [Tooltip("플레이어를 바라볼지")]
        public bool lookAtPlayer = true;
    }
}
