using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AIAvatar
{
    /// <summary>
    /// Text-to-speech over a REST endpoint that returns an audio file. Defaults to
    /// the OpenAI Audio Speech format, but the URL, auth header, and JSON body
    /// template are all configurable, so it also works with ElevenLabs / Azure /
    /// your own proxy by editing those fields.
    ///
    /// We fetch raw bytes (DownloadHandlerBuffer) and decode ourselves:
    /// WAV is parsed directly into an AudioClip (no FMOD file loading, which fails
    /// on POST responses), MP3 is decoded via a temp file. WAV is recommended.
    ///
    /// Body template placeholders: {TEXT} {VOICE} {MODEL} {FORMAT}
    ///
    /// ── API KEY ──────────────────────────────────────────────────────────────
    /// Resolution: inspector field → StreamingAssets/tts_api_key.txt → env var
    /// (default OPENAI_API_KEY). For shipped builds prefer a Proxy URL so the key
    /// never leaves your backend.
    /// </summary>
    [AddComponentMenu("AI Avatar/Speech/REST Text To Speech")]
    public class RestTextToSpeech : MonoBehaviour, ITextToSpeech
    {
        public enum AudioFormat { Wav, Mp3 }

        [Header("Endpoint")]
        [SerializeField] private string endpoint = "https://api.openai.com/v1/audio/speech";
        [SerializeField] private string model = "gpt-4o-mini-tts";
        [SerializeField] private string voice = "alloy";
        [Tooltip("wav 권장(직접 디코딩, 가장 안정). mp3는 임시파일 경유")]
        [SerializeField] private AudioFormat format = AudioFormat.Wav;

        [Header("Auth (키는 빌드에 넣지 말고 Proxy Url 권장)")]
        [SerializeField] private string authHeaderName = "Authorization";
        [Tooltip("키 앞에 붙일 접두어. OpenAI는 'Bearer ' / ElevenLabs는 빈 값 + 헤더명 'xi-api-key'")]
        [SerializeField] private string authPrefix = "Bearer ";
        [SerializeField] private string apiKey = "";
        [SerializeField] private string apiKeyEnvVar = "OPENAI_API_KEY";
        [Tooltip("설정하면 endpoint 대신 이 프록시로 전송하고 인증 헤더를 생략")]
        [SerializeField] private string proxyUrl = "";

        [Header("Body template  ({TEXT} {VOICE} {MODEL} {FORMAT})")]
        [TextArea(3, 6)]
        [SerializeField] private string bodyTemplate =
            "{\"model\":\"{MODEL}\",\"input\":\"{TEXT}\",\"voice\":\"{VOICE}\",\"response_format\":\"{FORMAT}\"}";

        [Header("Playback")]
        [SerializeField] private AudioSource audioSource;

        public bool IsSpeaking => audioSource != null && audioSource.isPlaying;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        public void Speak(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) _ = SpeakAsync(text);
        }

        public void StopSpeaking()
        {
            if (audioSource != null) audioSource.Stop();
        }

        private async Awaitable SpeakAsync(string text)
        {
            bool useProxy = !string.IsNullOrEmpty(proxyUrl);
            string key = ResolveKey();
            if (!useProxy && string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[AIAvatar] TTS API 키가 없어 음성을 생략합니다. (키 또는 Proxy Url 설정 필요)");
                return;
            }

            string fmt = format == AudioFormat.Wav ? "wav" : "mp3";
            string body = bodyTemplate
                .Replace("{MODEL}", model)
                .Replace("{VOICE}", voice)
                .Replace("{FORMAT}", fmt)
                .Replace("{TEXT}", EscapeJson(text));

            string url = useProxy ? proxyUrl : endpoint;

            byte[] data;
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("content-type", "application/json");
                if (!useProxy && !string.IsNullOrEmpty(authHeaderName))
                    req.SetRequestHeader(authHeaderName, authPrefix + key);

                var op = req.SendWebRequest();
                while (!op.isDone) await Awaitable.NextFrameAsync();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AIAvatar] TTS 요청 실패({req.responseCode}): {req.error}\n{Preview(req.downloadHandler.text)}");
                    return;
                }
                data = req.downloadHandler.data;
            }

            if (data == null || data.Length == 0)
            {
                Debug.LogError("[AIAvatar] TTS 응답이 비어 있습니다.");
                return;
            }
            if (LooksLikeJson(data))
            {
                Debug.LogError("[AIAvatar] TTS 엔드포인트가 오디오 대신 JSON을 반환했어요 (키/모델/요청 형식 확인): "
                               + Preview(Encoding.UTF8.GetString(data)));
                return;
            }

            AudioClip clip = format == AudioFormat.Wav
                ? ParseWav(data, "tts")
                : await DecodeViaTempFile(data, "mp3");

            if (clip == null)
            {
                Debug.LogError("[AIAvatar] TTS 오디오 디코딩 실패.");
                return;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }

        // ── WAV → AudioClip (in-memory, RIFF chunk walk) ───────────────────────
        private static AudioClip ParseWav(byte[] b, string name)
        {
            if (b == null || b.Length < 44) return null;
            if (b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return null;

            int channels = 1, sampleRate = 24000, bits = 16;
            int dataPos = -1, dataLen = 0;
            int pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int size = BitConverter.ToInt32(b, pos + 4);
                int bodyPos = pos + 8;
                if (id == "fmt " && bodyPos + 16 <= b.Length)
                {
                    channels = BitConverter.ToInt16(b, bodyPos + 2);
                    sampleRate = BitConverter.ToInt32(b, bodyPos + 4);
                    bits = BitConverter.ToInt16(b, bodyPos + 14);
                }
                else if (id == "data")
                {
                    dataPos = bodyPos;
                    dataLen = size;
                    if (dataLen <= 0 || dataPos + dataLen > b.Length) dataLen = b.Length - dataPos;
                    break;
                }
                if (size < 0) break;
                pos = bodyPos + size + (size & 1); // chunks are word-aligned
            }
            if (dataPos < 0 || channels <= 0) return null;

            int bytesPerSample = Mathf.Max(1, bits / 8);
            int sampleCount = dataLen / bytesPerSample;
            if (sampleCount <= 0) return null;

            var floats = new float[sampleCount];
            int p = dataPos;
            switch (bits)
            {
                case 16:
                    for (int i = 0; i < sampleCount; i++) { floats[i] = (short)(b[p] | (b[p + 1] << 8)) / 32768f; p += 2; }
                    break;
                case 8:
                    for (int i = 0; i < sampleCount; i++) { floats[i] = (b[p] - 128) / 128f; p += 1; }
                    break;
                case 24:
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int s = b[p] | (b[p + 1] << 8) | (b[p + 2] << 16);
                        if ((s & 0x800000) != 0) s |= unchecked((int)0xFF000000);
                        floats[i] = s / 8388608f; p += 3;
                    }
                    break;
                case 32:
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int s = b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24);
                        floats[i] = s / 2147483648f; p += 4;
                    }
                    break;
                default:
                    return null;
            }

            int perChannel = sampleCount / channels;
            if (perChannel <= 0) return null;
            var clip = AudioClip.Create(string.IsNullOrEmpty(name) ? "tts" : name, perChannel, channels, sampleRate, false);
            clip.SetData(floats, 0);
            return clip;
        }

        // ── MP3 (and fallback) via temp file ───────────────────────────────────
        private async Awaitable<AudioClip> DecodeViaTempFile(byte[] data, string ext)
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "aiavatar_tts." + ext);
            try { System.IO.File.WriteAllBytes(path, data); }
            catch (Exception e) { Debug.LogError($"[AIAvatar] 임시 오디오 저장 실패: {e.Message}"); return null; }

            using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.MPEG);
            var op = req.SendWebRequest();
            while (!op.isDone) await Awaitable.NextFrameAsync();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AIAvatar] mp3 디코딩 실패: {req.error}");
                return null;
            }
            return DownloadHandlerAudioClip.GetContent(req);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static bool LooksLikeJson(byte[] data)
        {
            for (int i = 0; i < data.Length && i < 16; i++)
            {
                byte c = data[i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t') continue;
                return c == '{' || c == '[';
            }
            return false;
        }

        private static string Preview(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) + "…" : s);

        private string ResolveKey()
        {
            if (!string.IsNullOrEmpty(apiKey)) return apiKey.Trim();
            try
            {
                string p = System.IO.Path.Combine(Application.streamingAssetsPath, "tts_api_key.txt");
                if (System.IO.File.Exists(p))
                {
                    string k = System.IO.File.ReadAllText(p).Trim();
                    if (!string.IsNullOrEmpty(k)) return k;
                }
            }
            catch { /* ignore */ }
            if (!string.IsNullOrEmpty(apiKeyEnvVar))
            {
                string e = Environment.GetEnvironmentVariable(apiKeyEnvVar);
                if (!string.IsNullOrEmpty(e)) return e.Trim();
            }
            return null;
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
