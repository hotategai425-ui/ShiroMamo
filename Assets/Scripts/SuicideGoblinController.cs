using System;
using Unity.Netcode;
using UnityEngine;

// 専用Prefabだけに付ける。移動はMonster、準備時計と爆発はホストが管理する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Monster), typeof(ExplosionPreparationVisual))]
public sealed class SuicideGoblinController : NetworkBehaviour
{
    [SerializeField, Min(0.01f)] private float preparationSeconds = 3f;
    [SerializeField, Min(0f)] private float explosionRadius = 3f;
    [SerializeField, Min(0f)] private float explosionDamage = 20f;

    public struct PreparationState : INetworkSerializable, IEquatable<PreparationState>
    {
        public bool Preparing;
        public float Remaining, Duration;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Preparing);
            serializer.SerializeValue(ref Remaining);
            serializer.SerializeValue(ref Duration);
        }
        public bool Equals(PreparationState other) => Preparing == other.Preparing
            && Remaining.Equals(other.Remaining) && Duration.Equals(other.Duration);
    }

    private readonly NetworkVariable<PreparationState> preparation = new();
    private Monster monster;
    private Rigidbody body;
    private ExplosionPreparationVisual visual;
    private bool resolved;

    private void Awake()
    {
        monster = GetComponent<Monster>();
        body = GetComponent<Rigidbody>();
        visual = GetComponent<ExplosionPreparationVisual>();
    }

    public override void OnNetworkSpawn()
    {
        resolved = false;
        preparation.OnValueChanged += OnPreparationChanged;
        ApplyVisual(preparation.Value);
    }

    public override void OnNetworkDespawn()
    {
        resolved = true;
        preparation.OnValueChanged -= OnPreparationChanged;
        visual.SetPreparationVisual(false, 0f, 0f);
    }

    private void OnPreparationChanged(PreparationState previous, PreparationState current) => ApplyVisual(current);
    private void ApplyVisual(PreparationState state) => visual.SetPreparationVisual(state.Preparing, state.Remaining, state.Duration);

    // Monsterのノックバックearly returnとは独立して進める。停止中は同期値も位相も変えない。
    private void Update()
    {
        if (!IsServer || !IsSpawned || resolved || !monster.IsAlive) return;
        if (TeamProgression.IsPaused(NetworkManager) || CreatorMode.AreMonstersPaused(NetworkManager)) return;
        var state = preparation.Value;
        if (!state.Preparing) return;
        state.Remaining = Mathf.Max(0f, state.Remaining - Time.deltaTime);
        preparation.Value = state;
        if (state.Remaining <= 0f) Explode();
    }

    internal void MoveTowardsFortress(float speed, float attackRange)
    {
        Fortress fortress = FindFortress(NetworkManager);
        Vector3 velocity = body.linearVelocity;
        if (preparation.Value.Preparing || fortress == null)
        {
            body.linearVelocity = new Vector3(0f, velocity.y, 0f);
            return;
        }
        Vector3 to = fortress.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude - fortress.Radius <= attackRange)
        {
            body.linearVelocity = new Vector3(0f, velocity.y, 0f);
            float duration = Mathf.Max(0.01f, preparationSeconds);
            preparation.Value = new PreparationState { Preparing = true, Remaining = duration, Duration = duration };
            return;
        }
        Vector3 direction = to.normalized;
        body.linearVelocity = new Vector3(direction.x * speed, velocity.y, direction.z * speed);
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.fixedDeltaTime);
    }

    internal static Fortress FindFortress(NetworkManager manager)
    {
        if (manager == null || manager.SpawnManager == null) return null;
        foreach (var obj in manager.SpawnManager.SpawnedObjectsList)
        {
            var fortress = obj.GetComponent<Fortress>();
            if (fortress != null && fortress.IsServer && fortress.CurrentHp > 0f) return fortress;
        }
        return null;
    }

    private void Explode()
    {
        if (resolved || !monster.IsAlive) return;
        resolved = true; // Damage callbacks / subsequent frames cannot resolve twice.
        Vector3 origin = transform.position;
        foreach (var player in PlayerStats.All.ToArray())
        {
            if (player == null || !player.IsSpawned || !player.IsServer || player.IsDead || player.NetworkManager != NetworkManager) continue;
            foreach (var shape in player.GetComponentsInChildren<Collider>())
            {
                if (!shape.enabled || shape.isTrigger) continue;
                Vector3 point = shape.ClosestPoint(origin);
                if ((point - origin).sqrMagnitude > explosionRadius * explosionRadius || !HasSight(origin, point)) continue;
                player.TakeDamage(explosionDamage); // Source-less: shield reduction applies, reflection does not.
                break;
            }
        }
        var fortress = FindFortress(NetworkManager);
        if (fortress != null)
        {
            Vector3 target = fortress.transform.position;
            // The current fortress is a tall cylinder. Use its configured combat radius and collider height.
            Bounds bounds = new Bounds(target, Vector3.zero);
            bool found = false;
            foreach (var shape in fortress.GetComponentsInChildren<Collider>())
            {
                if (!shape.enabled || shape.isTrigger) continue;
                if (!found) { bounds = shape.bounds; found = true; }
                else bounds.Encapsulate(shape.bounds);
            }
            target.y = found ? Mathf.Clamp(origin.y, bounds.min.y, bounds.max.y) : target.y;
            Vector3 horizontal = origin - target;
            horizontal.y = 0f;
            Vector3 surface = target + horizontal.normalized * Mathf.Min(horizontal.magnitude, fortress.Radius);
            if ((surface - origin).sqrMagnitude <= explosionRadius * explosionRadius && HasSight(origin, target))
                fortress.TakeDamage(explosionDamage);
        }
        // Do not call Monster.Die: self-destruction has no kill rewards.
        if (IsSpawned) NetworkObject.Despawn(true);
    }

    private static bool HasSight(Vector3 origin, Vector3 point)
    {
        Vector3 delta = point - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f) return true;
        foreach (var hit in Physics.RaycastAll(origin, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            if (!IsActor(hit.collider)) return false;
        return true;
    }

    internal static bool IsActor(Collider shape) => shape.GetComponentInParent<Monster>() != null
        || shape.GetComponentInParent<PlayerStats>() != null || shape.GetComponentInParent<Fortress>() != null
        || shape.GetComponentInParent<Pickup>() != null || shape.GetComponentInParent<MagicProjectile>() != null;
}
