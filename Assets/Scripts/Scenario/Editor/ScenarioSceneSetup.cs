using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using AIAvatar.EditorTools; // 한국어 폰트 유틸 재사용

namespace Classroom.Scenario.EditorTools
{
    /// <summary>
    /// 원클릭 관찰 세션 구성기.
    /// Tools ▸ Classroom Scenario ▸ Build Observation Scene 을 실행하면:
    ///   • PDF Scene 2 대사를 담은 <see cref="ScenarioScript"/> 에셋을 생성/로드하고
    ///   • 모둠(4명)을 책상 둘레에 자동 배치하고(placeholder 캡슐 + 머리 + 말풍선),
    ///   • <see cref="ScenarioDirector"/> 를 스크립트/캐릭터에 연결하고,
    ///   • 시작용 [ ! ] 트리거를 만들어 Director 에 연결하고,
    ///   • 한국어 TMP 폰트를 보장합니다.
    /// 남는 수동 작업은 README.md 에 적습니다.
    /// </summary>
    public static class ScenarioSceneSetup
    {
        private const string ScriptAssetDir = "Assets/Scripts/Scenario";
        private const string ScriptAssetPath = ScriptAssetDir + "/ClassroomScenario.asset";
        private const string ReadmePath = ScriptAssetDir + "/README.md";

        // 모둠 중앙(월드). 플레이어가 원점 부근에서 +Z 로 다가온다고 가정.
        private static readonly Vector3 GroupCenter = new(0f, 0f, 3f);
        private const float SeatRadius = 0.95f; // 책상 둘레 반경
        private const float BodyHeight = 1.24f; // 6학년 ~1.2m placeholder

        [MenuItem("Tools/Classroom Scenario/Build Observation Scene", false, 0)]
        public static void BuildScene()
        {
            var font = AIAvatarFontUtil.GetOrCreateKoreanFont();
            var script = CreateOrLoadScript();

            var root = new GameObject("Classroom Scenario");
            Undo.RegisterCreatedObjectUndo(root, "Build Observation Scene");

            // 모둠 중앙 마커(화자가 lookAt 대상 없을 때 바라봄)
            var center = new GameObject("Group Center").transform;
            center.SetParent(root.transform, false);
            center.position = GroupCenter + Vector3.up * 1.2f;

            // (선택) 책상 placeholder
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Desk (placeholder)";
            desk.transform.SetParent(root.transform, false);
            desk.transform.position = GroupCenter + Vector3.up * 0.35f;
            desk.transform.localScale = new Vector3(1.1f, 0.7f, 1.1f);
            Paint(desk, new Color(0.55f, 0.42f, 0.3f));

            // 4개의 자리 (중앙을 바라보도록) — id 는 PDF 인물명 그대로
            var seats = new (string id, Vector3 offset)[]
            {
                ("마야",  new Vector3(0f, 0f, -SeatRadius)), // 플레이어 쪽
                ("태상",  new Vector3(0f, 0f,  SeatRadius)),
                ("선영",  new Vector3(-SeatRadius, 0f, 0f)),
                ("민영",  new Vector3( SeatRadius, 0f, 0f)),
            };

            var characters = new List<ScenarioCharacter>();
            var groupGO = new GameObject("Group").transform;
            groupGO.SetParent(root.transform, false);

            foreach (var seat in seats)
            {
                var cast = script.FindCast(seat.id);
                Color col = cast != null ? cast.color : Color.gray;
                var ch = BuildCharacter(groupGO, seat.id, GroupCenter + seat.offset, GroupCenter, col, font);
                characters.Add(ch);
            }

            // Director
            var director = root.AddComponent<ScenarioDirector>();
            director.EditorWire(script, characters, center);

            // [ ! ] 트리거 — 플레이어와 모둠 사이
            var trigger = BuildTrigger(root.transform, director, font,
                GroupCenter + new Vector3(0f, 0f, -SeatRadius - 1.1f));

            EditorUtility.SetDirty(root);
            WriteReadme();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(script);

            Debug.Log(
                "[Scenario] 관찰 세션 생성 완료 ✅\n" +
                "• 시작: 씬을 Play 한 뒤 [ ! ] 오브젝트를 컨트롤러 레이로 선택(또는 PC에서 클릭)하면 장면이 시작됩니다.\n" +
                "• 대사: Assets/Scripts/Scenario/ClassroomScenario.asset (Inspector 에서 수정 가능).\n" +
                "• 캐릭터 교체: 각 'Character (이름)' ▸ 'Model' 아래 placeholder 를 지우고 여러분 모델을 넣으세요. " +
                "필요하면 ScenarioCharacter 의 Head 를 모델 머리 본으로 연결.\n" +
                "• 남은 수동 작업/세부 지침: " + ReadmePath);
        }

