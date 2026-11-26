using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// プレイヤーのステータス・レベル・HP管理。
// 数値の変更はホストだけが行い、全員に同期される。
[RequireComponent(typeof(PlayerEquipment))]
public partial class PlayerStats : NetworkBehaviour
{
    // 全プレイヤー(自分も含む) / 自分が操作しているプレイヤー
    public static readonly List<PlayerStats> All = new List<PlayerStats>();
    public static PlayerStats Local { get; private set; }

    [Header("基本ステータス (Lv1)")]
    [SerializeField] private float baseMaxHp = 100f;
    [SerializeField] private float baseAttack = 12f;
    [SerializeField] private float baseMoveSpeed = 6f;
    [Range(0f, 1f), SerializeField] private float baseCriticalChance = 0f;
    [Min(1f), SerializeField] private float baseCriticalMultiplier = 1.5f;
    [Min(0.1f), SerializeField] private float baseAttackSpeed = 1f;

    [Header("ジャンプ・ブリンク (高さと距離はm、間隔は秒)")]
    [Min(0f), SerializeField] private float jumpHeight = 1.5f;
    [Min(0f), SerializeField] private float blinkDistance = 4f;
    [Tooltip("この秒数をかけてブリンク距離を移動します。短いほど高速です。")]
    [Min(0.02f), SerializeField] private float blinkDuration = 0.15f;
    [Min(0f), SerializeField] private float blinkCooldown = 1.5f;

    [Header("レベルごとの上昇量")]
    [SerializeField] private float hpPerLevel = 15f;
    [SerializeField] private float attackPerLevel = 2f;
    [SerializeField] private float moveSpeedPerLevel = 0.1f;

    [Header("経験値")]
    [SerializeField] private float expToNextBase = 30f;
    [SerializeField] private float expGrowth = 1.4f;

    [Header("出現位置(左右X方向へ spacing だけ離れて出現)")]
    [SerializeField] private Vector3 spawnOrigin = new Vector3(0f, 1.05f, 6f);
    [SerializeField] private float spawnSpacing = 4f;
    [SerializeField] private float spawnFacingYaw = 0f;

    [Header("やられた時")]
    [SerializeField] private float respawnDelay = 3f;

