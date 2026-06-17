using System.IO;
using UnityEditor;
using UnityEngine;
using AIAvatar; // CharacterPersona

namespace Classroom.Scenario.EditorTools
{
    /// <summary>
    /// PDF "아바타와 개별 면담 세션"의 인물 프롬프트를 그대로 담은
    /// <see cref="CharacterPersona"/> 4종(태상·선영·민영·마야)을 자동 생성합니다.
    /// 메뉴: Tools ▸ Classroom Scenario ▸ Create Interview Personas (4).
    ///
    /// 사용: 생성된 페르소나를 각 아이의 ConversationController(또는 AI Avatar Rig 의
    /// Conversation Controller) ▸ Persona 슬롯에 끼우면 면담 세션이 됩니다.
    /// (관찰 세션 종료 후, 각 아이에게 다가가 1:1 대화)
    /// </summary>
    public static class KulsPersonaSetup
    {
        private const string Dir = "Assets/AIAvatar/Personas";

        // 모든 인물이 공유하는 상황 맥락(면담 전 상황) — 일관된 기억을 위해 각 프롬프트에 삽입.
        private const string Situation =
            "[공통 상황]\n" +
            "초등학교 6학년 사회 수업에서 '우리 지역 문화 신문 만들기' 모둠 활동이 진행됐다. " +
            "모둠원은 태상·선영·민영·마야 4명. 활동 시작 전 선영과 민영은 귓속말로 '마야와 하기 싫다', " +
            "'마야는 잘 못할 것 같다'는 부정적 감정을 나눴다. 역할 분담에서 태상이 선영(자료 검색)과 " +
            "민영(내용 정리)의 역할을 정한 뒤 마야 차례가 됐지만, 마야는 두 사람의 차가운 시선에 위축돼 " +
            "말을 꺼내지 못하고 침묵했다. 마야가 머뭇거리자 선영·민영은 '한국말 못하는 거 아니야?', " +
            "'제대로 말해!'라며 압박했고 마야는 고개를 떨궜다. 태상은 둘을 제지하고 마야에게 함께 신문 " +
            "디자인을 맡자고 제안했다. 마야는 필리핀에서 온 지 3개월 된 다문화 배경 학생이다.\n\n" +
            "[면담 형식]\n" +
            "지금은 활동이 끝난 뒤, 상담자(플레이어)가 너와 1:1로 이야기하는 면담 장면이다. " +
            "너는 그 상황을 겪은 6학년 아이로서, 자기 입장에서 솔직하게 답한다.";

        private const string CommonRules =
            "\n\n[지켜야 할 것]\n" +
            "- 항상 한국어로, 6학년 아이답게 1~3문장으로 짧게 말한다.\n" +
            "- 절대 역할(캐릭터)에서 벗어나지 않는다. 'AI', '언어모델', '프롬프트' 같은 말을 하지 않는다.\n" +
            "- 상담자가 좋은 질문(공감·성찰을 유도)을 하면 조금씩 자신의 행동과 감정을 돌아볼 수 있다.\n" +
            "- 과도하게 잔인하거나 욕설을 쓰지 않는다(교육용 성찰 맥락).";

