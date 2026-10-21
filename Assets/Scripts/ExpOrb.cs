using UnityEngine;

// 経験値オーブ
public class ExpOrb : Pickup
{
    [SerializeField] private Color color = new Color(0.3f, 0.9f, 1f);

    private float amount;

    // ホストが Spawn する前に呼ぶ
    public void Init(float value)
    {
        amount = value;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        var r = GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = color;
    }

    protected override void OnCollectedServer(PlayerStats player)
    {
        player.AddExp(amount);
    }
}
