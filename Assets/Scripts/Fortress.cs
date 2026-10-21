using Unity.Netcode;
using UnityEngine;

// 中心の砦。HPが0になるとゲームオーバー(HPはホストが管理し、全員に同期)
// 砦のオブジェクトに NetworkObject も付けること
public class Fortress : NetworkBehaviour
{
    public static Fortress Instance { get; private set; }

    [SerializeField] private float maxHp = 1000f;
    [Tooltip("砦の半径。モンスターはこの外周で攻撃を始める")]
    [SerializeField] private float radius = 2.5f;

    private NetworkVariable<float> hp = new NetworkVariable<float>(0f);

    public float MaxHp => maxHp;
    public float CurrentHp => hp.Value;
    public float Radius => radius;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) hp.Value = maxHp;
    }

    // モンスターから呼ばれる(ホストのみ有効)
    public void TakeDamage(float amount)
    {
        if (!IsServer || hp.Value <= 0f) return;

        hp.Value = Mathf.Max(0f, hp.Value - amount);
        if (hp.Value <= 0f && WaveManager.Instance != null)
            WaveManager.Instance.Defeat();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
