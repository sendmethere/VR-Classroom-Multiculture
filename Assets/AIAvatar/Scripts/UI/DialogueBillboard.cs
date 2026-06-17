using UnityEngine;

namespace AIAvatar
{
    /// <summary>
    /// Keeps the world-space dialogue panel readable: it orbits around the avatar
    /// to the player's side (so it never ends up behind the character) and turns to
    /// face the player. Both position and rotation are smoothed and snap instantly
    /// the moment the panel is shown. Put this on the Dialogue Canvas.
    /// </summary>
    [AddComponentMenu("AI Avatar/Dialogue Billboard")]
    public class DialogueBillboard : MonoBehaviour
    {
        [Tooltip("바라볼 대상 (플레이어/HMD). 비우면 런타임에 Camera.main")]
        [SerializeField] private Transform target;

        [Tooltip("이 오브젝트(아바타) 주위를 돌며 플레이어 쪽으로 배치")]
        [SerializeField] private Transform anchor;

        [Header("Position")]
        [Tooltip("anchor 주위로 플레이어 쪽에 배치할지 (끄면 현재 위치 유지, 회전만)")]
        [SerializeField] private bool followPosition = true;
        [Tooltip("아바타로부터 플레이어 쪽으로 띄울 거리(m)")]
        [SerializeField] private float orbitDistance = 0.75f;
        [Tooltip("anchor 기준 패널 높이(m)")]
        [SerializeField] private float heightOffset = 1.5f;
        [Tooltip("좌우 이동: +면 캐릭터 기준 오른쪽, -면 왼쪽 (m)")]
        [SerializeField] private float lateralOffset = 0.25f;
        [SerializeField] private float positionLerp = 8f;

        [Header("Facing")]
        [SerializeField] private bool faceTarget = true;
        [Tooltip("글자가 좌우 반전돼 보이면 켜기 (180° 뒤집기)")]
        [SerializeField] private bool flipFacing = true;
        [Tooltip("수평으로만 회전(상하 기울임 없음)")]
        [SerializeField] private bool keepUpright = true;
        [SerializeField] private float rotationLerp = 8f;

        private void OnEnable() => UpdatePose(true);   // 보일 때 즉시 정위치로 스냅
        private void LateUpdate() => UpdatePose(false); // 이후 매 프레임 부드럽게 추종

        private void UpdatePose(bool snap)
        {
            if (target == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                target = cam.transform;
            }

            Vector3 playerPos = target.position;

            if (followPosition && anchor != null)
            {
                Vector3 dir = playerPos - anchor.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) dir = -anchor.forward; // 거의 겹칠 때 폴백
                dir.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, dir); // 캐릭터가 플레이어를 볼 때의 오른쪽
                Vector3 desired = anchor.position
                                  + dir * orbitDistance
                                  + right * lateralOffset
                                  + Vector3.up * heightOffset;
                transform.position = snap
                    ? desired
                    : Vector3.Lerp(transform.position, desired, Smooth(positionLerp));
            }

            if (faceTarget)
            {
                Vector3 toPlayer = playerPos - transform.position;
                if (keepUpright) toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 1e-4f)
                {
                    // flipFacing이면 정면을 반대로 (좌우 반전 교정)
                    Vector3 face = flipFacing ? -toPlayer : toPlayer;
                    Quaternion desiredRot = Quaternion.LookRotation(face.normalized, Vector3.up);
                    transform.rotation = snap
                        ? desiredRot
                        : Quaternion.Slerp(transform.rotation, desiredRot, Smooth(rotationLerp));
                }
            }
        }

        // Framerate-independent smoothing factor.
        private static float Smooth(float k) => 1f - Mathf.Exp(-k * Time.deltaTime);
    }
}
