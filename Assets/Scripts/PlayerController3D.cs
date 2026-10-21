using Unity.Netcode;
using UnityEngine;

// カプセル(Rigidbody)の移動。一人称視点で、向いている方向が基準。
// 自分のプレイヤーだけを操作し、位置は ClientNetworkTransform で全員に同期される。
[RequireComponent(typeof(Rigidbody), typeof(PlayerStats))]
public class PlayerController3D : NetworkBehaviour
{
    private Rigidbody rb;
    private PlayerStats stats;
    private CameraFollow cam;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<PlayerStats>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        else
        {
            // 相手のプレイヤーは物理を動かさず、同期された位置に従うだけ
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsOwner) return;

        Vector3 vel = rb.linearVelocity;

        if (stats.IsDead)
        {
            rb.linearVelocity = new Vector3(0f, vel.y, 0f);
            return;
        }

        if (cam == null && Camera.main != null) cam = Camera.main.GetComponent<CameraFollow>();

        float yaw = cam != null ? cam.Yaw : transform.eulerAngles.y;
        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = facing * Vector3.forward;
        Vector3 right = facing * Vector3.right;

        Vector2 input = GameInput.Move;
        Vector3 dir = forward * input.y + right * input.x;

        rb.linearVelocity = new Vector3(dir.x * stats.MoveSpeed, vel.y, dir.z * stats.MoveSpeed);

        // 一人称: 体は常にカメラの向き(左右)に合わせる
        transform.rotation = facing;
    }
}
