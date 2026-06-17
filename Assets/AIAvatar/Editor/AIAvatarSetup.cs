using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AIAvatar.EditorTools
{
    /// <summary>
    /// One-click scene setup. Builds a placeholder cylinder avatar, a world-space
    /// dialogue panel (name + line + choice buttons + free-text input), wires a
    /// <see cref="ConversationController"/> with all three providers (Mock active
    /// by default), and creates sample Persona + DialogueTree assets.
    /// Menu: Tools ▸ AI Avatar ▸ Create Conversational Avatar.
    /// </summary>
    public static class AIAvatarSetup
    {
        private const string AssetRoot = "Assets/AIAvatar";
        private const string PersonaPath = AssetRoot + "/Personas/SamplePersona.asset";
        private const string TreePath = AssetRoot + "/Personas/SampleDialogueTree.asset";

        [MenuItem("Tools/AI Avatar/Create Conversational Avatar", false, 0)]
        public static void CreateAvatar()
        {
            var font = AIAvatarFontUtil.GetOrCreateKoreanFont();
            var persona = CreateOrLoadPersona();
            var tree = CreateOrLoadTree();

            // ── Root ─────────────────────────────────────────────────────────
            var root = new GameObject("AI Avatar Rig");
            Undo.RegisterCreatedObjectUndo(root, "Create AI Avatar");
            root.transform.position = Vector3.zero;

            // ── Cylinder avatar ──────────────────────────────────────────────
            var avatarGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            avatarGO.name = "Avatar (Cylinder)";
            avatarGO.transform.SetParent(root.transform, false);
            avatarGO.transform.localScale = new Vector3(0.3f, 0.8f, 0.3f); // ~1.6m tall
            avatarGO.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var renderer = avatarGO.GetComponent<MeshRenderer>();
            var avatar = avatarGO.AddComponent<AvatarCharacter>();
            SetRef(avatar, "bodyRenderer", renderer);

            // ── Brain (providers) ────────────────────────────────────────────
            var brain = new GameObject("Brain");
            brain.transform.SetParent(root.transform, false);
            var mock = brain.AddComponent<MockConversationProvider>();
            var claude = brain.AddComponent<ClaudeConversationProvider>();
            var treeProvider = brain.AddComponent<DialogueTreeProvider>();
            SetRef(treeProvider, "tree", tree);
            SetRef(treeProvider, "aiProviderBehaviour", mock); // handoff target (switch to claude later)

            // ── World-space dialogue UI ──────────────────────────────────────
            var sprites = LoadUiSprites();
            var ui = BuildDialogueUI(root.transform, font, sprites, out var emotionIcon);

            // Emotion icon handler: lives on the avatar (so AvatarCharacter discovers
            // it) but drives the Image in the canvas. Adding a handler = a new
            // reaction with zero core changes.
            var emoHandler = avatarGO.AddComponent<EmotionIconHandler>();
            emoHandler.target = emotionIcon;
            emoHandler.neutral = sprites.neutral;
            emoHandler.happy = sprites.happy;
            emoHandler.sad = sprites.sad;
            emoHandler.angry = sprites.angry;
            emoHandler.surprised = sprites.surprised;
            emoHandler.thinking = sprites.thinking;

            // ── Controller ───────────────────────────────────────────────────
            var controller = brain.AddComponent<ConversationController>();
            SetRef(controller, "persona", persona);
            SetRef(controller, "providerBehaviour", mock);   // offline by default
            SetRef(controller, "ui", ui);
            SetRef(controller, "avatar", avatar);

            // ── TTS (REST, OpenAI 호환) — 아바타 위치에서 음성 재생 ─────────────
            var audioSrc = avatarGO.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 1f; // 3D
            audioSrc.minDistance = 1f;
            audioSrc.maxDistance = 15f;
            var tts = avatarGO.AddComponent<RestTextToSpeech>();
            SetRef(tts, "audioSource", audioSrc);
            SetRef(controller, "ttsBehaviour", tts);

            // ── 근접 활성화: 다가가면 대화창이 뜨고 대화 시작 ──────────────────
            var canvasGO = ui.GetComponentInParent<Canvas>().gameObject;
            var prox = avatarGO.AddComponent<ProximityActivator>();
            SetRef(prox, "controller", controller);
            SetRef(prox, "dialogueRoot", canvasGO);
            SetSerializedBool(controller, "startOnEnable", false); // 자동 시작 끔 (다가오면 시작)

            // ── 대화창이 항상 플레이어 쪽을 향하도록 (캐릭터 뒤로 안 감) ──────────
            var billboard = canvasGO.AddComponent<DialogueBillboard>();
            SetRef(billboard, "anchor", avatarGO.transform); // target은 런타임에 Camera.main
            SetSerializedBool(billboard, "flipFacing", true);
            SetSerializedFloat(billboard, "lateralOffset", 0.25f);

            canvasGO.SetActive(false);                              // 평소엔 숨김

            EnsureEventSystem();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(persona);

            Debug.Log(
                "[AIAvatar] 생성 완료 ✅\n" +
                "• 근접 활성화: 대화창은 평소 숨겨져 있고, 아바타에 약 2m 안으로 다가가면 뜨면서 대화가 시작됩니다 " +
                "(Avatar의 Proximity Activator에서 거리 조절).\n" +
                "• 기본 백엔드: Mock(오프라인). 선택지+자유입력으로 바로 테스트 가능.\n" +
                "• TTS: Avatar의 'Rest Text To Speech'에 OpenAI 호환 키(또는 StreamingAssets/tts_api_key.txt / 환경변수 OPENAI_API_KEY)를 넣으면 아바타가 말합니다. 키 없으면 무음.\n" +
                "• Claude로 바꾸려면: Brain ▸ Conversation Controller ▸ Provider Behaviour를 'ClaudeConversationProvider'로 교체 + 키 설정.\n" +
                "• 트리+AI 하이브리드: Provider Behaviour를 'DialogueTreeProvider'로 교체.\n" +
                "• 역할/프롬프트 주입: Personas/SamplePersona ▸ System Prompt 편집.\n" +
                "• VR 레이 클릭: 'Tools ▸ AI Avatar ▸ Fix EventSystem for XR UI' 실행 + Ray Interactor의 UI Press Input 바인딩 확인.");
        }

        [MenuItem("Tools/AI Avatar/Create Sample Persona Only", false, 20)]
        public static void CreatePersonaOnly()
        {
            var p = CreateOrLoadPersona();
            Selection.activeObject = p;
            EditorGUIUtility.PingObject(p);
        }

        // ── UI construction ───────────────────────────────────────────────────

        internal static DialogueUI BuildDialogueUI(Transform parent, TMP_FontAsset font,
            UiSprites sprites, out Image emotionIcon)
        {
            // The supplied art is light (translucent white panel/buttons), so when
            // it's present we use dark ink for text; otherwise fall back to a dark
            // panel with white text.
            bool light = sprites.panel != null;
            Color ink = new Color(0.13f, 0.14f, 0.17f, 1f);
            Color textColor = light ? ink : Color.white;
            Color nameColor = light ? new Color(0.16f, 0.28f, 0.55f) : new Color(0.7f, 0.82f, 1f);

            // Canvas (world space)
            var canvasGO = new GameObject("Dialogue Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            canvasGO.transform.SetParent(parent, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null) canvas.worldCamera = Camera.main; // event camera (XR/mouse)
            var canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(640f, 820f);
            canvasRT.localPosition = new Vector3(0f, 1.7f, 0.32f);
            canvasRT.localScale = Vector3.one * 0.0013f; // ~0.83m x 1.07m

            // Background panel with vertical layout
            var panel = NewRect("Panel", canvasRT);
            Stretch(panel);
            var bg = panel.gameObject.AddComponent<Image>();
            if (light) { bg.sprite = sprites.panel; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = new Color(0.08f, 0.09f, 0.13f, 0.92f);
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 28, 28);
            vlg.spacing = 14;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var ui = panel.gameObject.AddComponent<DialogueUI>();

            // Header row: emotion icon + name
            var header = NewRect("Header", panel);
            var hh = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hh.spacing = 12;
            hh.childControlWidth = true; hh.childControlHeight = true;
            hh.childForceExpandWidth = false; hh.childForceExpandHeight = true;
            hh.childAlignment = TextAnchor.MiddleLeft;
            AddLayoutElement(header.gameObject, preferredHeight: 76);

            var iconRT = NewRect("EmotionIcon", header);
            emotionIcon = iconRT.gameObject.AddComponent<Image>();
            emotionIcon.preserveAspect = true;
            emotionIcon.sprite = sprites.neutral;
            emotionIcon.enabled = sprites.neutral != null;
            AddLayoutElement(iconRT.gameObject, preferredWidth: 72);

            var nameLabel = NewText("Name", header, "Avatar", 30, TextAlignmentOptions.Left, font);
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.color = nameColor;
            AddLayoutElement(nameLabel.gameObject, flexibleWidth: 1);

            // Body label
            var bodyLabel = NewText("Body", panel, "...", 26, TextAlignmentOptions.TopLeft, font);
            bodyLabel.color = textColor;
            AddLayoutElement(bodyLabel.gameObject, flexibleHeight: 1, minHeight: 220);

            // Thinking indicator
            var thinking = NewText("Thinking", panel, "…생각 중", 22, TextAlignmentOptions.Left, font);
            thinking.fontStyle = FontStyles.Italic;
            thinking.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
            AddLayoutElement(thinking.gameObject, preferredHeight: 30);
            thinking.gameObject.SetActive(false);

            // Choices container
            var choices = NewRect("Choices", panel);
            var clg = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            clg.spacing = 8;
            clg.childControlWidth = true; clg.childControlHeight = true;
            clg.childForceExpandWidth = true; clg.childForceExpandHeight = false;
            var cfit = choices.gameObject.AddComponent<ContentSizeFitter>();
            cfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Choice template (kept inactive; cloned at runtime)
            var template = NewButton("ChoiceTemplate", choices, "선택지", font, out var choiceLabel, sprites.choice);
            choiceLabel.color = light ? ink : Color.white;
            AddLayoutElement(template.gameObject, preferredHeight: 56);
            template.gameObject.SetActive(false);

            // Input row
            var inputRow = NewRect("InputRow", panel);
            var hlg = inputRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            AddLayoutElement(inputRow.gameObject, preferredHeight: 66);

            var input = NewInputField("Input", inputRow, "메시지를 입력…", font, textColor);
            AddLayoutElement(input.gameObject, flexibleWidth: 1, minWidth: 300);

            // The submit sprite already has "전송" baked in, so leave the label empty.
            string sendLabel = sprites.submit != null ? "" : "전송";
            var sendBtn = NewButton("Send", inputRow, sendLabel, font, out _, sprites.submit);
            AddLayoutElement(sendBtn.gameObject, preferredWidth: 120);

            // Wire DialogueUI serialized refs
            SetRef(ui, "nameLabel", nameLabel);
            SetRef(ui, "bodyLabel", bodyLabel);
            SetRef(ui, "choicesContainer", choices);
            SetRef(ui, "choiceButtonTemplate", template);
            SetRef(ui, "inputField", input);
            SetRef(ui, "sendButton", sendBtn);
            SetRef(ui, "thinkingIndicator", thinking.gameObject);

            return ui;
        }

        // Sprites loaded from Art/. Any missing one stays null and the setup falls
        // back gracefully (dark panel / coloured buttons / no emotion icon).
        internal class UiSprites
        {
            public Sprite panel, choice, submit, neutral, happy, sad, angry, surprised, thinking;
        }

        internal static UiSprites LoadUiSprites()
        {
            const string ui = AssetRoot + "/Art/UI/";
            const string emo = AssetRoot + "/Art/Icons/Emotions/";
            return new UiSprites
            {
                panel     = LoadSprite(ui + "panel.png"),
                choice    = LoadSprite(ui + "choice_button.png"),
                submit    = LoadSprite(ui + "submit_button.png"),
                neutral   = LoadSprite(emo + "neutral.png"),
                happy     = LoadSprite(emo + "happy.png"),
                sad       = LoadSprite(emo + "sad.png"),
                angry     = LoadSprite(emo + "angry.png"),
                surprised = LoadSprite(emo + "surprised.png"),
                thinking  = LoadSprite(emo + "thinking.png"),
            };
        }

        private static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
                Debug.LogWarning($"[AIAvatar] 스프라이트를 찾지 못했어요(스킵): {path}");
            return s;
        }

        // ── Sample assets ─────────────────────────────────────────────────────

        private static CharacterPersona CreateOrLoadPersona()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CharacterPersona>(PersonaPath);
            if (existing != null) return existing;

            EnsureFolder(AssetRoot + "/Personas");
            var persona = ScriptableObject.CreateInstance<CharacterPersona>();
            persona.displayName = "도우미";
            persona.systemPrompt =
                "당신은 VR 공간 안의 친절하고 호기심 많은 안내자 '도우미'입니다. " +
                "한국어로 1~3문장씩 자연스럽고 따뜻하게 대화하세요. 너무 길게 말하지 마세요.";
            persona.greeting = "안녕하세요! 저는 도우미예요. 무엇이 궁금하신가요?";
            persona.openingChoices = new[] { "넌 누구야?", "여긴 어디야?", "그냥 얘기하자" };
            persona.suggestedChoiceCount = 3;
            persona.requestDirectives = true;
            persona.baseColor = new Color(0.40f, 0.60f, 1.0f);
            AssetDatabase.CreateAsset(persona, PersonaPath);
            AssetDatabase.SaveAssets();
            return persona;
        }

        private static DialogueTree CreateOrLoadTree()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DialogueTree>(TreePath);
            if (existing != null) return existing;

            EnsureFolder(AssetRoot + "/Personas");
            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            tree.rootNodeId = "root";
            tree.nodes = new List<DialogueNode>
            {
                new DialogueNode {
                    id = "root", emotion = "happy",
                    avatarLine = "안녕하세요! 무엇을 하고 싶으세요?",
                    choices = new List<DialogueChoice> {
                        new DialogueChoice { text = "이 방을 소개해줘", nextNodeId = "intro" },
                        new DialogueChoice { text = "자유롭게 대화하자", nextNodeId = "ai" },
                    }
                },
                new DialogueNode {
                    id = "intro", emotion = "neutral",
                    avatarLine = "여긴 VR 교실이에요. 더 궁금한 게 있나요?",
                    choices = new List<DialogueChoice> {
                        new DialogueChoice { text = "이제 자유롭게 얘기하자", nextNodeId = "ai" },
                        new DialogueChoice { text = "고마워, 끝낼게", nextNodeId = "" },
                    }
                },
                new DialogueNode {
                    id = "ai", emotion = "thinking", handoffToAI = true,
                    avatarLine = "좋아요, 편하게 말 걸어주세요.",
                    aiContextSeed = "플레이어가 자유 대화를 선택했습니다."
                },
            };
            AssetDatabase.CreateAsset(tree, TreePath);
            AssetDatabase.SaveAssets();
            return tree;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        internal static void EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                // XR ray UI clicks require the XR UI Input Module, not the plain
                // Input System UI module. Warn (non-destructive) and point to the fix.
                if (existing.GetComponent<XRUIInputModule>() == null)
                    Debug.LogWarning("[AIAvatar] 씬의 EventSystem이 XR UI Input Module을 쓰지 않습니다. " +
                        "컨트롤러 레이로 UI를 클릭하려면 'Tools ▸ AI Avatar ▸ Fix EventSystem for XR UI'를 실행하세요.");
                return;
            }
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<XRUIInputModule>(); // XR-ready UI input
        }

        [MenuItem("Tools/AI Avatar/Fix EventSystem for XR UI", false, 40)]
        public static void FixEventSystemForXRUI()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                Undo.RegisterCreatedObjectUndo(es.gameObject, "Create EventSystem");
            }

            // Remove the plain Input System module if present.
            var legacy = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (legacy != null) Undo.DestroyObjectImmediate(legacy);

            if (es.GetComponent<XRUIInputModule>() == null)
                Undo.AddComponent<XRUIInputModule>(es.gameObject);

            Selection.activeGameObject = es.gameObject;
            EditorGUIUtility.PingObject(es.gameObject);
            Debug.Log("[AIAvatar] EventSystem을 XR UI Input Module로 전환했습니다 ✅  " +
                      "이제 Ray Interactor의 'UI Interaction'이 켜져 있으면 레이로 버튼을 클릭할 수 있어요.");
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text,
            float size, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.color = Color.white;
            t.richText = true;
            if (font != null) t.font = font;
            return t;
        }

        private static Button NewButton(string name, Transform parent, string label,
            TMP_FontAsset font, out TextMeshProUGUI labelText, Sprite sprite = null)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; img.color = Color.white; }
            else img.color = new Color(0.20f, 0.26f, 0.40f, 0.96f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var lrt = NewRect("Label", rt);
            Stretch(lrt);
            labelText = lrt.gameObject.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 24;
            labelText.color = Color.white;
            labelText.enableWordWrapping = true;
            if (font != null) labelText.font = font;
            return btn;
        }

        private static TMP_InputField NewInputField(string name, Transform parent,
            string placeholder, TMP_FontAsset font, Color textColor)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(textColor.r, textColor.g, textColor.b, 0.09f);
            var field = rt.gameObject.AddComponent<TMP_InputField>();

            var area = NewRect("Text Area", rt);
            Stretch(area);
            area.offsetMin = new Vector2(12, 8);
            area.offsetMax = new Vector2(-12, -8);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = NewText("Placeholder", area, placeholder, 24, TextAlignmentOptions.Left, font);
            Stretch(ph.rectTransform);
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(textColor.r, textColor.g, textColor.b, 0.5f);

            var txt = NewText("Text", area, "", 24, TextAlignmentOptions.Left, font);
            txt.color = textColor;
            Stretch(txt.rectTransform);

            field.textViewport = area;
            field.textComponent = txt;
            field.placeholder = ph;
            if (font != null) field.fontAsset = font;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.targetGraphic = img;
            return field;
        }

        private static void AddLayoutElement(GameObject go, float preferredHeight = -1,
            float preferredWidth = -1, float flexibleHeight = -1, float flexibleWidth = -1,
            float minHeight = -1, float minWidth = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (minHeight >= 0) le.minHeight = minHeight;
            if (minWidth >= 0) le.minWidth = minWidth;
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
                Debug.LogError($"[AIAvatar] Serialized property '{property}' not found on {target.GetType().Name}.");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedBool(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[AIAvatar] Bool property '{property}' not found on {target.GetType().Name}.");
                return;
            }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[AIAvatar] Float property '{property}' not found on {target.GetType().Name}.");
                return;
            }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
