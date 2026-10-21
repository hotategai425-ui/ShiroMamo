using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// プレイヤーのステータス・レベル・HP管理。
// 数値の変更はホストだけが行い、全員に同期される。
[RequireComponent(typeof(PlayerEquipment))]
public class PlayerStats : NetworkBehaviour
{
    // 全プレイヤー(自分も含む) / 自分が操作しているプレイヤー
    public static readonly List<PlayerStats> All = new List<PlayerStats>();
    public static PlayerStats Local { get; private set; }

    [Header("基本ステータス (Lv1)")]
    [SerializeField] private float baseMaxHp = 100f;
    [SerializeField] private float baseAttack = 12f;
    [SerializeField] private float baseDefense = 0f;
    [SerializeField] private float baseMoveSpeed = 6f;

    [Header("レベルごとの上昇量")]
    [SerializeField] private float hpPerLevel = 15f;
    [SerializeField] private float attackPerLevel = 2f;
    [SerializeField] private float defensePerLevel = 0.5f;
    [SerializeField] private float moveSpeedPerLevel = 0.1f;

    [Header("経験値")]
    [SerializeField] private float expToNextBase = 30f;
    [SerializeField] private float expGrowth = 1.4f;

    [Header("出現位置(2人は Z 方向に spacing だけ離れて出現)")]
    [SerializeField] private Vector3 spawnOrigin = new Vector3(5f, 1f, 0f);
    [SerializeField] private float spawnSpacing = 2f;

    [Header("やられた時")]
    [SerializeField] private float respawnDelay = 3f;

    private NetworkVariable<int> level = new NetworkVariable<int>(1);
    private NetworkVariable<float> exp = new NetworkVariable<float>(0f);
    private NetworkVariable<float> currentHp = new NetworkVariable<float>(1f);
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);

    private PlayerEquipment equipment;

    public PlayerEquipment Equipment => equipment;
    public int Level => level.Value;
    public float Exp => exp.Value;
    public float ExpToNext => expToNextBase * Mathf.Pow(expGrowth, Level - 1);
    public float CurrentHp => currentHp.Value;
    public bool IsDead => isDead.Value;

    public float MaxHp => baseMaxHp + hpPerLevel * (Level - 1) + equipment.TotalMaxHp;
    public float Attack => baseAttack + attackPerLevel * (Level - 1) + equipment.TotalAttack;
    public float Defense => baseDefense + defensePerLevel * (Level - 1) + equipment.TotalDefense;
    public float MoveSpeed => baseMoveSpeed + moveSpeedPerLevel * (Level - 1);

    private Vector3 SpawnPosition
    {
        get
        {
            int slot = (int)(OwnerClientId % 2);          // 0 or 1
            float z = (slot * 2 - 1) * spawnSpacing * 0.5f; // -1 or +1 の半分
            return spawnOrigin + new Vector3(0f, 0f, z);
        }
    }

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
    }

    public override void OnNetworkSpawn()
    {
        All.Add(this);

        if (IsServer) currentHp.Value = MaxHp;

        if (IsOwner)
        {
            Local = this;
            StartCoroutine(PlaceAtSpawn());
        }
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
        if (Local == this) Local = null;
    }

    private IEnumerator PlaceAtSpawn()
    {
        yield return null; // 1フレーム待ってから配置(同期の初期化後)
        TeleportLocal(SpawnPosition);
    }

    // 自分のプレイヤーを瞬間移動(オーナーだけが呼べる)
    private void TeleportLocal(Vector3 position)
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.position = position;
        }
        transform.position = position;

        var nt = GetComponent<ClientNetworkTransform>();
        if (nt != null) nt.Teleport(position, transform.rotation, transform.localScale);
    }

    [Rpc(SendTo.Owner)]
    private void TeleportOwnerRpc(Vector3 position)
    {
        TeleportLocal(position);
    }

    [Rpc(SendTo.Owner)]
    private void NotifyOwnerRpc(string message)
    {
        GameLog.Post(message);
    }

    // このプレイヤーの画面にメッセージを出す(ホストから呼ぶ)
    public void Notify(string message)
    {
        if (!IsServer) return;
        NotifyOwnerRpc(message);
    }

    // ---- 以下はホストだけが実行 ----

    public void AddExp(float amount)
    {
        if (!IsServer) return;

        exp.Value += amount;
        while (exp.Value >= ExpToNext)
        {
            float need = ExpToNext;
            exp.Value -= need;
            level.Value = level.Value + 1;
            currentHp.Value = MaxHp; // レベルアップで全回復
            NotifyOwnerRpc($"LEVEL UP! Lv {level.Value}");
        }
    }

    public void TakeDamage(float rawDamage)
    {
        if (!IsServer || isDead.Value) return;

        float dmg = Mathf.Max(1f, rawDamage - Defense);
        currentHp.Value = Mathf.Max(0f, currentHp.Value - dmg);

        if (currentHp.Value <= 0f)
        {
            isDead.Value = true;
            NotifyOwnerRpc("You were defeated... respawning");
            StartCoroutine(RespawnRoutine());
        }
    }

    // 装備変更で最大HPが減った場合に備えて
    public void ClampHp()
    {
        if (!IsServer) return;
        currentHp.Value = Mathf.Min(currentHp.Value, MaxHp);
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        TeleportOwnerRpc(SpawnPosition);
        currentHp.Value = MaxHp;
        isDead.Value = false;
    }
}
