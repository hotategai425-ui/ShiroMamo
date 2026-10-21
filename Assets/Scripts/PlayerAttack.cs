using Unity.Netcode;
using UnityEngine;

// 射程内でいちばん近いモンスターを自動攻撃(ホストが全プレイヤー分を処理する)
[RequireComponent(typeof(PlayerStats))]
public class PlayerAttack : NetworkBehaviour
{
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackInterval = 0.5f;

    private PlayerStats stats;
    private float timer;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;
        if (stats.IsDead) return;

        // 戦闘フェーズ以外は攻撃しない(ロビーなど)
        var wm = WaveManager.Instance;
        if (wm == null || wm.Phase != GamePhase.Combat) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        Monster target = FindNearest();
        if (target == null) return;

        timer = attackInterval;
        target.TakeDamage(stats.Attack);
        // TODO: ここで攻撃アニメ・エフェクト・SEを再生(全員に見せるなら Rpc で通知)
    }

    private Monster FindNearest()
    {
        Monster best = null;
        float bestSqr = attackRange * attackRange;
        Vector3 pos = transform.position;

        foreach (var m in Monster.All)
        {
            if (m == null) continue;
            Vector3 d = m.transform.position - pos;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr <= bestSqr)
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
