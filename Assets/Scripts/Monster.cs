using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// モンスター本体。動きと戦闘はホストだけが計算し、位置は NetworkTransform で全員に同期される。
// 近くにいる(生きている)プレイヤーのうち一番近い人を優先して狙い、いなければ砦を狙う。
[RequireComponent(typeof(Rigidbody))]
public class Monster : NetworkBehaviour
{
    public static readonly List<Monster> All = new List<Monster>();

    // 色の同期。アルファ0 は「色を変えない」の意味
    private NetworkVariable<Color> tint = new NetworkVariable<Color>(new Color(0f, 0f, 0f, 0f));

    private MonsterData data;
    private Rigidbody rb;
    private float hp;
    private float damage;
    private float speed;
    private float expReward;
    private float dropChance;
    private float attackTimer;
    private bool dead;
    private Vector3 knockbackDirection;
    private float knockbackRemaining;
    private Color pendingTint = new Color(0f, 0f, 0f, 0f);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    // ホストが Spawn する「前」に呼ぶ。ウェーブ補正を反映
    public void ServerInit(MonsterData d, WaveManager.WaveSetting wave)
    {
        data = d;
        hp = d.maxHp * wave.hpMultiplier;
        damage = d.attackDamage * wave.damageMultiplier;
        speed = d.moveSpeed * wave.speedMultiplier;
        expReward = d.expReward * wave.expMultiplier;
        dropChance = d.itemDropChance * wave.dropRateMultiplier;

        transform.localScale *= d.scale;
        pendingTint = d.applyTint ? d.tintColor : new Color(0f, 0f, 0f, 0f);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            tint.Value = pendingTint;
        }
        else
        {
            // 参加者側では物理を動かさず、同期された位置に従うだけ
            rb.isKinematic = true;
        }

        ApplyTint(tint.Value);
        tint.OnValueChanged += OnTintChanged;
    }

    public override void OnNetworkDespawn()
    {
        tint.OnValueChanged -= OnTintChanged;
    }

    private void OnTintChanged(Color previous, Color current)
    {
        ApplyTint(current);
    }

    private void ApplyTint(Color c)
    {
        if (c.a <= 0f) return;
        var r = GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = c;
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned || data == null || dead) return;

        if (knockbackRemaining > 0f && data.knockbackSpeed > 0f && data.knockbackDuration > 0f)
        {
            MoveKnockback();
            return;
        }
        knockbackRemaining = 0f;

        // 狙う相手を決める(近くの生きているプレイヤー優先 / いなければ砦)
        PlayerStats targetPlayer = null;
        float best = data.aggroRange;
        foreach (var p in PlayerStats.All)
        {
            if (p == null || p.IsDead) continue;
            float d = Flat(p.transform.position - transform.position).magnitude;
            if (d <= best)
            {
                best = d;
                targetPlayer = p;
            }
        }

        Vector3 targetPos;
        float targetRadius;
        if (targetPlayer != null)
        {
            targetPos = targetPlayer.transform.position;
            targetRadius = 0.5f;
        }
        else if (Fortress.Instance != null)
        {
            targetPos = Fortress.Instance.transform.position;
            targetRadius = Fortress.Instance.Radius;
        }
        else return;

        Vector3 to = Flat(targetPos - transform.position);
        float dist = to.magnitude - targetRadius;
        Vector3 vel = rb.linearVelocity;

        if (dist > data.attackRange)
        {
            // 移動
            Vector3 dir = to.normalized;
            rb.linearVelocity = new Vector3(dir.x * speed, vel.y, dir.z * speed);
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.fixedDeltaTime);
        }
        else
        {
            // 攻撃
            rb.linearVelocity = new Vector3(0f, vel.y, 0f);
            attackTimer -= Time.fixedDeltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = data.attackInterval;
                if (targetPlayer != null) targetPlayer.TakeDamage(damage);
                else Fortress.Instance.TakeDamage(damage);
            }
        }
    }

    // プレイヤーの攻撃から呼ばれる(ホストのみ有効)
    public void TakeDamage(float amount, Vector3 hitDirection = default)
    {
        if (!IsServer || dead) return;
        hp -= amount;
        if (hp <= 0f)
        {
            Die();
            return;
        }

        Vector3 direction = Flat(hitDirection);
        if (amount <= 0f || data == null || data.knockbackSpeed <= 0f || data.knockbackDuration <= 0f
            || !(direction.sqrMagnitude > 0f) || float.IsInfinity(direction.sqrMagnitude)) return;

        // 重ねて被弾した場合は、最後の攻撃方向へ押し戻す。速度は加算しない。
        knockbackDirection = direction.normalized;
        knockbackRemaining = data.knockbackDuration;
    }

    private void MoveKnockback()
    {
        float stepTime = Mathf.Min(Time.fixedDeltaTime, knockbackRemaining);
        float speed = data.knockbackSpeed * Mathf.Clamp01(knockbackRemaining / data.knockbackDuration);
        float distance = speed * stepTime;
        var hits = rb.SweepTestAll(knockbackDirection, distance + 0.05f, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            // 床への平行な接触は除き、壁や砦など進路を遮る面で止める。
            if (Vector3.Dot(knockbackDirection, hit.normal) < -0.01f)
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
        }

        Vector3 velocity = knockbackDirection * (distance / Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        knockbackRemaining = Mathf.Max(0f, knockbackRemaining - stepTime);
    }

    private void Die()
    {
        dead = true;

        if (DropManager.Instance != null)
        {
            DropManager.Instance.SpawnExp(transform.position, expReward);

            if (Random.value < dropChance)
            {
                ItemData item = data.RollDrop();
                if (item != null) DropManager.Instance.SpawnItem(transform.position, item);
            }
        }

        if (IsSpawned) NetworkObject.Despawn(true);
    }

    public static void DespawnAll()
    {
        foreach (var m in All.ToArray())
        {
            if (m != null && m.IsSpawned && m.IsServer) m.NetworkObject.Despawn(true);
        }
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
