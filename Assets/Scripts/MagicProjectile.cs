using System.Collections.Generic;
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
    private bool isCritical;
    private PlayerStats attacker;
    private bool impacted;
    private WeaponSkillLoadout skills;
    private int targetLimit = 1;
    private readonly HashSet<ulong> hitEnemies = new HashSet<ulong>();
    private readonly List<Contact> contacts = new List<Contact>();

    private struct Contact
    {
        public Collider Collider;
        public float Distance;
        public bool Environment;
        public ulong EnemyId;
        public float Proximity;
    }

    public void Init(Vector3 direction, float speed, float lifetime, float attackDamage)
    {
        velocity = direction.normalized * Mathf.Max(0.1f, speed);
        remainingTime = Mathf.Max(0.1f, lifetime);
        damage = attackDamage;
        isCritical = false;
        attacker = null;
        impacted = false;
        skills = default;
        targetLimit = 1;
        hitEnemies.Clear();
    }

    public void Init(Vector3 direction, float speed, float lifetime, AttackDamageResult attackDamage, PlayerStats source)
    {
        Init(direction, speed, lifetime, attackDamage.Amount);
        isCritical = attackDamage.IsCritical;
        attacker = source;
    }

    public void Init(Vector3 direction, float speed, float lifetime, AttackDamageResult attackDamage,
        PlayerStats source, WeaponSkillLoadout attackSkills)
    {
        Init(direction, speed, lifetime, attackDamage, source);
        skills = attackSkills;
        targetLimit = WeaponSkillSettings.Current.GetPierceTargetLimit(skills.Count(WeaponSkillId.Pierce));
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned || Time.timeScale == 0f) return;
        var wave = WaveManager.Instance;
        if (remainingTime <= 0f || wave == null || wave.Phase != GamePhase.Combat)
        {
            NetworkObject.Despawn(true);
            return;
        }

        // 先に全候補を収集する。ダメージによるDespawnで検索・距離順を壊さない。
        contacts.Clear();
        foreach (var overlap in Physics.OverlapSphere(transform.position, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            // 出現時に壁内部なら、その位置の敵を通して先へ進めない。
            if (AddContact(overlap, 0f) && overlap.GetComponentInParent<Monster>() == null)
            {
                Stop();
                return;
            }
        }

        float stepTime = Mathf.Min(Time.fixedDeltaTime, remainingTime);
        float distance = velocity.magnitude * stepTime;
        var hits = Physics.SphereCastAll(transform.position, radius, velocity.normalized, distance,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            AddContact(hit.collider, hit.distance);
        }
        contacts.Sort((a, b) =>
        {
            int order = a.Distance.CompareTo(b.Distance);
            if (order != 0) return order;
            // 同距離なら環境を優先。物理検索が返す未定義の配列順に依存しない。
            if (a.Environment != b.Environment) return a.Environment ? -1 : 1;
            order = a.Proximity.CompareTo(b.Proximity);
            if (order != 0) return order;
            return a.EnemyId.CompareTo(b.EnemyId);
        });
        Vector3 start = transform.position;
        foreach (var contact in contacts)
        {
            if (contact.Collider == null) continue;
            transform.position = start + velocity.normalized * contact.Distance;
            Impact(contact.Collider);
            if (impacted || !IsSpawned) return;
        }
        transform.position = start + velocity.normalized * distance;
        remainingTime -= stepTime;
    }

    private bool AddContact(Collider collider, float distance)
    {
        if (collider.GetComponentInParent<PlayerStats>() != null) return false;
        var monster = collider.GetComponentInParent<Monster>();
        if (monster != null && (!monster.IsAlive || !monster.IsServer || monster.NetworkManager != NetworkManager)) return false;
        contacts.Add(new Contact { Collider = collider, Distance = distance,
            Environment = monster == null, EnemyId = monster != null ? monster.NetworkObjectId : 0,
            Proximity = distance == 0f ? (collider.ClosestPoint(transform.position) - transform.position).sqrMagnitude : 0f });
        return true;
    }

    private void Impact(Collider other)
    {
        if (!IsServer || !IsSpawned || impacted) return;
        var monster = other.GetComponentInParent<Monster>();
        if (monster == null)
        {
            Stop();
            return;
        }
        if (monster.IsAlive && monster.IsServer && monster.NetworkManager == NetworkManager
            && hitEnemies.Add(monster.NetworkObjectId))
        {
            Vector3 direction = velocity;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = -monster.transform.forward;
            monster.TryTakeDamage(DamageContext.Direct(
                new AttackDamageResult { Amount = damage, IsCritical = isCritical }, attacker, skills), direction);
            // 0等の不受理ダメージでも、正当な敵への接触数は消費する（旧Initの停止動作も維持）。
            if (hitEnemies.Count >= targetLimit) Stop();
        }
    }

    private void Stop()
    {
        impacted = true;
        if (IsSpawned) NetworkObject.Despawn(true);
    }
}
