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
        if (leftPressed) AttackRpc(true, aim);
        if (rightPressed) AttackRpc(false, aim);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AttackRpc(bool leftHand, Vector3 aim)
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

        if (!projectile) aim.y = 0f;
        if (!(aim.sqrMagnitude > 0f) || float.IsInfinity(aim.sqrMagnitude)) return;
        aim.Normalize();

        nextAttack = now + attackInterval;
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

        Monster target = FindTarget(aim);
        if (target != null) target.TakeDamage(stats.GetAttack(leftHand));
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
            if (sqr <= bestSqr && Vector3.Dot(aim, d) >= CosHalfAngle * Mathf.Sqrt(sqr))
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
