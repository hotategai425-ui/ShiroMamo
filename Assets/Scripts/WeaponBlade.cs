using UnityEngine;

// 武器の姿勢とPCの振り動作を担当する。ダメージはPlayerAttack側で確定する。
[DefaultExecutionOrder(100)]
public class WeaponBlade : MonoBehaviour
{
    private PlayerEquipment equipment;
    private PlayerAttack attack;
    private PlayerStats stats;
    private ItemData weapon;
    private bool leftHand;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private int attackId;
    private int equippedIndex;
    private bool swinging;
    private float startedAt;
    private float nextSampleTime;
    private bool remotePoseActive;
    private Vector3 remotePosition;
    private Quaternion remoteRotation;
    private float lastRemoteTime;

    public void Initialize(PlayerEquipment owner, bool left, ItemData item)
    {
        equipment = owner;
        attack = owner.GetComponent<PlayerAttack>();
        stats = owner.GetComponent<PlayerStats>();
        weapon = item;
        leftHand = left;
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;

        var shape = new GameObject("Blade Hitbox");
        shape.transform.SetParent(transform, false);
        Vector3 axis = weapon.bladeTip - weapon.bladeBase;
        shape.transform.localPosition = (weapon.bladeBase + weapon.bladeTip) * 0.5f;
        if (axis.sqrMagnitude > 0f) shape.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
        var capsule = shape.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = Mathf.Max(0.01f, weapon.bladeRadius);
        capsule.height = axis.magnitude + capsule.radius * 2f;
    }

    public void StartSwing(int id)
    {
        attackId = id;
        equippedIndex = equipment.GetEquippedWeaponIndex(leftHand);
        startedAt = Time.time;
        nextSampleTime = 0f;
        swinging = true;
    }

    public void ShowRemotePose(int id, Vector3 position, Quaternion rotation, float progress)
    {
        if (id < attackId) return;
        attackId = id;
        remotePosition = position;
        remoteRotation = rotation;
        lastRemoteTime = Time.time;
        remotePoseActive = progress < 1f;
        if (!remotePoseActive) RestorePose();
    }

    private void LateUpdate()
    {
        if (equipment == null || !equipment.IsSpawned) return;
        if (!equipment.IsOwner)
        {
            if (remotePoseActive)
            {
                if (Time.time - lastRemoteTime > 0.5f)
                {
                    remotePoseActive = false;
                    RestorePose();
                }
                else
                {
                    float blend = 1f - Mathf.Exp(-30f * Time.deltaTime);
                    transform.SetPositionAndRotation(Vector3.Lerp(transform.position, remotePosition, blend),
                        Quaternion.Slerp(transform.rotation, remoteRotation, blend));
                }
            }
            return;
        }

        if (!swinging) return;
        if (stats.IsDead || equipment.IsInventoryOpen || equipment.GetEquippedWeaponIndex(leftHand) != equippedIndex)
        {
            CancelSwing();
            return;
        }

        float progress = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.1f, weapon.swingDuration));
        float side = leftHand ? -1f : 1f;
        Quaternion windup = Quaternion.Euler(55f, 0f, -side * 65f);
        Quaternion finish = Quaternion.Euler(100f, 0f, side * 65f);
        if (progress < 0.2f)
            transform.localRotation = Quaternion.Slerp(restRotation, windup, Mathf.SmoothStep(0f, 1f, progress / 0.2f));
        else if (progress < 0.7f)
            transform.localRotation = Quaternion.Slerp(windup, finish, Mathf.SmoothStep(0f, 1f, (progress - 0.2f) / 0.5f));
        else
            transform.localRotation = Quaternion.Slerp(finish, restRotation, Mathf.SmoothStep(0f, 1f, (progress - 0.7f) / 0.3f));
        transform.localPosition = restPosition + Vector3.forward * (0.12f * Mathf.Sin(progress * Mathf.PI));

        if (Time.time >= nextSampleTime || progress >= 1f)
        {
            attack.SubmitMeleePose(leftHand, attackId, transform.position, transform.rotation, progress);
            nextSampleTime = Time.time + 0.02f;
        }
        if (progress >= 1f)
        {
            swinging = false;
            RestorePose();
        }
    }

    private void RestorePose()
    {
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
    }

    private void OnDisable() => CancelSwing();

    private void CancelSwing()
    {
        if (!swinging) return;
        swinging = false;
        if (equipment != null && equipment.IsSpawned && equipment.IsOwner)
            attack.CancelMeleeAttack(leftHand, attackId);
        RestorePose();
    }
}
