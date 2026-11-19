using System.Collections.Generic;
using UnityEngine;

// 入力方式に依存せず、刃の前回と今回の姿勢から接触を調べる。
public static class MeleeHitResolver
{
    // trueは環境接触による打切り。敵への接触では振りを消費しない。
    public static bool SweepAll(ItemData weapon, Vector3 previousPosition, Quaternion previousRotation,
        Vector3 position, Quaternion rotation, PlayerStats attacker, List<Monster> targets)
    {
        float scale = Mathf.Max(0.1f, weapon.meleeScale);
        float radius = Mathf.Max(0.01f, weapon.bladeRadius) * scale;
        float bladeReach = Mathf.Max(weapon.bladeBase.magnitude, weapon.bladeTip.magnitude) * scale;
        float travel = Vector3.Distance(previousPosition, position)
            + Quaternion.Angle(previousRotation, rotation) * Mathf.Deg2Rad * bladeReach;
        int steps = Mathf.Clamp(Mathf.CeilToInt(travel / Mathf.Max(0.01f, radius)), 1, 256);

        for (int i = 0; i <= steps; i++)
        {
            float fraction = i / (float)steps;
            Vector3 grip = Vector3.Lerp(previousPosition, position, fraction);
            Quaternion facing = Quaternion.Slerp(previousRotation, rotation, fraction);
            Vector3 bladeBase = grip + facing * (weapon.bladeBase * scale);
            Vector3 bladeTip = grip + facing * (weapon.bladeTip * scale);
            var overlaps = Physics.OverlapCapsule(bladeBase, bladeTip, radius,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            // 同じサンプルで環境にも触れた場合は、従来どおりこの姿勢から先を遮断する。
            foreach (var overlap in overlaps)
            {
                if (overlap.GetComponentInParent<PlayerStats>() != null) continue;
                if (overlap.GetComponentInParent<Monster>() == null) return true;
            }
            foreach (var overlap in overlaps)
            {
                var monster = overlap.GetComponentInParent<Monster>();
                if (monster == null || !monster.IsSpawned || !monster.IsServer
                    || monster.NetworkManager != attacker.NetworkManager || targets.Contains(monster)) continue;
                Vector3 contact = overlap.ClosestPoint((bladeBase + bladeTip) * 0.5f);
                if (!IsMeleeOccluded(attacker, contact)) targets.Add(monster);
            }
        }
        return false;
    }

    private static bool IsMeleeOccluded(PlayerStats attacker, Vector3 contact)
    {
        Vector3 origin = attacker.transform.position + Vector3.up * 0.6f;
        Vector3 direction = contact - origin;
        var hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<PlayerStats>() != null) continue;
            if (hit.collider.GetComponentInParent<Monster>() != null) continue;
            return true;
        }
        return false;
    }

    public static bool Sweep(ItemData weapon, Vector3 previousPosition, Quaternion previousRotation,
        Vector3 position, Quaternion rotation, PlayerStats attacker, out Monster target)
    {
        target = null;
        float scale = Mathf.Max(0.1f, weapon.meleeScale);
        float radius = Mathf.Max(0.01f, weapon.bladeRadius) * scale;
        float bladeReach = Mathf.Max(weapon.bladeBase.magnitude, weapon.bladeTip.magnitude) * scale;
        float travel = Vector3.Distance(previousPosition, position)
            + Quaternion.Angle(previousRotation, rotation) * Mathf.Deg2Rad * bladeReach;
        int steps = Mathf.Clamp(Mathf.CeilToInt(travel / Mathf.Max(0.01f, radius)), 1, 256);

        for (int i = 0; i <= steps; i++)
        {
            float fraction = i / (float)steps;
            Vector3 grip = Vector3.Lerp(previousPosition, position, fraction);
            Quaternion facing = Quaternion.Slerp(previousRotation, rotation, fraction);
            Vector3 bladeBase = grip + facing * (weapon.bladeBase * scale);
            Vector3 bladeTip = grip + facing * (weapon.bladeTip * scale);
            var overlaps = Physics.OverlapCapsule(bladeBase, bladeTip, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            Monster nearest = null;
            Collider nearestCollider = null;
            float bestDistance = float.MaxValue;
            foreach (var overlap in overlaps)
            {
                if (overlap.GetComponentInParent<PlayerStats>() != null) continue;
                var monster = overlap.GetComponentInParent<Monster>();
                if (monster == null) return true; // 壁に触れた一振りは、その先へダメージを通さない。
                if (!monster.IsSpawned) continue;
                float distance = (overlap.ClosestPoint(grip) - grip).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = monster;
                    nearestCollider = overlap;
                }
            }
            if (nearest == null) continue;
            Vector3 contact = nearestCollider.ClosestPoint((bladeBase + bladeTip) * 0.5f);
            if (!IsOccluded(attacker, contact, nearest)) target = nearest;
            return true;
        }
        return false;
    }

    public static bool IsOccluded(PlayerStats attacker, Vector3 contact, Monster target)
    {
        Vector3 origin = attacker.transform.position + Vector3.up * 0.6f;
        Vector3 direction = contact - origin;
        var hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<PlayerStats>() != null) continue;
            var monster = hit.collider.GetComponentInParent<Monster>();
            if (monster == target || (monster != null && !monster.IsSpawned)) continue;
            return true;
        }
        return false;
    }
}
