using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using AIAvatar;
using AIAvatar.EditorTools;

namespace Classroom.Scenario.EditorTools
{
    /// <summary>
    /// 관찰 씬에 이미 배치된 4명의 <see cref="ScenarioCharacter"/> 각각에 면담(1:1 대화)
    /// 기능을 자동으로 붙입니다. 같은 아이가 장면을 연기한 뒤, 다가가면 대화가 시작됩니다.
    ///   • Brain(Mock/Claude 프로바이더) + ConversationController
    ///   • 월드 대화 UI(AIAvatarSetup 의 검증된 패널 재사용) + DialogueBillboard
    ///   • ProximityActivator(다가가면 활성화) + (선택)TTS
    ///   • id 로 매칭되는 Persona 자동 연결 (없으면 자동 생성)
    /// 면담은 관찰 세션이 끝난 뒤에만 열리도록 <see cref="InterviewGate"/> 로 잠급니다.
    /// 메뉴: Tools ▸ Classroom Scenario ▸ Attach Interview to Characters.
    /// </summary>
    public static class InterviewSetup
    {
        private const string PersonaDir = "Assets/AIAvatar/Personas";

        [MenuItem("Tools/Classroom Scenario/Attach Interview to Characters", false, 41)]
        public static void Attach()
        {
            // 1) 페르소나 보장 (없으면 생성)
            KulsPersonaSetup.CreatePersonas();
            var font = AIAvatarFontUtil.GetOrCreateKoreanFont();

            // 2) 씬의 캐릭터 / 감독 수집
            var characters = Object.FindObjectsByType<ScenarioCharacter>(FindObjectsSortMode.None);
            if (characters == null || characters.Length == 0)
            {
                EditorUtility.DisplayDialog("면담 구성",
                    "씬에 ScenarioCharacter 가 없습니다.\n먼저 Tools ▸ Classroom Scenario ▸ Build Observation Scene 을 실행하세요.",
                    "확인");
                return;
            }
            var director = Object.FindFirstObjectByType<ScenarioDirector>();

            var sprites = AIAvatarSetup.LoadUiSprites();
            var gatedActivators = new List<Behaviour>();
            int attached = 0;

            foreach (var ch in characters)
            {
                if (ch == null) continue;

                // 이미 붙어 있으면 건너뜀(중복 방지)
                if (ch.GetComponentInChildren<ConversationController>(true) != null)
                {
                    gatedActivators.AddRange(ch.GetComponents<ProximityActivator>());
                    continue;
                }

                var persona = LoadPersona(ch.Id);
                if (persona == null)
                {
                    Debug.LogWarning($"[Interview] '{ch.Id}' 페르소나를 찾지 못해 건너뜁니다: {PersonaDir}/Persona_{ch.Id}.asset", ch);
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(ch.gameObject, "Attach Interview");

                // ── Brain (프로바이더) ──────────────────────────────────────────
                var brain = new GameObject("Interview Brain");
                brain.transform.SetParent(ch.transform, false);
                var mock = brain.AddComponent<MockConversationProvider>();
                brain.AddComponent<ClaudeConversationProvider>(); // 키 넣고 교체하면 진짜 AI

                // ── 월드 대화 UI (AIAvatarSetup 재사용) ────────────────────────
                var ui = AIAvatarSetup.BuildDialogueUI(ch.transform, font, sprites, out _);
                var canvasGO = ui.GetComponentInParent<Canvas>().gameObject;

                // ── Controller ─────────────────────────────────────────────────
                var controller = brain.AddComponent<ConversationController>();
                SetRef(controller, "persona", persona);
                SetRef(controller, "providerBehaviour", mock);   // 기본 오프라인
                SetRef(controller, "ui", ui);
                SetBool(controller, "startOnEnable", false);     // 다가오면 시작

                // ── TTS (키 없으면 무음) ───────────────────────────────────────
                var audioSrc = ch.gameObject.AddComponent<AudioSource>();
                audioSrc.playOnAwake = false; audioSrc.spatialBlend = 1f;
                audioSrc.minDistance = 1f; audioSrc.maxDistance = 15f;
                var tts = ch.gameObject.AddComponent<RestTextToSpeech>();
                SetRef(tts, "audioSource", audioSrc);
                SetRef(controller, "ttsBehaviour", tts);

                // ── 근접 활성화 ────────────────────────────────────────────────
                var prox = ch.gameObject.AddComponent<ProximityActivator>();
                SetRef(prox, "controller", controller);
                SetRef(prox, "dialogueRoot", canvasGO);
                prox.enabled = false;            // 관찰 끝나기 전엔 잠금(게이트가 켜줌)
                gatedActivators.Add(prox);

                // ── 말풍선처럼 항상 플레이어를 향함 ─────────────────────────────
                var billboard = canvasGO.AddComponent<DialogueBillboard>();
                SetRef(billboard, "anchor", ch.Head);
                SetBool(billboard, "flipFacing", true);

                canvasGO.SetActive(false);
                attached++;
            }

            // 3) 면담 게이트 — 관찰 종료 후에만 열림
            if (director != null && gatedActivators.Count > 0)
            {
                var gate = director.GetComponent<InterviewGate>();
                if (gate == null) gate = director.gameObject.AddComponent<InterviewGate>();
                gate.EditorWire(gatedActivators.ToArray(), true);

                // director.onFinished → gate.Unlock (영구 리스너, 중복 추가 방지)
                if (!HasPersistentCall(director.onFinished, gate, nameof(InterviewGate.Unlock)))
                    UnityEventTools.AddVoidPersistentListener(director.onFinished, gate.Unlock);
            }

            AIAvatarSetup.EnsureEventSystem(); // XR 레이로 버튼 클릭 가능하도록

            EditorSceneMarkDirty();
            Debug.Log(
                $"[Interview] 면담 자동 구성 완료 ✅ ({attached}명에 추가)\n" +
                "• 흐름: [ ! ] 로 관찰 세션 재생 → 끝나면 면담 잠금 해제 → 각 아이에게 다가가면(약 2m) 대화창이 뜨고 시작.\n" +
                "• 백엔드: Mock(오프라인). 진짜 AI 는 각 'Interview Brain' 의 Conversation Controller ▸ Provider Behaviour 를 ClaudeConversationProvider 로 바꾸고 키 설정.\n" +
                "• 프롬프트 수정: Assets/AIAvatar/Personas/Persona_*.asset ▸ System Prompt.");
        }

        [MenuItem("Tools/Classroom Scenario/Interview Provider/Use Claude (real AI)", false, 60)]
        public static void UseClaude() => SwitchProvider(useClaude: true);

        [MenuItem("Tools/Classroom Scenario/Interview Provider/Use Mock (offline)", false, 61)]
        public static void UseMock() => SwitchProvider(useClaude: false);

        // 면담 ConversationController 들의 Provider Behaviour 를 일괄 전환.
        private static void SwitchProvider(bool useClaude)
        {
            var controllers = Object.FindObjectsByType<ConversationController>(FindObjectsSortMode.None);
            int n = 0, noKey = 0;
            foreach (var c in controllers)
            {
                if (c == null || c.GetComponentInParent<ScenarioCharacter>() == null) continue; // 면담 리그만

                MonoBehaviour target = useClaude
                    ? c.GetComponentInChildren<ClaudeConversationProvider>(true)
                    : (MonoBehaviour)c.GetComponentInChildren<MockConversationProvider>(true);
                if (target == null)
                {
                    Debug.LogWarning($"[Interview] {c.name} 에서 {(useClaude ? "Claude" : "Mock")} 프로바이더를 못 찾음.", c);
                    continue;
                }
                SetRef(c, "providerBehaviour", target);
                n++;

                if (useClaude && !ClaudeHasKey((ClaudeConversationProvider)target)) noKey++;
            }
            EditorSceneMarkDirty();

            string msg = $"[Interview] 프로바이더 전환 완료: {(useClaude ? "Claude(실시간 AI)" : "Mock(오프라인)")} — {n}명 적용.";
            if (useClaude && noKey > 0)
                msg += $"\n⚠ {noKey}명은 Claude API 키가 비어 있습니다. 각 Claude Conversation Provider 의 Api Key 칸 " +
                       "또는 Assets/StreamingAssets/anthropic_api_key.txt, 환경변수 ANTHROPIC_API_KEY 중 하나를 설정하세요.";
            Debug.Log(msg);
        }

        // 인스펙터 필드(직렬화)에 키가 들어있는지만 확인(파일/환경변수는 런타임 확인).
        private static bool ClaudeHasKey(ClaudeConversationProvider claude)
        {
            var so = new SerializedObject(claude);
            var p = so.FindProperty("apiKey");
            var proxy = so.FindProperty("proxyUrl");
            bool hasInspectorKey = p != null && !string.IsNullOrEmpty(p.stringValue);
            bool hasProxy = proxy != null && !string.IsNullOrEmpty(proxy.stringValue);
            return hasInspectorKey || hasProxy;
        }

        private static CharacterPersona LoadPersona(string id) =>
            AssetDatabase.LoadAssetAtPath<CharacterPersona>($"{PersonaDir}/Persona_{id}.asset");

        private static bool HasPersistentCall(UnityEngine.Events.UnityEventBase evt, Object target, string method)
        {
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                if (evt.GetPersistentTarget(i) == target && evt.GetPersistentMethodName(i) == method)
                    return true;
            return false;
        }

        private static void EditorSceneMarkDirty()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[Interview] '{property}' 필드를 {target.GetType().Name} 에서 못 찾음."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[Interview] bool '{property}' 필드를 {target.GetType().Name} 에서 못 찾음."); return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
