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

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!controller.enabled) return;

        if (controller.isGrounded && playerVelocity.y < 0)
            playerVelocity.y = -2f;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

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
    }
}