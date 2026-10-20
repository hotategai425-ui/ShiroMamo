using UnityEngine;

// カプセル(Rigidbody)の移動。カメラ基準で WASD 移動
[RequireComponent(typeof(Rigidbody), typeof(PlayerStats))]
public class PlayerController3D : MonoBehaviour
{
    [SerializeField] private float turnSpeed = 14f;

    private Rigidbody rb;
    private PlayerStats stats;
    private Transform cam;
    private CameraFollow fpCam;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<PlayerStats>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void FixedUpdate()
    {
        Vector3 vel = rb.linearVelocity;

        if (stats.IsDead)
        {
            rb.linearVelocity = new Vector3(0f, vel.y, 0f);
            return;
        }

        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
            fpCam = cam.GetComponent<CameraFollow>();
        }

        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;

        Vector2 input = GameInput.Move;
        Vector3 dir = forward * input.y + right * input.x;

        rb.linearVelocity = new Vector3(dir.x * stats.MoveSpeed, vel.y, dir.z * stats.MoveSpeed);

        if (fpCam != null)
        {
            // 一人称: 体は常にカメラの向き(左右)に合わせる
            transform.rotation = Quaternion.Euler(0f, fpCam.Yaw, 0f);
        }
        else if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.fixedDeltaTime);
        }
    }
}