using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AIAvatar
{
    /// <summary>
    /// <see cref="ISpeechToText"/> backed by OpenAI Whisper (audio transcription REST).
    /// Records from the system's default microphone via Unity's <see cref="Microphone"/>
    /// API between <see cref="StartListening"/> and <see cref="StopListening"/>, encodes
    /// the captured samples to 16-bit PCM WAV in memory, and POSTs them as multipart
    /// form-data to <c>/v1/audio/transcriptions</c>. The final transcript is raised on
    /// <see cref="OnFinalText"/> — wire that to <see cref="ConversationController.SubmitPlayerMessage"/>
    /// (the bundled <c>SpeechInputRelay</c> already does this).
    ///
    /// Whisper has no streaming partial results here, so <see cref="OnPartialText"/> is
    /// unused (kept for interface compatibility).
    ///
    /// ── API KEY ──────────────────────────────────────────────────────────────
    /// Resolution: inspector field → StreamingAssets/openai_api_key.txt → env var
    /// (default OPENAI_API_KEY). Whisper uses the same OpenAI key as RestTextToSpeech.
    /// For shipped builds prefer a Proxy Url so the key never leaves your backend.
    /// </summary>
    [AddComponentMenu("AI Avatar/Input/Whisper Speech To Text")]
    public class WhisperSpeechToText : MonoBehaviour, ISpeechToText
    {
        [Header("Endpoint")]
        [SerializeField] private string endpoint = "https://api.openai.com/v1/audio/transcriptions";
        [Tooltip("whisper-1(안정) / gpt-4o-transcribe / gpt-4o-mini-transcribe")]
        [SerializeField] private string model = "whisper-1";
        [Tooltip("인식 언어 힌트(ISO-639-1). 한국어=ko, 비우면 자동 감지")]
        [SerializeField] private string language = "ko";

        [Header("Auth (키는 빌드에 넣지 말고 Proxy Url 권장)")]
        [SerializeField] private string apiKey = "";
        [SerializeField] private string apiKeyEnvVar = "OPENAI_API_KEY";
        [Tooltip("설정하면 endpoint 대신 이 프록시로 전송하고 Authorization 헤더를 생략")]
        [SerializeField] private string proxyUrl = "";

        [Header("Microphone")]
        [Tooltip("비우면 시스템 기본 입력 장치 사용")]
        [SerializeField] private string microphoneDevice = "";
        [Tooltip("요청 샘플레이트(Whisper 권장 16kHz). 장치가 지원하지 않으면 자동 보정")]
        [SerializeField] private int sampleRate = 16000;
        [Tooltip("한 번 녹음 최대 길이(초). 초과하면 잘림")]
        [SerializeField, Range(2, 60)] private int maxSeconds = 20;

        public bool IsListening { get; private set; }
        public event Action<string> OnPartialText;
        public event Action<string> OnFinalText;

        private AudioClip recordingClip;
        private string activeDevice;
        private int recordStartSample;

        public void StartListening()
        {
            if (IsListening) return;

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogError("[AIAvatar] 마이크 장치를 찾을 수 없습니다. (입력 장치/권한 확인)");
                return;
            }

            activeDevice = string.IsNullOrEmpty(microphoneDevice) ? null : microphoneDevice; // null = 기본 장치
            int freq = ResolveFrequency(activeDevice, sampleRate);

            recordingClip = Microphone.Start(activeDevice, false, maxSeconds, freq);
            if (recordingClip == null)
            {
                Debug.LogError("[AIAvatar] 마이크 녹음을 시작하지 못했습니다.");
                return;
            }

            recordStartSample = 0;
            IsListening = true;
            Debug.Log($"[AIAvatar] 🎙 녹음 시작 (장치: {(activeDevice ?? "기본")}, {freq}Hz). V 키를 떼면 인식합니다.");
        }

        public void StopListening()
        {
            if (!IsListening) return;
            IsListening = false;

            int endSample = Microphone.GetPosition(activeDevice);
            Microphone.End(activeDevice);

            if (recordingClip == null || endSample <= recordStartSample)
            {
                Debug.LogWarning("[AIAvatar] 녹음된 오디오가 없어 인식을 건너뜁니다.");
                OnFinalText?.Invoke(string.Empty);
                return;
            }

            int sampleCount = endSample - recordStartSample;
            var samples = new float[sampleCount * recordingClip.channels];
            recordingClip.GetData(samples, recordStartSample);

            byte[] wav = EncodeWav(samples, recordingClip.channels, recordingClip.frequency);
            recordingClip = null;

            _ = TranscribeAsync(wav);
        }

        // ── Whisper request (multipart/form-data) ─────────────────────────────

        private async Awaitable TranscribeAsync(byte[] wav)
        {
            bool useProxy = !string.IsNullOrEmpty(proxyUrl);
            string key = ResolveKey();
            if (!useProxy && string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[AIAvatar] STT: OpenAI API 키가 없어 음성 인식을 건너뜁니다. (키 또는 Proxy Url 설정 필요)");
                OnFinalText?.Invoke(string.Empty);
                return;
            }

            var form = new WWWForm();
            form.AddBinaryData("file", wav, "speech.wav", "audio/wav");
            form.AddField("model", model);
            form.AddField("response_format", "json");
            if (!string.IsNullOrEmpty(language)) form.AddField("language", language);

            string url = useProxy ? proxyUrl : endpoint;

            using var req = UnityWebRequest.Post(url, form);
            if (!useProxy) req.SetRequestHeader("Authorization", "Bearer " + key);

            var op = req.SendWebRequest();
            while (!op.isDone) await Awaitable.NextFrameAsync();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AIAvatar] STT 요청 실패({req.responseCode}): {req.error}\n{Preview(req.downloadHandler.text)}");
                OnFinalText?.Invoke(string.Empty);
                return;
            }

            string text = ParseText(req.downloadHandler.text);
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.Log("[AIAvatar] STT: 인식된 텍스트가 없습니다.");
                OnFinalText?.Invoke(string.Empty);
                return;
            }

            Debug.Log($"[AIAvatar] 🗣 인식 결과: {text}");
            OnFinalText?.Invoke(text.Trim());
        }

        private static string ParseText(string json)
        {
            try
            {
                var r = JsonUtility.FromJson<WhisperResponse>(json);
                return r != null ? r.text : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIAvatar] STT 응답 파싱 실패: {e.Message}\n{Preview(json)}");
                return null;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static int ResolveFrequency(string device, int requested)
        {
            Microphone.GetDeviceCaps(device, out int min, out int max);
            if (min == 0 && max == 0) return requested;          // 임의 주파수 허용
            return Mathf.Clamp(requested, min, max);
        }

        // float[-1,1] → 16-bit PCM WAV (RIFF)
        private static byte[] EncodeWav(float[] samples, int channels, int frequency)
        {
            int byteCount = samples.Length * 2;
            using var ms = new System.IO.MemoryStream(44 + byteCount);
            using var w = new System.IO.BinaryWriter(ms);

            void Str(string s) => w.Write(Encoding.ASCII.GetBytes(s));

            Str("RIFF");
            w.Write(36 + byteCount);
            Str("WAVE");
            Str("fmt ");
            w.Write(16);                                 // PCM header size
            w.Write((short)1);                           // PCM
            w.Write((short)channels);
            w.Write(frequency);
            w.Write(frequency * channels * 2);           // byte rate
            w.Write((short)(channels * 2));              // block align
            w.Write((short)16);                          // bits/sample
            Str("data");
            w.Write(byteCount);

            foreach (float f in samples)
                w.Write((short)(Mathf.Clamp(f, -1f, 1f) * 32767f));

            w.Flush();
            return ms.ToArray();
        }

        private string ResolveKey()
        {
            if (!string.IsNullOrEmpty(apiKey)) return apiKey.Trim();
            try
            {
                string p = System.IO.Path.Combine(Application.streamingAssetsPath, "openai_api_key.txt");
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

        private static string Preview(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) + "…" : s);

        [Serializable] private class WhisperResponse { public string text; }
    }
}
