using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 左右のクリックを受け取り、ホストが攻撃判定を行う。
[RequireComponent(typeof(PlayerStats))]
public class PlayerAttack : NetworkBehaviour
{
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackInterval = 0.5f;

    private PlayerStats stats;
    private double nextLeftAttack;
    private double nextRightAttack;
    private double localNextLeftAttack;
    private double localNextRightAttack;
    private int nextAttackId;
    private int lastAcceptedLeftAttackId;
    private int lastAcceptedRightAttackId;
    private readonly SwingState leftSwing = new SwingState();
    private readonly SwingState rightSwing = new SwingState();

    private sealed class SwingState
    {
        public int Id;
        public int EquippedIndex;
        public ItemData Weapon;
        public double ExpiresAt;
        public float Progress = -1f;
        public Vector3 Position;
        public Quaternion Rotation;
        public AttackDamageResult Damage;
        public WeaponSkillLoadout Skills;
        public bool HasPose;
        public bool Consumed;
        public readonly HashSet<ulong> HitEnemies = new HashSet<ulong>();
        public readonly List<Monster> Contacts = new List<Monster>();
    }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || !CanAttack() || stats.Equipment.IsControlBlocked) return;

        bool leftPressed = GameInput.HandPressed(leftHand: true);
        bool rightPressed = GameInput.HandPressed(leftHand: false);
        if (!leftPressed && !rightPressed) return;

        Camera camera = Camera.main;
        Vector3 aim = camera != null ? camera.transform.forward : transform.forward;
        if (leftPressed) RequestAttack(true, aim);
        if (rightPressed) RequestAttack(false, aim);
    }

    // PC用の入力と振り動作をつなぐ。
    public bool RequestAttack(bool leftHand, Vector3 aim)
    {
        if (!IsSpawned || !IsOwner) return false;
        ItemData weapon = stats.Equipment.GetWeapon(leftHand);
        bool melee = weapon != null && weapon.IsMeleeAttack;
        WeaponBlade blade = stats.Equipment.GetWeaponBlade(leftHand);
        if (melee && blade == null) return false;
        int id = BeginAttack(leftHand, aim);
        if (id == 0) return false;
        if (melee) blade.StartSwing(id);
        return true;
    }

    // 追跡入力はこの戻り値を使ってSubmitMeleePoseへ姿勢を送れる。
    public int BeginAttack(bool leftHand, Vector3 aim)
    {
        if (!IsSpawned || !IsOwner || !CanAttack() || stats.Equipment.IsControlBlocked) return 0;
        if (!(aim.sqrMagnitude > 0f) || float.IsInfinity(aim.sqrMagnitude)) return 0;
        ItemData weapon = stats.Equipment.GetWeapon(leftHand);
        bool melee = weapon != null && weapon.IsMeleeAttack;

        double now = Time.timeAsDouble;
        ref double next = ref (leftHand ? ref localNextLeftAttack : ref localNextRightAttack);
        if (now < next) return 0;
        next = now + stats.GetAttackInterval(attackInterval, weapon);
        int id = ++nextAttackId;
        AttackRpc(leftHand, aim, id);
        return id;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AttackRpc(bool leftHand, Vector3 aim, int attackId)
    {
        if (!CanAttack()) return;
        ref int lastAccepted = ref (leftHand ? ref lastAcceptedLeftAttackId : ref lastAcceptedRightAttackId);
        if (attackId <= 0 || attackId <= lastAccepted) return;
        // 左右とも、空いている手は同じ素手判定を使う。間隔と受理IDは手ごとに独立。

        double now = Time.timeAsDouble;
        ref double nextAttack = ref (leftHand ? ref nextLeftAttack : ref nextRightAttack);
        if (now < nextAttack) return;

        ItemData weapon = stats.Equipment.GetWeapon(leftHand);
        bool projectile = weapon != null && weapon.attackType == WeaponAttackType.Projectile;
        if (projectile && weapon.projectilePrefab == null) return;

        if (weapon == null) aim.y = 0f;
        if (!(aim.sqrMagnitude > 0f) || float.IsInfinity(aim.sqrMagnitude)) return;
        aim.Normalize();

        bool melee = weapon != null && !projectile;
        lastAccepted = attackId;
        nextAttack = now + stats.GetAttackInterval(attackInterval, weapon);
        if (projectile)
        {
            // 球を壁の向こうに出さないよう、プレイヤーの横から発射する。
            Vector3 origin = transform.position + Vector3.up * 0.6f
                + transform.right * (leftHand ? -0.35f : 0.35f);
            MagicProjectile shot = Instantiate(weapon.projectilePrefab, origin, Quaternion.LookRotation(aim));
            shot.Init(aim, weapon.projectileSpeed, weapon.projectileLifetime, stats.RollAttackDamage(leftHand), stats,
                stats.Equipment.GetWeaponSkills(leftHand));
            shot.GetComponent<NetworkObject>().Spawn(true);
            return;
        }

        if (melee)
        {
            SwingState swing = leftHand ? leftSwing : rightSwing;
            if (attackId <= swing.Id) return;
            swing.Id = attackId;
            swing.EquippedIndex = stats.Equipment.GetEquippedWeaponIndex(leftHand);
            swing.Weapon = weapon;
            swing.ExpiresAt = now + stats.GetSwingDuration(weapon) + 0.35f;
            swing.Progress = -1f;
            swing.HasPose = false;
            swing.Consumed = false;
            swing.HitEnemies.Clear();
            swing.Damage = stats.RollAttackDamage(leftHand);
            swing.Skills = stats.Equipment.GetWeaponSkills(leftHand);
            return;
        }

        // 素手は武器モデルを持たないため、正面の距離判定を使う。
        Monster target = FindTarget(aim);
        if (target != null)
            target.TakeDamage(stats.RollAttackDamage(leftHand), stats, target.transform.position - transform.position);
    }

    // 将来の追跡入力は、武器の実際のワールド姿勢をこの入口へ渡す。
    public void SubmitMeleePose(bool leftHand, int attackId, Vector3 position, Quaternion rotation, float progress)
    {
        if (!IsSpawned || !IsOwner || !CanAttack() || stats.Equipment.IsControlBlocked) return;
        MeleePoseRpc(leftHand, attackId, position, rotation, progress);
    }

    public void CancelMeleeAttack(bool leftHand, int attackId)
    {
        if (IsSpawned && IsOwner) CancelMeleeRpc(leftHand, attackId);
    }

    // 持ち物削除で番号が詰まった後に、削除前の振りを受理しない。
    public void CancelEquipmentAttacks()
    {
        if (!IsServer) return;
        leftSwing.Weapon = null;
        rightSwing.Weapon = null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void CancelMeleeRpc(bool leftHand, int attackId)
    {
        SwingState swing = leftHand ? leftSwing : rightSwing;
        if (swing.Id != attackId || swing.Weapon == null) return;
        swing.ExpiresAt = 0d;
        swing.Consumed = true;
        ShowMeleePoseRpc(leftHand, attackId, swing.EquippedIndex, swing.Position, swing.Rotation, 1f);
        swing.Weapon = null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void MeleePoseRpc(bool leftHand, int attackId, Vector3 position, Quaternion rotation, float progress)
    {
        if (!CanAttack()) return;
        SwingState swing = leftHand ? leftSwing : rightSwing;
        if (swing.Weapon == null || swing.Id != attackId || Time.timeAsDouble > swing.ExpiresAt) return;
        if (stats.Equipment.GetEquippedWeaponIndex(leftHand) != swing.EquippedIndex
            || stats.Equipment.GetWeapon(leftHand) != swing.Weapon) return;
        if (!(progress >= 0f && progress <= 1f) || progress < swing.Progress) return;

        float rotationLength = Quaternion.Dot(rotation, rotation);
        if (!(rotationLength > 0.5f && rotationLength < 1.5f)) return;
        rotation = rotation.normalized;
        float scale = Mathf.Max(0.1f, swing.Weapon.meleeScale);
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        Vector3 bladeBase = position + rotation * (swing.Weapon.bladeBase * scale);
        Vector3 bladeTip = position + rotation * (swing.Weapon.bladeTip * scale);
        float maxReachSquared = attackRange * attackRange;
        if (!((position - origin).sqrMagnitude <= maxReachSquared)
            || !((bladeBase - origin).sqrMagnitude <= maxReachSquared)
            || !((bladeTip - origin).sqrMagnitude <= maxReachSquared)) return;

        if (!swing.HasPose)
        {
            swing.Position = position;
            swing.Rotation = rotation;
        }
        if (!swing.Consumed && progress >= 0.2f && progress <= 0.7f)
        {
            swing.Contacts.Clear();
            swing.Consumed = MeleeHitResolver.SweepAll(swing.Weapon, swing.Position, swing.Rotation,
                position, rotation, stats, swing.Contacts);
            // 接触収集とダメージ適用を分離し、致死Despawnで物理検索や反復を壊さない。
            foreach (Monster target in swing.Contacts)
            {
                if (target == null || !target.IsSpawned || !target.IsServer
                    || target.NetworkManager != NetworkManager
                    || !swing.HitEnemies.Add(target.NetworkObjectId)) continue;
                target.TryTakeDamage(DamageContext.Direct(swing.Damage, stats, swing.Skills), target.transform.position - transform.position);
            }
        }
        swing.Position = position;
        swing.Rotation = rotation;
        swing.Progress = progress;
        swing.HasPose = true;
        ShowMeleePoseRpc(leftHand, attackId, swing.EquippedIndex, position, rotation, progress);
    }

    [Rpc(SendTo.Everyone)]
    private void ShowMeleePoseRpc(bool leftHand, int attackId, int equippedIndex, Vector3 position, Quaternion rotation, float progress)
    {
        if (IsOwner) return;
        if (stats.Equipment.GetEquippedWeaponIndex(leftHand) != equippedIndex) return;
        var blade = stats.Equipment.GetWeaponBlade(leftHand);
        if (blade != null) blade.ShowRemotePose(attackId, position, rotation, progress);
    }

    private bool CanAttack()
    {
        var wave = WaveManager.Instance;
        return !stats.IsDead && !stats.IsTeamLevelUpPaused && wave != null && wave.Phase == GamePhase.Combat;
    }

    private Monster FindTarget(Vector3 aim)
    {
        const float CosHalfAngle = 0.707107f; // 正面の左右45度
        Monster best = null;
        float bestSqr = attackRange * attackRange;
        Vector3 pos = transform.position;

        foreach (var m in Monster.All)
        {
            if (m == null || !m.IsSpawned || !m.IsServer || m.NetworkManager != NetworkManager) continue;
            Vector3 d = m.transform.position - pos;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr <= bestSqr && Mathf.Abs(m.transform.position.y - pos.y) <= 1.5f
                && Vector3.Dot(aim, d) >= CosHalfAngle * Mathf.Sqrt(sqr)
                && !MeleeHitResolver.IsOccluded(stats, m.transform.position, m))
            {
                bestSqr = sqr;
                best = m;
            }
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
