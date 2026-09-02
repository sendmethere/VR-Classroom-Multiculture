using UnityEngine;
using UnityEngine.InputSystem;

public class XRPlayerController : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;

    [SerializeField] private float playerSpeed = 2.0f;
    [SerializeField] private float gravityValue = -20.0f;

    [Header("XR Input")]
    [SerializeField] private InputActionProperty moveAction;
    [SerializeField] private Transform cameraTransform;

    [Header("Desktop Mouse Look")]
    [Tooltip("마우스로 시점 회전 허용 (데스크톱 테스트용)")]
    [SerializeField] private bool enableMouseLook = true;
    [Tooltip("오른쪽 마우스 버튼을 누르고 있을 때만 회전 (UI 클릭과 충돌 방지). 끄면 항상 회전")]
    [SerializeField] private bool holdRightMouseToLook = true;
    [SerializeField] private float mouseSensitivity = 0.12f;

    private float pitch;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!controller.enabled) return;

        if (controller.isGrounded && playerVelocity.y < 0)
            playerVelocity.y = -2f;

        Vector2 input = moveAction.action != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        // 데스크톱 테스트용 WASD / 방향키 (New Input System). XR 스틱 입력과 함께 동작.
        Vector2 keyboard = ReadKeyboardMove();
        if (keyboard != Vector2.zero) input = keyboard;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * input.y + right * input.x)
                          * playerSpeed * Time.deltaTime;

        controller.Move(moveDir);

        playerVelocity.y += gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        HandleMouseLook();
    }

    // 마우스로 시점 회전. 요우는 리그(XR Origin)에, 피치는 카메라의 부모(Camera Offset)에 적용한다.
    // 카메라 자체는 XR TrackedPoseDriver 가 로컬 포즈를 구동하므로 건드리지 않는다(HMD와 충돌 방지).
    private void HandleMouseLook()
    {
        if (!enableMouseLook) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (holdRightMouseToLook && !mouse.rightButton.isPressed) return;

        Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
        if (d.sqrMagnitude <= 0f) return;

        transform.Rotate(0f, d.x, 0f, Space.World);          // 요우: 리그 회전

        pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);          // 피치: 위/아래
        Transform pivot = cameraTransform != null ? cameraTransform.parent : null;
        if (pivot != null) pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // WASD / 방향키를 XR 스틱과 같은 (x=좌우, y=전후) 벡터로 변환.
    private static Vector2 ReadKeyboardMove()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        Vector2 v = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
        return v.sqrMagnitude > 1f ? v.normalized : v;
    }
}