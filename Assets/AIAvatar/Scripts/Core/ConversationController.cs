using System;
using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// Orchestrates a conversation: owns the persona, the active provider, and the
    /// transcript; drives the <see cref="DialogueUI"/> and <see cref="AvatarCharacter"/>.
    /// UI and input call <see cref="SubmitPlayerMessage"/> / <see cref="SelectChoice"/>;
    /// everything else flows from there. Provider, persona and UI are all swappable
    /// at runtime, so this single class is the integration point for the whole feature.
    /// </summary>
    [AddComponentMenu("AI Avatar/Conversation Controller")]
    public class ConversationController : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private CharacterPersona persona;

        [Tooltip("IConversationProvider 구현 컴포넌트 (Mock / Claude / DialogueTree). 비우면 자식에서 자동 검색")]
        [SerializeField] private MonoBehaviour providerBehaviour;

        [SerializeField] private DialogueUI ui;
        [SerializeField] private AvatarCharacter avatar;

        [Tooltip("ITextToSpeech 구현 (RestTextToSpeech 등). 비우면 음성 출력 없음")]
        [SerializeField] private MonoBehaviour ttsBehaviour;

        [Header("Flow")]
        [SerializeField] private bool startOnEnable = true;

        public CharacterPersona Persona => persona;
        public ConversationState State { get; } = new();
        public bool IsBusy { get; private set; }
        public bool HasEnded { get; private set; }

        private IConversationProvider provider;
        private ITextToSpeech tts;

        public event Action<AvatarTurn> OnAvatarTurn;
        public event Action<string> OnPlayerMessage;
        public event Action OnConversationEnded;

        private void Awake()
        {
            provider = providerBehaviour as IConversationProvider
                       ?? GetComponentInChildren<IConversationProvider>();
            tts = ttsBehaviour as ITextToSpeech;
            if (ui != null) ui.Bind(this);
        }

        private async void OnEnable()
        {
            if (startOnEnable) await StartConversationAsync();
        }

        // ── Public control surface ────────────────────────────────────────────

        public void SetPersona(CharacterPersona p) => persona = p;

        public void SetProvider(IConversationProvider p) => provider = p;

        public void SetTts(ITextToSpeech t) => tts = t;

        public void StopSpeaking() => tts?.StopSpeaking();

        public void SetProvider(MonoBehaviour behaviour)
        {
            providerBehaviour = behaviour;
            provider = behaviour as IConversationProvider;
        }

        public async Awaitable StartConversationAsync()
        {
            if (!Validate()) return;

            HasEnded = false;
            tts?.StopSpeaking();
            State.Clear();
            State.Persona = persona;
            provider.Initialize(persona);
            if (avatar != null) avatar.ApplyPersona(persona);

            IsBusy = true;
            ui?.ShowThinking(persona.displayName);
            try
            {
                HandleTurn(await provider.BeginAsync(State));
            }
            catch (Exception e) { Fail(e); }
            finally { IsBusy = false; }
        }

        public async void SubmitPlayerMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (IsBusy || HasEnded || provider == null)
            {
                Debug.Log($"[AIAvatar] 입력 무시됨 → busy={IsBusy}, ended={HasEnded}, provider={(provider != null)}. " +
                          "(busy=아바타 응답 중, ended=대화 종료됨)");
                return;
            }
            message = message.Trim();

            tts?.StopSpeaking(); // 플레이어가 응답하면 아바타 발화 중단
            State.Add(Speaker.Player, message);
            OnPlayerMessage?.Invoke(message);
            ui?.ShowPlayerMessage(message);

            IsBusy = true;
            ui?.ShowThinking(persona.displayName);
            try
            {
                HandleTurn(await provider.RespondAsync(State, message));
            }
            catch (Exception e) { Fail(e); }
            finally { IsBusy = false; }
        }

        public void SelectChoice(string choiceText) => SubmitPlayerMessage(choiceText);

        public async void Restart() => await StartConversationAsync();

        // ── Internals ─────────────────────────────────────────────────────────

        private void HandleTurn(AvatarTurn turn)
        {
            if (turn == null) return;

            State.Add(Speaker.Avatar, turn.reply);
            if (avatar != null) avatar.ApplyDirectives(turn.directives);
            ui?.ShowAvatarTurn(persona.displayName, turn);
            if (!string.IsNullOrEmpty(turn.reply)) tts?.Speak(turn.reply);
            OnAvatarTurn?.Invoke(turn);

            if (turn.endConversation)
            {
                HasEnded = true;
                OnConversationEnded?.Invoke();
            }
        }

        private bool Validate()
        {
            if (provider == null)
            {
                Debug.LogError("[AIAvatar] No conversation provider assigned/found.", this);
                return false;
            }
            if (persona == null)
            {
                Debug.LogError("[AIAvatar] No CharacterPersona assigned.", this);
                return false;
            }
            return true;
        }

        private void Fail(Exception e)
        {
            Debug.LogException(e);
            string name = persona != null ? persona.displayName : "Avatar";
            ui?.ShowAvatarTurn(name, AvatarTurn.Say("(오류가 발생했어요: " + e.Message + ")"));
        }
    }
}
