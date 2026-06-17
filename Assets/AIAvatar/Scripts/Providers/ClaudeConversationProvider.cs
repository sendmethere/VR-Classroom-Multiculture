using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AIAvatar
{
    /// <summary>
    /// Talks to the Anthropic Claude Messages API. The persona's system prompt is
    /// the character's identity; we additionally ask the model to reply as a small
    /// JSON object so we get the spoken line, suggested choices, and a directive
    /// (emotion/action) in one round-trip. Parsing is defensive: if the model
    /// returns plain prose, we still show it as the reply.
    ///
    /// ── API KEY ──────────────────────────────────────────────────────────────
    /// Resolution order: inspector field → StreamingAssets/anthropic_api_key.txt
    /// → ANTHROPIC_API_KEY environment variable.
    /// ⚠ A key embedded in a shipped client build is extractable. For production,
    ///   set <see cref="proxyUrl"/> to your own server that injects the key and
    ///   forwards to Anthropic; the key then never leaves your backend.
    /// </summary>
    [AddComponentMenu("AI Avatar/Providers/Claude Conversation Provider")]
    public class ClaudeConversationProvider : MonoBehaviour, IConversationProvider
    {
        [Header("Model")]
        [Tooltip("예: claude-sonnet-4-6 (대화), claude-opus-4-8 (고품질), claude-haiku-4-5-20251001 (빠름)")]
        [SerializeField] private string model = "claude-sonnet-4-6";
        [SerializeField, Range(64, 4096)] private int maxTokens = 1024;

        [Header("Auth (택1) — 키는 빌드에 넣지 말고 프록시 권장)")]
        [Tooltip("비워두면 StreamingAssets/anthropic_api_key.txt 또는 환경변수 ANTHROPIC_API_KEY 사용")]
        [SerializeField] private string apiKey = "";
        [Tooltip("설정하면 이 URL로 전송하고 x-api-key 헤더를 보내지 않음 (서버가 키 주입)")]
        [SerializeField] private string proxyUrl = "";

        private const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";

        private CharacterPersona persona;

        public void Initialize(CharacterPersona persona) => this.persona = persona;

        public Awaitable<AvatarTurn> BeginAsync(ConversationState state) =>
            // Treat the opening as a hidden "greet the player" instruction.
            RequestAsync(state, "[SYSTEM] 대화를 시작합니다. 인사하고 첫 대사를 해주세요.");

        public Awaitable<AvatarTurn> RespondAsync(ConversationState state, string message) =>
            RequestAsync(state, message);

        private async Awaitable<AvatarTurn> RequestAsync(ConversationState state, string latestUserMessage)
        {
            string key = ResolveApiKey();
            bool useProxy = !string.IsNullOrEmpty(proxyUrl);
            if (!useProxy && string.IsNullOrEmpty(key))
            {
                return AvatarTurn.Say(
                    "(Claude API 키가 설정되지 않았어요. Mock 프로바이더로 전환하거나 키를 등록하세요.)");
            }

            string url = useProxy ? proxyUrl : DefaultEndpoint;
            string body = BuildRequestJson(state, latestUserMessage);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("content-type", "application/json");
            if (!useProxy)
            {
                req.SetRequestHeader("x-api-key", key);
                req.SetRequestHeader("anthropic-version", AnthropicVersion);
            }

            var op = req.SendWebRequest();
            while (!op.isDone) await Awaitable.NextFrameAsync();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AIAvatar] Claude request failed ({req.responseCode}): {req.error}\n{req.downloadHandler.text}");
                return AvatarTurn.Say($"(요청 실패: {req.responseCode} {req.error})");
            }

            return ParseResponse(req.downloadHandler.text);
        }

        // ── Request building ──────────────────────────────────────────────────

        private string BuildRequestJson(ConversationState state, string latestUserMessage)
        {
            var messages = new List<ApiMessage>();
            foreach (var m in state.History)
            {
                if (m.speaker == Speaker.System) continue;
                messages.Add(new ApiMessage
                {
                    role = m.speaker == Speaker.Player ? "user" : "assistant",
                    content = m.text
                });
            }
            // The latest player message isn't in History yet for BeginAsync; the
            // controller adds player messages before RespondAsync, so guard dupes.
            if (messages.Count == 0 || messages[^1].role != "user")
                messages.Add(new ApiMessage { role = "user", content = latestUserMessage });

            // Anthropic requires the first message to be role "user". The avatar's
            // opening greeting lives in history as an assistant turn, so trim any
            // leading assistant messages (alternation is preserved afterwards).
            while (messages.Count > 0 && messages[0].role != "user")
                messages.RemoveAt(0);

            var payload = new ApiRequest
            {
                model = model,
                max_tokens = maxTokens,
                system = BuildSystemPrompt(),
                messages = messages.ToArray()
            };
            return JsonUtility.ToJson(payload);
        }

        private string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine(persona.systemPrompt);
            sb.AppendLine();
            sb.AppendLine("# 출력 형식 (반드시 지킬 것)");
            sb.AppendLine("아래 JSON 객체 하나만 출력하세요. 코드펜스/설명 없이 JSON만:");
            sb.AppendLine("{");
            sb.AppendLine("  \"reply\": \"캐릭터가 말하는 대사 (한국어, 1~3문장)\",");
            if (persona.suggestedChoiceCount > 0)
                sb.AppendLine($"  \"choices\": [\"플레이어가 고를 응답 후보 {persona.suggestedChoiceCount}개\"],");
            else
                sb.AppendLine("  \"choices\": [],");
            if (persona.requestDirectives)
            {
                sb.AppendLine("  \"emotion\": \"neutral|happy|sad|angry|surprised|thinking 중 하나\",");
                sb.AppendLine("  \"action\": \"wave|nod|shrug 등 동작 id 또는 빈 문자열\"");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ── Response parsing ──────────────────────────────────────────────────

        private AvatarTurn ParseResponse(string json)
        {
            string text;
            try
            {
                var resp = JsonUtility.FromJson<ApiResponse>(json);
                text = (resp?.content != null && resp.content.Length > 0) ? resp.content[0].text : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIAvatar] Failed to parse Claude envelope: {e.Message}\n{json}");
                return AvatarTurn.Say("(응답 파싱 실패)");
            }

            if (string.IsNullOrWhiteSpace(text))
                return AvatarTurn.Say("(빈 응답)");

            string inner = StripCodeFences(text).Trim();
            try
            {
                var parsed = JsonUtility.FromJson<LlmReply>(inner);
                if (parsed != null && !string.IsNullOrEmpty(parsed.reply))
                {
                    var turn = new AvatarTurn { reply = parsed.reply };
                    if (parsed.choices != null) turn.choices.AddRange(parsed.choices);
                    turn.directives.emotion = string.IsNullOrEmpty(parsed.emotion) ? "neutral" : parsed.emotion;
                    turn.directives.action = parsed.action;
                    return turn;
                }
            }
            catch { /* fall through to plain-text handling */ }

            // Model didn't return our JSON — treat the whole thing as a spoken line.
            return AvatarTurn.Say(inner);
        }

        private static string StripCodeFences(string s)
        {
            s = s.Trim();
            if (!s.StartsWith("```")) return s;
            int firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0) s = s.Substring(firstNewline + 1);
            int lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) s = s.Substring(0, lastFence);
            return s.Trim();
        }

        // ── Key resolution ────────────────────────────────────────────────────

        private string ResolveApiKey()
        {
            if (!string.IsNullOrEmpty(apiKey)) return apiKey.Trim();

            try
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "anthropic_api_key.txt");
                if (System.IO.File.Exists(path))
                {
                    string fromFile = System.IO.File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(fromFile)) return fromFile;
                }
            }
            catch { /* ignore */ }

            string env = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            return string.IsNullOrEmpty(env) ? null : env.Trim();
        }

        // ── JSON DTOs (JsonUtility-friendly) ──────────────────────────────────

        [Serializable] private class ApiRequest { public string model; public int max_tokens; public string system; public ApiMessage[] messages; }
        [Serializable] private class ApiMessage { public string role; public string content; }
        [Serializable] private class ApiResponse { public ApiContent[] content; }
        [Serializable] private class ApiContent { public string type; public string text; }
        [Serializable] private class LlmReply { public string reply; public string[] choices; public string emotion; public string action; }
    }
}