    private NetworkVariable<float> currentHp = new NetworkVariable<float>(1f);
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);
    private NetworkVariable<int> spawnSlot = new NetworkVariable<int>(-1);
    public int SpawnSlot => spawnSlot.Value;

    private PlayerEquipment equipment;

    public PlayerEquipment Equipment => equipment;
    public int Level => Mathf.Max(1, teamProgression.Value.Level);
    public float Exp => teamProgression.Value.Exp;
    public float ExpToNext => RequiredExperience(Level);
    public float CurrentHp => currentHp.Value;
    public bool IsDead => isDead.Value;

    public float MaxHp => baseMaxHp + hpPerLevel * (Level - 1) + equipment.TotalMaxHp + upgradeBonuses.Value.MaxHp;
    [System.Obsolete("本人の防御力は廃止。DamageReductionRateを使用してください。")]
    public float Defense => 0f;
    public float DamageReductionRate => 1f - equipment.IncomingDamageMultiplier;
    public float MoveSpeed => baseMoveSpeed + moveSpeedPerLevel * (Level - 1) + upgradeBonuses.Value.MoveSpeed;
    public float JumpHeight => Mathf.Max(0f, jumpHeight + upgradeBonuses.Value.JumpHeight);
    public float CriticalChance => Mathf.Clamp01(baseCriticalChance + upgradeBonuses.Value.CriticalChance);
    public float CriticalMultiplier => Mathf.Max(1f, baseCriticalMultiplier + upgradeBonuses.Value.CriticalDamage);
    public float AttackSpeedMultiplier => Mathf.Max(0.1f, baseAttackSpeed + upgradeBonuses.Value.AttackSpeed);
    public float BlinkDistance => Mathf.Max(0f, blinkDistance);
    public float BlinkDuration => Mathf.Max(0.02f, blinkDuration);
    public float BlinkCooldown => Mathf.Max(0f, blinkCooldown);

    public float BaseAttackPower => baseAttack + attackPerLevel * (Level - 1) + upgradeBonuses.Value.Attack;
    public float AttackPower => BaseAttackPower * GuardianAttackMultiplier;
    public float GetAttack(bool leftHand) => AttackPower * equipment.GetWeaponAttackMultiplier(leftHand);

    public float GetAttackDamage(bool leftHand) => RollAttackDamage(leftHand).Amount;

    public AttackDamageResult RollAttackDamage(bool leftHand)
    {
        float damage = GetAttack(leftHand);
        float chance = CriticalChance;
        bool critical = IsServer && (chance >= 1f || chance > 0f && Random.value < chance);
        return new AttackDamageResult { Amount = critical ? damage * CriticalMultiplier : damage, IsCritical = critical };
    }

    public float GetSwingDuration(ItemData weapon) => Mathf.Max(0.1f, weapon.swingDuration / AttackSpeedMultiplier);
    public float GetAttackInterval(float interval, ItemData weapon) => Mathf.Max(0.02f, interval / AttackSpeedMultiplier,
        weapon != null && weapon.IsMeleeAttack ? GetSwingDuration(weapon) : 0f);

    private Vector3 SpawnPosition
    {
        get
        {
            return spawnOrigin + Vector3.right * ((spawnSlot.Value * 2 - 1) * spawnSpacing * 0.5f);
        }
    }

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        waveRewardResults = new NetworkList<WaveRewardResult>(null, NetworkVariableReadPermission.Owner);
    }

    public override void OnNetworkSpawn()
    {
        All.Add(this);
        teamProgression.OnValueChanged += OnTeamProgressionChanged;
        if (IsServer)
        {
            AssignSpawnSlot();
            TeamProgression.For(NetworkManager).Register(this);
            currentHp.Value = MaxHp;
        }

        if (IsOwner)
        {
            Local = this;
            Time.timeScale = IsTeamLevelUpPaused ? 0f : 1f;
            StartCoroutine(PlaceAtSpawn());
            if (GetComponent<LevelUpChoiceUI>() == null) gameObject.AddComponent<LevelUpChoiceUI>();
            if (GetComponent<WaveRewardChoiceUI>() == null) gameObject.AddComponent<WaveRewardChoiceUI>();
            if (IsHost && CreatorMode.SupportedBuild)
            {
                var creator = GetComponent<CreatorMode>();
                if (creator == null) creator = gameObject.AddComponent<CreatorMode>();
                creator.Initialize(this);
                if (GetComponent<CreatorModeUI>() == null) gameObject.AddComponent<CreatorModeUI>();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        TeamProgression.Unregister(this);
        teamProgression.OnValueChanged -= OnTeamProgressionChanged;
        if (IsOwner) Time.timeScale = 1f;
        All.Remove(this);
        if (Local == this) Local = null;
    }

    private IEnumerator PlaceAtSpawn()
    {
        yield return null; // 1フレーム待ってから配置(同期の初期化後)
        while (IsSpawned && IsOwner && spawnSlot.Value < 0) yield return null;
        if (IsSpawned && IsOwner && spawnSlot.Value >= 0) TeleportLocal(SpawnPosition);
    }

    private void AssignSpawnSlot()
    {
        for (int candidate = 0; candidate < 2; candidate++)
        {
            bool used = false;
            foreach (var player in All)
                if (player != this && player != null && player.IsSpawned && player.IsServer
                    && player.NetworkManager == NetworkManager && player.SpawnSlot == candidate) { used = true; break; }
            if (!used) { spawnSlot.Value = candidate; return; }
        }
        // 接続上限をここでは変更しない。未割当のまま、誤った場所への仮配置を避ける。
        Debug.LogError("PlayerStats: 空いている出現枠がありません。");
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
        GetComponent<PlayerController3D>()?.ResetJumpConsumption();
        transform.rotation = Quaternion.Euler(0f, spawnFacingYaw, 0f);
        if (rb != null) rb.rotation = transform.rotation;
        if (Camera.main != null) Camera.main.GetComponent<CameraFollow>()?.ResetSpawnFacing(spawnFacingYaw);

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

    // Monsterが受理した命中だけを、消滅しない攻撃者PlayerObjectから本人へ送る。
    public void NotifyDamageConfirmed(AttackDamageResult damage, Vector3 position)
    {
        if (!IsServer || !IsSpawned || !(damage.Amount > 0f) || float.IsInfinity(damage.Amount)
            || !DamageFeedback.IsFinite(position)) return;
        DamageConfirmedOwnerRpc(damage.Amount, position, damage.IsCritical);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void DamageConfirmedOwnerRpc(float amount, Vector3 position, bool critical)
    {
        if (!IsOwner) return;
        DamageFeedback.Publish(new DamageFeedbackData { Amount = amount, WorldPosition = position, IsCritical = critical });
    }

    // ---- 以下はホストだけが実行 ----

    public void AddExp(float amount)
    {
        if (!IsServer || !IsSpawned) return;
        TeamProgression.For(NetworkManager).AddExperience(amount);
    }

    public bool TrySetCreatorLevel(int requestedLevel)
    {
        if (!IsServer || !CreatorMode.CanEdit(this) || IsDead || IsGamePaused || requestedLevel < 1) return false;
        // 現行の経験値計算がオーバーフローする値は受け付けない。
        float nextExp = RequiredExperience(requestedLevel);
        if (!(nextExp > 0f) || float.IsInfinity(nextExp)) return false;
        TeamProgression.For(NetworkManager).SetLevel(requestedLevel);
        return true;
    }

    public void TakeDamage(float rawDamage)
        => TakeDamage(rawDamage, null);

    public void TakeDamage(float rawDamage, Monster source)
    {
        if (!IsServer || isDead.Value || !(rawDamage > 0f) || float.IsInfinity(rawDamage)) return;

        bool canReflect = source != null && source.IsServer && source.IsAlive && source.NetworkManager == NetworkManager;
        float reflected = canReflect ? rawDamage * WeaponSkillSettings.Current.ReflectRate * equipment.EquippedReflectCount : 0f;

        float dmg = rawDamage * equipment.IncomingDamageMultiplier;
        currentHp.Value = Mathf.Max(0f, currentHp.Value - dmg);

        if (currentHp.Value <= 0f)
        {
            isDead.Value = true;
            NotifyOwnerRpc("You were defeated... respawning");
            StartCoroutine(RespawnRoutine());
        }
        // 自分が致死でも反射する。別の二次ダメージ文脈なので毒等を再帰させない。
        if (canReflect && source != null && source.IsAlive)
            source.TryTakeDamage(DamageContext.Secondary(reflected, this, DamageKind.Reflection));
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

        while (IsSpawned && spawnSlot.Value < 0) yield return null;
        if (!IsSpawned) yield break;

        TeleportOwnerRpc(SpawnPosition);
        currentHp.Value = MaxHp;
        isDead.Value = false;
    }
}
