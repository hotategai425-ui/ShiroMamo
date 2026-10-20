using System.Collections;
using UnityEngine;

// プレイヤーのステータス・レベル・HP管理
[RequireComponent(typeof(PlayerEquipment))]
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

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

    [Header("やられた時")]
    [SerializeField] private float respawnDelay = 3f;

    private PlayerEquipment equipment;
    private Vector3 spawnPosition;

    public PlayerEquipment Equipment => equipment;
    public int Level { get; private set; } = 1;
    public float Exp { get; private set; }
    public float ExpToNext { get; private set; }
    public float CurrentHp { get; private set; }
    public bool IsDead { get; private set; }

    public float MaxHp => baseMaxHp + hpPerLevel * (Level - 1) + equipment.TotalMaxHp;
    public float Attack => baseAttack + attackPerLevel * (Level - 1) + equipment.TotalAttack;
    public float Defense => baseDefense + defensePerLevel * (Level - 1) + equipment.TotalDefense;
    public float MoveSpeed => baseMoveSpeed + moveSpeedPerLevel * (Level - 1);

    private void Awake()
    {
        Instance = this;
        equipment = GetComponent<PlayerEquipment>();
        spawnPosition = transform.position;
        ExpToNext = expToNextBase;
        CurrentHp = MaxHp;
    }

    public void AddExp(float amount)
    {
        Exp += amount;
        while (Exp >= ExpToNext)
        {
            Exp -= ExpToNext;
            Level++;
            ExpToNext = expToNextBase * Mathf.Pow(expGrowth, Level - 1);
            CurrentHp = MaxHp; // レベルアップで全回復
            GameLog.Post($"LEVEL UP! Lv {Level}");
        }
    }

    public void TakeDamage(float rawDamage)
    {
        if (IsDead) return;

        float dmg = Mathf.Max(1f, rawDamage - Defense);
        CurrentHp -= dmg;
        if (CurrentHp <= 0f)
        {
            CurrentHp = 0f;
            IsDead = true;
            GameLog.Post("You were defeated... respawning");
            StartCoroutine(RespawnRoutine());
        }
    }

    // 装備変更で最大HPが減った場合に備えて
    public void ClampHp()
    {
        CurrentHp = Mathf.Min(CurrentHp, MaxHp);
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = spawnPosition;
        }
        transform.position = spawnPosition;

        CurrentHp = MaxHp;
        IsDead = false;
    }
}