        [MenuItem("Tools/Classroom Scenario/Create Scenario Script Only", false, 20)]
        public static void CreateScriptOnly()
        {
            var s = CreateOrLoadScript();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        // ── 캐릭터(자리) 빌드 ──────────────────────────────────────────────────

        private static ScenarioCharacter BuildCharacter(Transform parent, string id, Vector3 pos,
            Vector3 faceTarget, Color color, TMP_FontAsset font)
        {
            var go = new GameObject($"Character ({id})");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            Vector3 look = faceTarget - pos; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f)
                go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            var character = go.AddComponent<ScenarioCharacter>();

            // Model 루트 ─ 여기 아래를 통째로 교체
            var model = new GameObject("Model").transform;
            model.SetParent(go.transform, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body (placeholder)";
            body.transform.SetParent(model, false);
            body.transform.localScale = new Vector3(0.34f, BodyHeight / 2f, 0.34f);
            body.transform.localPosition = new Vector3(0f, BodyHeight / 2f, 0f);
            Paint(body, color);

            // Head(빈 오브젝트) — 고개 숙임/말풍선 기준. 아래에 머리 메시.
            var head = new GameObject("Head").transform;
            head.SetParent(model, false);
            head.localPosition = new Vector3(0f, BodyHeight + 0.12f, 0f);
            var headMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headMesh.name = "Head Mesh";
            headMesh.transform.SetParent(head, false);
            headMesh.transform.localScale = Vector3.one * 0.3f;
            Paint(headMesh, Color.Lerp(color, Color.white, 0.35f));

            // 말풍선
            var bubble = BuildBubble(go.transform, head.localPosition.y + 0.55f, font);

            character.EditorWire(id, model, head, bubble);
            return character;
        }

        // ── 말풍선 빌드 ────────────────────────────────────────────────────────

        private static SpeechBubble BuildBubble(Transform parent, float localY, TMP_FontAsset font)
        {
            var canvasGO = new GameObject("Speech Bubble",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGO.transform.SetParent(parent, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360f, 200f);
            rt.localPosition = new Vector3(0f, localY, 0f);
            rt.localScale = Vector3.one * 0.0026f; // ~0.94m x 0.52m

            var bubble = canvasGO.AddComponent<SpeechBubble>();
            var group = canvasGO.GetComponent<CanvasGroup>();

            // 배경 패널
            var panel = NewRect("Panel", rt);
            Stretch(panel);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.95f);
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 16, 16);
            vlg.spacing = 6;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            // 꼬리(아래로 향하는 작은 삼각형 대용 사각형)
            var tail = NewRect("Tail", rt);
            tail.sizeDelta = new Vector2(36f, 36f);
            tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0f);
            tail.pivot = new Vector2(0.5f, 1f);
            tail.anchoredPosition = new Vector2(0f, 6f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var tailImg = tail.gameObject.AddComponent<Image>();
            tailImg.color = bg.color;

            // 이름
            var nameLabel = NewText("Name", panel, "이름", 26, TextAlignmentOptions.Left, font);
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.color = new Color(0.2f, 0.28f, 0.45f);
            AddLayoutElement(nameLabel.gameObject, preferredHeight: 30);

            // 본문
            var bodyLabel = NewText("Body", panel, "...", 24, TextAlignmentOptions.TopLeft, font);
            bodyLabel.color = new Color(0.12f, 0.13f, 0.16f);
            AddLayoutElement(bodyLabel.gameObject, flexibleHeight: 1, minHeight: 90);

            SetRef(bubble, "group", group);
            SetRef(bubble, "nameLabel", nameLabel);
            SetRef(bubble, "bodyLabel", bodyLabel);
            SetRef(bubble, "background", bg);

            group.alpha = 0f;
            return bubble;
        }

        // ── [ ! ] 트리거 빌드 ──────────────────────────────────────────────────