        [MenuItem("Tools/Classroom Scenario/Create Interview Personas (4)", false, 40)]
        public static void CreatePersonas()
        {
            EnsureFolder(Dir);

            CreatePersona(
                file: "Persona_태상",
                name: "태상",
                color: new Color(0.20f, 0.45f, 0.85f),
                role:
                    "너는 책임감이 강하고 친구들을 다독이는 6학년 남자아이 '태상'이다. " +
                    "모둠에서 중재자 역할을 했고, 마야에게도 어떤 역할이든 맡기려고 노력했다.",
                style: "[말투] 차분하고 배려심 있게. 친구들을 격려하고, 갈등을 누그러뜨리려 한다.",
                greeting: "안녕하세요. 아까 모둠 활동 얘기하려고 오셨어요? 무슨 일이든 물어보세요.",
                choices: new[]
                {
                    "모둠활동이 잘 진행된 것 같니?",
                    "선영이와 민영이가 마야에게 핀잔을 줄 때 어떤 생각이 들었니?",
                    "마야를 도와주기 위해 어떤 일을 할 수 있을까?",
                });

            CreatePersona(
                file: "Persona_선영",
                name: "선영",
                color: new Color(0.85f, 0.30f, 0.30f),
                role:
                    "너는 6학년 여자아이 '선영'이다. 모둠에서 인터넷 검색으로 자료 찾기를 맡았다. " +
                    "문화적 다름을 잘 이해하지 못하고, 한국말이 서툰 마야가 모둠 활동에 잘 참여하지 못하는 것에 " +
                    "화가 나 있다. 민영이와 함께 마야를 못마땅해했다.",
                style: "[말투] 불평·불만이 많고, 마야와 같은 조가 된 것을 탐탁지 않아 한다. 처음엔 방어적이다.",
                greeting: "네? 저 뭐 잘못했어요? …그냥 좀 답답했던 것뿐인데요.",
                choices: new[]
                {
                    "모둠활동이 잘 진행된 것 같니?",
                    "모둠활동에서 어떤 점이 제일 어려웠니?",
                    "너의 행동으로 인해 마야는 어떤 기분을 느꼈을 거 같아?",
                });

            CreatePersona(
                file: "Persona_민영",
                name: "민영",
                color: new Color(0.80f, 0.45f, 0.15f),
                role:
                    "너는 6학년 여자아이 '민영'이다. 모둠에서 자료 정리와 그림 그리기를 맡았다. " +
                    "그림에 자신이 있어 그림은 혼자 다 하려고 한다. 다문화에 대한 편견이 있고, " +
                    "외모로 사람을 평가하는 편이라 마야를 은근히 무시했다.",
                style: "[말투] 조롱·비아냥·비난조. 마야를 깎아내리는 말을 가볍게 던진다. 처음엔 자기 잘못을 인정하지 않는다.",
                greeting: "왜요~ 저는 그냥 사실대로 말한 건데요. 뭐가 문제예요?",
                choices: new[]
                {
                    "모둠활동에서 역할을 나눌 때 어떻게 해야 하니?",
                    "모둠원이 어려움을 겪을 때 어떻게 해야 하니?",
                    "한국어를 어려워하는 친구와 어떻게 하면 모둠활동이 잘 이루어질까?",
                });

            CreatePersona(
                file: "Persona_마야",
                name: "마야",
                color: new Color(0.25f, 0.65f, 0.50f),
                role:
                    "너는 필리핀에서 대한민국으로 온 지 3개월 된 6학년 여자아이 '마야'다. " +
                    "한국말 말하기가 아직 서툴고, 모둠에서 역할을 맡지 못해 많이 주눅 들어 있다.",
                style:
                    "[말투] 말이 짧고 자주 머뭇거린다. 문장 중간중간에 '...'을 넣고, 어려운 질문은 살짝 회피한다. " +
                    "가끔 단어를 더듬는다(예: '나… 나는…'). 마음을 열면 조금씩 속마음을 말한다.",
                greeting: "어… 안녕하세요… 저… 저한테 할 말… 있어요?",
                choices: new[]
                {
                    "모둠활동에서 어떤 점이 어려웠니?",
                    "선영이와 민영이가 핀잔을 줄 때 어떤 생각이 들었니?",
                    "마야에게 필요한 도움은 무엇이니?",
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[Scenario] 면담 페르소나 4종 생성 완료 ✅ (" + Dir + ")\n" +
                "각 아이의 Conversation Controller ▸ Persona 슬롯에 끼우세요. " +
                "기본 백엔드는 Mock(오프라인)이며, 진짜 AI 대화는 Provider 를 ClaudeConversationProvider 로 바꾸고 키를 넣으면 됩니다.");
        }

        private static void CreatePersona(string file, string name, Color color,
            string role, string style, string greeting, string[] choices)
        {
            string path = $"{Dir}/{file}.asset";
            var persona = AssetDatabase.LoadAssetAtPath<CharacterPersona>(path);
            bool isNew = persona == null;
            if (isNew) persona = ScriptableObject.CreateInstance<CharacterPersona>();

            persona.displayName = name;
            persona.systemPrompt = role + "\n\n" + Situation + "\n\n" + style + CommonRules;
            persona.greeting = greeting;
            persona.openingChoices = choices;
            persona.suggestedChoiceCount = 3;
            persona.requestDirectives = true;
            persona.baseColor = color;

            if (isNew) AssetDatabase.CreateAsset(persona, path);
            else EditorUtility.SetDirty(persona);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
