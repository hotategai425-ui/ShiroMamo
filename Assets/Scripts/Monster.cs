using System.Collections.Generic;
using UnityEngine;

// モンスター本体。Rigidbody で砦に向かって歩き、近づくと攻撃する。
// プレイヤーが aggroRange 内に入るとプレイヤーを優先して狙う。
[RequireComponent(typeof(Rigidbody))]
public class Monster : MonoBehaviour
{
    public static readonly List<Monster> All = new List<Monster>();

    private MonsterData data;
    private Rigidbody rb;
    private float hp;
    private float damage;
    private float speed;
    private float expReward;
    private float dropChance;
    private float attackTimer;
    private bool dead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    // スポーン直後に呼ぶ。ウェーブ補正を反映
    public void Init(MonsterData d, WaveManager.WaveSetting wave)
    {
        data = d;
        hp = d.maxHp * wave.hpMultiplier;
        damage = d.attackDamage * wave.damageMultiplier;
        speed = d.moveSpeed * wave.speedMultiplier;
        expReward = d.expReward * wave.expMultiplier;
        dropChance = d.itemDropChance * wave.dropRateMultiplier;

        transform.localScale *= d.scale;

        if (d.applyTint)
        {
            var r = GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = d.tintColor;
        }
    }

    private void FixedUpdate()
    {
        if (data == null || dead) return;

        // 狙う相手を決める(プレイヤー優先 / なければ砦)
        var player = PlayerStats.Instance;
        bool targetPlayer = player != null && !player.IsDead &&
                            Flat(player.transform.position - transform.position).magnitude <= data.aggroRange;

        Vector3 targetPos;
        float targetRadius;
        if (targetPlayer)
        {
            targetPos = player.transform.position;
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
                if (targetPlayer) player.TakeDamage(damage);
                else Fortress.Instance.TakeDamage(damage);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (dead) return;
        hp -= amount;
        if (hp <= 0f) Die();
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

        Destroy(gameObject);
    }

    public static void DespawnAll()
    {
        foreach (var m in All.ToArray())
            if (m != null) Destroy(m.gameObject);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
