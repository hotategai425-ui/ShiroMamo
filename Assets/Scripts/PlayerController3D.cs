using Unity.Netcode;
using UnityEngine;

// カプセル(Rigidbody)の移動。一人称視点で、向いている方向が基準。
// 自分のプレイヤーだけを操作し、位置は ClientNetworkTransform で全員に同期される。
[RequireComponent(typeof(Rigidbody), typeof(PlayerStats), typeof(CapsuleCollider))]
public class PlayerController3D : NetworkBehaviour
{
    private Rigidbody rb;
    private PlayerStats stats;
    private CameraFollow cam;
    private CapsuleCollider capsule;
    private bool jumpRequested;
    private bool blinkRequested;
    private Vector2 blinkInput;
    private float nextBlinkTime;
    private Vector3 blinkDirection;
    private float blinkRemainingDistance;
    private float blinkSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<PlayerStats>();
        capsule = GetComponent<CapsuleCollider>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public override void OnNetworkSpawn()
    {
        jumpRequested = false;
        blinkRequested = false;
        nextBlinkTime = 0f;
        blinkRemainingDistance = 0f;
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

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        if (stats.IsDead || stats.Equipment.IsInventoryOpen)
        {
            jumpRequested = false;
            blinkRequested = false;
            return;
        }

        // フレーム間の短い入力も、次の物理更新で1回だけ処理する。
        jumpRequested |= GameInput.JumpPressed;
        if (GameInput.BlinkPressed)
        {
            blinkRequested = true;
            blinkInput = GameInput.Move;
        }
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsOwner) return;

        Vector3 vel = rb.linearVelocity;

        if (stats.IsDead || stats.Equipment.IsInventoryOpen)
        {
            jumpRequested = false;
            blinkRequested = false;
            blinkRemainingDistance = 0f;
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

        if (jumpRequested && vel.y <= 0.1f && IsGrounded())
            vel.y = Mathf.Sqrt(2f * Mathf.Max(0f, -Physics.gravity.y) * stats.JumpHeight);
        jumpRequested = false;

        // 一人称: 体は常にカメラの向き(左右)に合わせる
        transform.rotation = facing;

        if (blinkRequested) TryBlink(forward * blinkInput.y + right * blinkInput.x);
        blinkRequested = false;

        Vector3 horizontalVelocity = blinkRemainingDistance > 0f ? StepBlink() : dir * stats.MoveSpeed;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, vel.y, horizontalVelocity.z);
    }

    private bool IsGrounded()
    {
        Bounds bounds = capsule.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f;
        float distance = bounds.extents.y - radius + 0.08f;
        var hits = Physics.SphereCastAll(bounds.center, radius, Vector3.down, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.rigidbody != rb && hit.normal.y >= 0.5f) return true;
        }
        return false;
    }

    private void TryBlink(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f || Time.time < nextBlinkTime || blinkRemainingDistance > 0f) return;

        direction.Normalize();
        float distance = BlinkClearance(direction, stats.BlinkDistance);
        if (distance <= 0.01f) return;

        blinkDirection = direction;
        blinkRemainingDistance = distance;
        blinkSpeed = stats.BlinkDistance / stats.BlinkDuration;
        nextBlinkTime = Time.time + stats.BlinkCooldown;
    }

    private Vector3 StepBlink()
    {
        float distance = Mathf.Min(blinkSpeed * Time.fixedDeltaTime, blinkRemainingDistance);
        // 移動中に進路へ入ってきた障害物も、各物理更新で確認する。
        float clearance = BlinkClearance(blinkDirection, distance);
        if (clearance < distance)
        {
            distance = clearance;
            blinkRemainingDistance = 0f;
        }
        else
        {
            blinkRemainingDistance = Mathf.Max(0f, blinkRemainingDistance - distance);
        }
        return blinkDirection * (distance / Time.fixedDeltaTime);
    }

    private float BlinkClearance(Vector3 direction, float distance)
    {
        var hits = rb.SweepTestAll(direction, distance + 0.05f, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            // 接地中の床など、進行方向を遮らない接触は除く。
            if (Vector3.Dot(direction, hit.normal) < -0.01f)
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
        }
        return distance;
    }
}
