using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIAvatar
{
    /// <summary>
    /// World-space dialogue panel: avatar name + line, a column of choice buttons
    /// (clickable by an XR ray or the mouse), a free-text input, and a "thinking"
    /// indicator. Knows nothing about which provider is talking — it just renders
    /// <see cref="AvatarTurn"/>s and forwards player input to the controller.
    /// All references are wired by the editor setup, but you can rebuild/restyle
    /// the panel freely; only the serialized fields below must stay connected.
    /// </summary>
    [AddComponentMenu("AI Avatar/Dialogue UI")]
    public class DialogueUI : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text bodyLabel;

        [Header("Choices")]
        [SerializeField] private Transform choicesContainer;
        [Tooltip("선택지 버튼 템플릿 (비활성 상태로 두면 복제해서 사용)")]
        [SerializeField] private Button choiceButtonTemplate;

        [Header("Free text input")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;

        [Header("Status")]
        [SerializeField] private GameObject thinkingIndicator;

        private ConversationController controller;
        private readonly List<GameObject> spawnedChoices = new();

        /// <summary>Connect this panel to a controller (idempotent).</summary>
        public void Bind(ConversationController controller)
        {
            this.controller = controller;

            if (choiceButtonTemplate != null)
            {
                ApplyButtonFeedback(choiceButtonTemplate); // 호버/클릭 색을 진하게 (클론도 상속)
                choiceButtonTemplate.gameObject.SetActive(false);
            }

            if (sendButton != null)
            {
                ApplyButtonFeedback(sendButton);
                sendButton.onClick.RemoveListener(SubmitFromInput);
                sendButton.onClick.AddListener(SubmitFromInput);
            }
            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(OnInputSubmit);
                inputField.onSubmit.AddListener(OnInputSubmit);
            }
            if (thinkingIndicator != null) thinkingIndicator.SetActive(false);
        }

        /// <summary>Strong hover/press colours so ray targeting is obvious in VR.</summary>
        private static void ApplyButtonFeedback(Button btn)
        {
            if (btn == null) return;
            btn.transition = Selectable.Transition.ColorTint;
            var c = btn.colors;
            c.normalColor      = Color.white;
            c.highlightedColor = new Color(0.55f, 0.78f, 1f);   // 레이 호버 → 밝은 파랑
            c.pressedColor     = new Color(0.28f, 0.52f, 0.95f); // 클릭 → 진한 파랑
            c.selectedColor    = new Color(0.55f, 0.78f, 1f);
            c.disabledColor    = new Color(0.7f, 0.7f, 0.7f, 0.5f);
            c.colorMultiplier  = 1f;
            c.fadeDuration     = 0.08f;
            btn.colors = c;
        }

        public void ShowThinking(string speakerName)
        {
            if (nameLabel != null) nameLabel.text = speakerName;
            ClearChoices();
            if (thinkingIndicator != null) thinkingIndicator.SetActive(true);
        }

        public void ShowPlayerMessage(string message)
        {
            // Lightweight echo so the player sees what they sent.
            if (bodyLabel != null) bodyLabel.text = $"<i>나: {message}</i>";
            ClearChoices();
        }

        public void ShowAvatarTurn(string speakerName, AvatarTurn turn)
        {
            if (thinkingIndicator != null) thinkingIndicator.SetActive(false);
            if (nameLabel != null) nameLabel.text = speakerName;
            if (bodyLabel != null) bodyLabel.text = turn != null ? turn.reply : "";

            RebuildChoices(turn);
        }

        // ── Choices ───────────────────────────────────────────────────────────

        private void RebuildChoices(AvatarTurn turn)
        {
            ClearChoices();
            if (turn == null || choiceButtonTemplate == null || choicesContainer == null) return;

            foreach (var choice in turn.choices)
            {
                if (string.IsNullOrWhiteSpace(choice)) continue;
                var go = Instantiate(choiceButtonTemplate.gameObject, choicesContainer);
                go.SetActive(true);
                spawnedChoices.Add(go);

                var label = go.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = choice;

                string captured = choice;
                var btn = go.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => OnChoiceClicked(captured));
            }
        }

        private void ClearChoices()
        {
            foreach (var go in spawnedChoices)
                if (go != null) Destroy(go);
            spawnedChoices.Clear();
        }

        // ── Input forwarding ──────────────────────────────────────────────────

        private void OnChoiceClicked(string choice)
        {
            controller?.SelectChoice(choice);
        }

        private void OnInputSubmit(string _) => SubmitFromInput();

        private void SubmitFromInput()
        {
            if (inputField == null) return;
            string text = inputField.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            inputField.text = "";
            inputField.ActivateInputField();
            controller?.SubmitPlayerMessage(text);
        }
    }
}