        private static ScenarioTrigger BuildTrigger(Transform parent, ScenarioDirector director,
            TMP_FontAsset font, Vector3 pos)
        {
            var root = new GameObject("[ ! ] Start Trigger");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var trigger = root.AddComponent<ScenarioTrigger>();

            // 상호작용 콜라이더 + 인터랙터블은 ScenarioTrigger 와 같은 오브젝트(루트)에:
            //   - selectEntered 는 GetComponentInChildren 로 자동 구독
            //   - OnMouseDown 은 콜라이더와 같은 GameObject 에만 전달되므로 여기 있어야 함
            var col = root.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 1.3f, 0f);
            col.radius = 0.45f;
            root.AddComponent<XRSimpleInteractable>();

            // 비주얼(숨김 대상)
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.3f, 0f);

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Glow";
            glow.transform.SetParent(visual.transform, false);
            glow.transform.localScale = Vector3.one * 0.34f;
            var glowMat = Paint(glow, new Color(1f, 0.82f, 0.2f));
            if (glowMat.HasProperty("_EmissionColor"))
            {
                glowMat.EnableKeyword("_EMISSION");
                glowMat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.1f) * 1.5f);
            }

            // 느낌표 3D 텍스트
            var markGO = new GameObject("Mark", typeof(RectTransform));
            markGO.transform.SetParent(visual.transform, false);
            markGO.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            markGO.transform.localScale = Vector3.one * 0.12f;
            var mark = markGO.AddComponent<TextMeshPro>();
            mark.text = "!";
            mark.fontSize = 12;
            mark.alignment = TextAlignmentOptions.Center;
            mark.color = new Color(0.85f, 0.12f, 0.12f);
            mark.fontStyle = FontStyles.Bold;
            mark.enableWordWrapping = false;
            if (font != null) mark.font = font;
            var markRT = mark.GetComponent<RectTransform>();
            if (markRT != null) markRT.sizeDelta = new Vector2(6f, 6f);

            trigger.EditorWire(director, visual, visual.transform);
            return trigger;
        }

        // ── 시나리오 스크립트 에셋(PDF Scene 2) ────────────────────────────────

        private static ScenarioScript CreateOrLoadScript()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScenarioScript>(ScriptAssetPath);
            if (existing != null) return existing;

            EnsureFolder(ScriptAssetDir);
            var s = ScriptableObject.CreateInstance<ScenarioScript>();
            s.title = "다문화 차별 상황 — 역할 정하기 (관찰 세션)";
            s.cast = new List<CastMember>
            {
                new() { id = "태상", displayName = "태상", color = new Color(0.20f, 0.45f, 0.85f) },
                new() { id = "선영", displayName = "선영", color = new Color(0.85f, 0.30f, 0.30f) },
                new() { id = "민영", displayName = "민영", color = new Color(0.80f, 0.45f, 0.15f) },
                new() { id = "마야", displayName = "마야", color = new Color(0.25f, 0.65f, 0.50f) },
            };
            s.lines = new List<ScenarioLine>
            {
                L("선영", "아.. 나 마야랑 하기 싫었는데 ㅠㅠ", lookAt: "민영", g: Gesture.Whisper, emo: "sad"),
                L("민영", "나도… 마야는 왠지 잘 못할 것 같아", lookAt: "마야", g: Gesture.Whisper),
                L("태상", "오늘 우리는 우리 지역의 문화를 알리는 신문을 만들어야 해! 각자 어떤 역할을 하면 좋겠어?", emo: "happy"),
                L("민영", "먼저 서로 잘하는 것에 대해 말해볼까?"),
                L("선영", "좋아, 나는 인터넷 검색으로 자료 찾기를 잘해!", emo: "happy"),
                L("민영", "나는 내용을 정리하고 표 만드는 걸 잘해"),
                L("태상", "좋아. 그럼 선영이는 자료 검색을 맡고, 민영이는 내용 정리해주는 역할을 맡아줘. 나는 디자인을 맡을게. 마야는?", lookAt: "마야"),
                L("마야", "..........", g: Gesture.Shrug, emo: "sad", hold: 0.6f),
                L("태상", "마야?", lookAt: "마야"),
                L("마야", "선영이랑 민영이가 날 반기지 않는 것 같아.. 뭘 잘해야 한다고 할까?", g: Gesture.Thought, emo: "thinking"),
                L("선영", "마야. 우리 말 이해하는 거지? 듣고 있지?", lookAt: "마야", g: Gesture.LookAtTarget),
                L("민영", "마야 한국말 못하는 거 아니야?", lookAt: "마야", g: Gesture.LookAtTarget),
                L("태상", "그러지 마. 마야는 한국말 잘 할 수 있어. 혹시 이해하지 못하는 부분이 있으면 알려줘"),
                L("마야", "나… 나.는.. 나는…", g: Gesture.Stammer, emo: "sad"),
                L("선영", "제대로 말을 해! 뭘 잘한다는 거야?", lookAt: "마야", g: Gesture.LookAtTarget, emo: "angry"),
                L("마야", "…", g: Gesture.HeadDown, emo: "sad", hold: 0.8f),
                L("태상", "그럼 마야, 너는 나와 같이 신문 디자인 만들기로 하자!", lookAt: "마야", emo: "happy"),
            };
            AssetDatabase.CreateAsset(s, ScriptAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Scenario] 시나리오 스크립트 생성: " + ScriptAssetPath);
            return s;
        }

        private static ScenarioLine L(string speaker, string text, string lookAt = null,
            Gesture g = Gesture.None, string emo = "neutral", float hold = 0f) => new()
        {
            speakerId = speaker, text = text, lookAtId = lookAt, gesture = g, emotion = emo, extraHold = hold
        };

        // ── README ─────────────────────────────────────────────────────────────

        private static void WriteReadme()
        {
            const string md =
@"# 관찰 세션 (VR Classroom Scenario)

PDF `26-VR classroom scenario (KULS).pdf` 의 **관찰 세션(Scene 1~2)** 을 구현한 시스템입니다.
아이들이 모둠 활동에서 역할을 정하는 장면을, 플레이어가 옆에서 **말풍선**으로 지켜봅니다.

## 한 번에 생성하기
메뉴 **Tools ▸ Classroom Scenario ▸ Build Observation Scene** 실행 → 모둠 4명 + 말풍선 +
감독(Director) + [ ! ] 시작 트리거가 **자동 배치**됩니다. (한국어 폰트도 자동 보장)

## 시작 방법
씬을 **Play** → 떠다니는 **[ ! ]** 오브젝트를 **컨트롤러 레이로 선택**(또는 PC에서 마우스 클릭)
→ 대사가 **순차적으로** 말풍선에 나타나고, 듣는 아이들은 말하는 쪽으로 **고개를 돌립니다**.

## 캐릭터 교체 (오브젝트는 그대로, 캐릭터만 바꿔 끼우기)
각 `Character (이름)` 오브젝트 구조:
```
Character (선영)        ← ScenarioCharacter (id=""선영"")
 ├─ Model               ← ★ 이 아래 placeholder 를 지우고 여러분 모델을 넣으세요
 │   ├─ Body (placeholder)   (캡슐)
 │   └─ Head                  (머리 기준 빈 오브젝트 + Head Mesh 구)
 └─ Speech Bubble       ← 말풍선 (그대로 두면 됨)
```
### 절차 (한 명당 1~2분)
1. 모델 임포트: 리깅된 휴머노이드 모델(.fbx/.glb, Mixamo·Ready Player Me 등)을 `Assets` 로 드래그.
   - Rig 탭에서 **Animation Type = Humanoid** 권장(나중에 애니메이션 붙이기 쉬움).
2. Hierarchy 에서 `Character (이름) ▸ Model` 을 펼친다.
3. 내 모델을 **`Model` 아래로** 드래그해 자식으로 넣는다. Transform 을 **Reset**(localPosition 0, rotation 0).
   - 발이 바닥(y=0)에 오고 정면이 +Z(앞)를 보도록 맞춘다. 키는 6학년 ≈ 1.2~1.4m.
4. 기존 placeholder 인 `Body (placeholder)` 와 `Head Mesh`(구) 를 **삭제**.
   - 시선/머리 기준점인 빈 **`Head`** 는 남겨도 되고, 지웠다면 5번에서 다시 지정.
5. `Character (이름)` 선택 → **ScenarioCharacter** 컴포넌트에서:
   - **Head** = 내 모델의 **머리 본**(없으면 머리 높이의 빈 오브젝트). 비우면 고개 떨굼만 생략.
   - **Model** = 방금 넣은 모델 루트(보통 이미 연결됨).
   - **Id** 는 이미 `선영/민영/태상/마야` 로 설정됨 — 바꾸지 말 것(대사 매칭 키).
6. `Speech Bubble` 의 **Y 위치**를 새 머리 위로 살짝 조정(가려지면 0.1~0.3 올림).
7. (선택) 모델에 **Animator** 가 있으면, `ScenarioCharacter.PlayGesture` 안의 주석
   `GetComponentInChildren<Animator>()?.SetTrigger(...)` 를 켜서 동작을 실제 애니메이션으로 연결.

> 2D(예: Figma) 캐릭터를 쓰려면: Quad/Plane 에 캐릭터 스프라이트 머티리얼을 입혀 `Model` 아래에 두고,
> 말풍선과 동일하게 카메라를 보게 하면 됩니다(빌보드). 3D 모델과 절차는 동일.

> 새 인물을 추가하려면 `ClassroomScenario.asset` 의 **Cast** 와 **Lines**, 그리고
> `Scenario Director` 의 **Characters** 목록에 같은 id 로 추가하세요.

## 대사 수정
`Assets/Scripts/Scenario/ClassroomScenario.asset` 을 Inspector 에서 편집.
각 줄: 화자(speakerId) / 대사(text) / 쳐다볼 대상(lookAtId) / 동작(gesture) / 감정(emotion) / 추가시간(extraHold).

## 진행/연출 조절 (Classroom Scenario ▸ Scenario Director)
- **Auto Advance**: 시간이 지나면 자동으로 다음 줄(관찰용 기본 ON).
- **Allow Keyboard Advance**: PC 테스트 시 Space/Enter/클릭으로 다음 줄.
- **Base Read Time / Per Char Time**: 줄 표시 시간(기본 + 글자당).

## 남는 수동 작업 (필요 시)
- 씬에 **XR Origin(또는 Main Camera)** 이 있어야 말풍선이 플레이어를 바라보고, 레이 선택이 됩니다.
  (기존 SampleScene 에 XR Rig 가 있으면 그대로 사용)
- 컨트롤러 레이로 [ ! ] 를 **선택(Select)** 하려면 Ray Interactor 가 있어야 합니다.
  XR이 없으면 마우스 클릭으로 테스트하세요(콜라이더가 이미 있음).
- 배치 위치를 옮기려면 `Classroom Scenario` 루트를 통째로 이동하면 됩니다.

## 개별 면담 세션 (관찰 후 1:1 대화) — 원클릭
1. **Tools ▸ Classroom Scenario ▸ Attach Interview to Characters** 실행
   → 4명 각각에 ConversationController + 대화 UI + 근접 활성화 + 매칭 Persona 가 자동 연결됩니다.
   (Persona 없으면 자동 생성)
2. 면담은 **관찰 세션이 끝난 뒤에만** 열립니다(InterviewGate). [ ! ] 로 장면을 끝내면 잠금 해제.
3. 각 아이에게 약 2m 안으로 다가가면 대화창이 떠 면담 시작.
4. 기본 Mock(오프라인). 진짜 AI 는 Conversation Controller ▸ Provider Behaviour 를 Claude 로 교체 + 키 설정.
5. 프롬프트 수정: Assets/AIAvatar/Personas/Persona_*.asset ▸ System Prompt.
";
            try
            {
                File.WriteAllText(ReadmePath, md, new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(ReadmePath);
            }
            catch (System.Exception e) { Debug.LogWarning("[Scenario] README 작성 실패: " + e.Message); }
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────────

        private static Material Paint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            // URP/Built-in 모두에서 동작하도록 현재 머티리얼 인스턴스의 색을 설정
            var mat = r.sharedMaterial != null ? new Material(r.sharedMaterial) : new Material(Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            r.sharedMaterial = mat;
            return mat;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text,
            float size, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align;
            t.enableWordWrapping = true; t.richText = true;
            if (font != null) t.font = font;
            return t;
        }

        private static void AddLayoutElement(GameObject go, float preferredHeight = -1,
            float flexibleHeight = -1, float minHeight = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            if (minHeight >= 0) le.minHeight = minHeight;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[Scenario] '{property}' 직렬화 필드를 {target.GetType().Name} 에서 찾지 못했습니다.");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
