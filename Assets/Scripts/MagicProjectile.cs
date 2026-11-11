using Unity.Netcode;
using UnityEngine;

// 球の移動と命中はホストが処理し、位置はNetworkTransformで同期する。
[RequireComponent(typeof(NetworkObject))]
public class MagicProjectile : NetworkBehaviour
{
    [Min(0.01f), SerializeField] private float radius = 0.15f;

    private Vector3 velocity;
    private float remainingTime;
    private float damage;

    public void Init(Vector3 direction, float speed, float lifetime, float attackDamage)
    {
        velocity = direction.normalized * Mathf.Max(0.1f, speed);
        remainingTime = Mathf.Max(0.1f, lifetime);
        damage = attackDamage;
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned) return;
        var wave = WaveManager.Instance;
        if (remainingTime <= 0f || wave == null || wave.Phase != GamePhase.Combat)
        {
            NetworkObject.Despawn(true);
            return;
        }

        // 壁に密着して発射しても、壁の内側から通り抜けないようにする。
        foreach (var overlap in Physics.OverlapSphere(transform.position, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (overlap.GetComponentInParent<PlayerStats>() != null) continue;
            Impact(overlap);
            return;
        }

        float stepTime = Mathf.Min(Time.fixedDeltaTime, remainingTime);
        float distance = velocity.magnitude * stepTime;
        Collider target = null;
        var hits = Physics.SphereCastAll(transform.position, radius, velocity.normalized, distance,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            // 自分と相棒への攻撃を除外し、最初に当たる敵または地形を選ぶ。
            if (hit.collider.GetComponentInParent<PlayerStats>() != null || hit.distance > distance) continue;
            distance = hit.distance;
            target = hit.collider;
        }

        transform.position += velocity.normalized * distance;
        remainingTime -= stepTime;
        if (target != null) Impact(target);
    }

    private void Impact(Collider other)
    {
        var monster = other.GetComponentInParent<Monster>();
        if (monster != null && monster.IsSpawned) monster.TakeDamage(damage);
        NetworkObject.Despawn(true);
    }
}
