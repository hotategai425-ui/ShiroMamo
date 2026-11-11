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
        public float Damage;
        public bool HasPose;
        public bool Consumed;
    }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || !CanAttack() || stats.Equipment.IsInventoryOpen) return;

        bool leftPressed = GameInput.HandPressed(leftHand: true) && stats.Equipment.LeftWeapon != null;
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
        bool melee = weapon != null && weapon.attackType == WeaponAttackType.Melee;
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
        if (!IsSpawned || !IsOwner || !CanAttack() || stats.Equipment.IsInventoryOpen) return 0;
        if (!(aim.sqrMagnitude > 0f) || float.IsInfinity(aim.sqrMagnitude)) return 0;
        ItemData weapon = stats.Equipment.GetWeapon(leftHand);
        if (leftHand && weapon == null) return 0;
        bool melee = weapon != null && weapon.attackType == WeaponAttackType.Melee;

        double now = NetworkManager.ServerTime.Time;
        ref double next = ref (leftHand ? ref localNextLeftAttack : ref localNextRightAttack);
        if (now < next) return 0;
        next = now + (melee ? Mathf.Max(attackInterval, weapon.swingDuration) : attackInterval);
        int id = ++nextAttackId;
        AttackRpc(leftHand, aim, id);
        return id;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AttackRpc(bool leftHand, Vector3 aim, int attackId)
    {
        if (!CanAttack()) return;
        // まだ武器がない状態から敵を倒せるよう、右手は素手でも攻撃できる。
        if (leftHand && stats.Equipment.LeftWeapon == null) return;

        double now = NetworkManager.ServerTime.Time;
        ref double nextAttack = ref (leftHand ? ref nextLeftAttack : ref nextRightAttack);
        if (now < nextAttack) return;

        ItemData weapon = stats.Equipment.GetWeapon(leftHand);
        bool projectile = weapon != null && weapon.attackType == WeaponAttackType.Projectile;
        if (projectile && weapon.projectilePrefab == null) return;

        if (weapon == null) aim.y = 0f;
        if (!(aim.sqrMagnitude > 0f) || float.IsInfinity(aim.sqrMagnitude)) return;
        aim.Normalize();

        bool melee = weapon != null && !projectile;
        nextAttack = now + (melee ? Mathf.Max(attackInterval, weapon.swingDuration) : attackInterval);
        if (projectile)
        {
            // 球を壁の向こうに出さないよう、プレイヤーの横から発射する。
            Vector3 origin = transform.position + Vector3.up * 0.6f
                + transform.right * (leftHand ? -0.35f : 0.35f);
            MagicProjectile shot = Instantiate(weapon.projectilePrefab, origin, Quaternion.LookRotation(aim));
            shot.Init(aim, weapon.projectileSpeed, weapon.projectileLifetime, stats.GetAttack(leftHand));
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
            swing.ExpiresAt = now + Mathf.Max(0.1f, weapon.swingDuration) + 0.35f;
            swing.Progress = -1f;
            swing.HasPose = false;
            swing.Consumed = false;
            swing.Damage = stats.GetAttack(leftHand);
            return;
        }

        // 素手は武器モデルを持たないため、正面の距離判定を使う。
        Monster target = FindTarget(aim);
        if (target != null)
            target.TakeDamage(stats.GetAttack(leftHand), target.transform.position - transform.position);
    }

    // 将来の追跡入力は、武器の実際のワールド姿勢をこの入口へ渡す。
    public void SubmitMeleePose(bool leftHand, int attackId, Vector3 position, Quaternion rotation, float progress)
    {
        if (!IsSpawned || !IsOwner || !CanAttack() || stats.Equipment.IsInventoryOpen) return;
        MeleePoseRpc(leftHand, attackId, position, rotation, progress);
    }

    public void CancelMeleeAttack(bool leftHand, int attackId)
    {
        if (IsSpawned && IsOwner) CancelMeleeRpc(leftHand, attackId);
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
        if (swing.Weapon == null || swing.Id != attackId || NetworkManager.ServerTime.Time > swing.ExpiresAt) return;
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
            swing.Consumed = MeleeHitResolver.Sweep(swing.Weapon, swing.Position, swing.Rotation,
                position, rotation, stats, out Monster target);
            if (target != null)
                target.TakeDamage(swing.Damage, target.transform.position - transform.position);
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
        return !stats.IsDead && wave != null && wave.Phase == GamePhase.Combat;
    }

    private Monster FindTarget(Vector3 aim)
    {
        const float CosHalfAngle = 0.707107f; // 正面の左右45度
        Monster best = null;
        float bestSqr = attackRange * attackRange;
        Vector3 pos = transform.position;

        foreach (var m in Monster.All)
        {
            if (m == null || !m.IsSpawned) continue;
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
