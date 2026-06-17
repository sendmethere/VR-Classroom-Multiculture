using System.Collections;
using TMPro;
using UnityEngine;

namespace Classroom.Scenario
{
    /// <summary>
    /// 캐릭터 머리 위에 떠 있는 월드 공간 말풍선. 이름 + 대사를 보여주고,
    /// 타자기 효과로 글자를 하나씩 출력하며, 항상 카메라(플레이어)를 바라봅니다.
    /// 귓속말/혼잣말은 <see cref="BubbleStyle"/>로 스타일이 바뀝니다.
    /// 계층(Canvas/Image/Text)은 <c>ScenarioSceneSetup</c>이 만들어 참조를 연결합니다.
    /// </summary>
    [AddComponentMenu("Classroom Scenario/Speech Bubble")]
    public class SpeechBubble : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("배경 패널(색/투명도 조절). 비어도 동작")]
        [SerializeField] private UnityEngine.UI.Image background;

        [Header("Billboard")]
        [Tooltip("바라볼 대상(플레이어/HMD). 비우면 런타임에 Camera.main")]
        [SerializeField] private Transform target;
        [Tooltip("수평으로만 회전(상하 기울임 없음)")]
        [SerializeField] private bool keepUpright = true;

        [Header("Typewriter")]
        [Tooltip("초당 출력 글자 수. 0이면 즉시 표시")]
        [SerializeField] private float charsPerSecond = 28f;
        [SerializeField] private float fadeSpeed = 10f;

        private Coroutine typeRoutine;
        private float wantAlpha;

        public bool IsTyping { get; private set; }

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 0f;
            wantAlpha = 0f;
        }

        /// <summary>말풍선을 띄우고 대사를 타자기 효과로 출력.</summary>
        public void Show(string speaker, Color color, string text, BubbleStyle style)
        {
            gameObject.SetActive(true);
            ApplyStyle(style, color, speaker);

            if (nameLabel != null) nameLabel.text = speaker;
            wantAlpha = 1f;

            if (typeRoutine != null) StopCoroutine(typeRoutine);
            typeRoutine = StartCoroutine(Typewriter(text ?? ""));

            FaceTarget(snap: true); // 뜰 때 즉시 정면
        }

        /// <summary>타자기 출력 중이면 즉시 전체 문장을 표시.</summary>
        public void CompleteTypewriter()
        {
            if (!IsTyping || bodyLabel == null) return;
            if (typeRoutine != null) StopCoroutine(typeRoutine);
            bodyLabel.maxVisibleCharacters = int.MaxValue;
            IsTyping = false;
        }

        /// <summary>말풍선 숨기기(페이드 아웃).</summary>
        public void Hide()
        {
            wantAlpha = 0f;
            if (typeRoutine != null) { StopCoroutine(typeRoutine); typeRoutine = null; }
            IsTyping = false;
        }

        private IEnumerator Typewriter(string text)
        {
            IsTyping = true;
            if (bodyLabel != null)
            {
                bodyLabel.text = text;
                bodyLabel.maxVisibleCharacters = 0;
                bodyLabel.ForceMeshUpdate();
            }

            if (charsPerSecond <= 0f || bodyLabel == null)
            {
                if (bodyLabel != null) bodyLabel.maxVisibleCharacters = int.MaxValue;
                IsTyping = false;
                yield break;
            }

            int total = bodyLabel.textInfo.characterCount;
            float shown = 0f;
            while (shown < total)
            {
                shown += charsPerSecond * Time.deltaTime;
                bodyLabel.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);
                yield return null;
            }
            bodyLabel.maxVisibleCharacters = int.MaxValue;
            IsTyping = false;
        }

        private void ApplyStyle(BubbleStyle style, Color color, string speaker)
        {
            if (nameLabel != null) nameLabel.color = color;

            switch (style)
            {
                case BubbleStyle.Whisper:
                    if (nameLabel != null) nameLabel.text = speaker + " (귓속말)";
                    if (bodyLabel != null) bodyLabel.fontStyle = FontStyles.Italic;
                    if (background != null) background.color = new Color(0.92f, 0.92f, 0.96f, 0.78f);
                    break;
                case BubbleStyle.Thought:
                    if (nameLabel != null) nameLabel.text = speaker + " (속마음)";
                    if (bodyLabel != null) bodyLabel.fontStyle = FontStyles.Italic;
                    if (background != null) background.color = new Color(0.96f, 0.96f, 0.90f, 0.82f);
                    break;
                default:
                    if (bodyLabel != null) bodyLabel.fontStyle = FontStyles.Normal;
                    if (background != null) background.color = new Color(1f, 1f, 1f, 0.95f);
                    break;
            }
        }

        private void LateUpdate()
        {
            // 부드러운 페이드
            if (group != null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, wantAlpha, fadeSpeed * Time.deltaTime);
                if (wantAlpha == 0f && group.alpha <= 0.001f)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }
            FaceTarget(snap: false);
        }

        private void FaceTarget(bool snap)
        {
            if (target == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                target = cam.transform;
            }

            Vector3 dir = transform.position - target.position; // 텍스트가 정방향이 되도록
            if (keepUpright) dir.y = 0f;
            if (dir.sqrMagnitude < 1e-5f) return;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = snap ? rot : Quaternion.Slerp(transform.rotation, rot, 12f * Time.deltaTime);
        }
    }
}
