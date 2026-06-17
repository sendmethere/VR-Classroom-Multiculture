using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// An authored branching conversation. Create via
    /// Assets ▸ Create ▸ AI Avatar ▸ Dialogue Tree. Walked by
    /// <see cref="DialogueTreeProvider"/>, which can hand off to a free-form AI
    /// provider at any node flagged <see cref="DialogueNode.handoffToAI"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "AI Avatar/Dialogue Tree", fileName = "NewDialogueTree")]
    public class DialogueTree : ScriptableObject
    {
        public string rootNodeId = "root";
        public List<DialogueNode> nodes = new();

        public DialogueNode Find(string id) =>
            nodes.Find(n => n != null && n.id == id);
    }

    [Serializable]
    public class DialogueNode
    {
        [Tooltip("노드 식별자 (선택지의 nextNodeId가 가리킴)")]
        public string id = "root";

        [TextArea(2, 5)] public string avatarLine;
        public string emotion = "neutral";
        public string action;

        public List<DialogueChoice> choices = new();

        [Header("AI Handoff")]
        [Tooltip("이 노드에 도달하면 자유 AI 대화로 전환 (이후 Tree 대신 AI가 응답)")]
        public bool handoffToAI;

        [TextArea(2, 4)]
        [Tooltip("AI로 넘어갈 때 참고시킬 추가 맥락 (현재 프로토타입에서는 메모용, 확장 지점)")]
        public string aiContextSeed;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string text;

        [Tooltip("선택 시 이동할 노드 id (비우면 대화 종료)")]
        public string nextNodeId;
    }
}
