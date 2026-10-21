using Unity.Netcode;
using UnityEngine;

// 武器・防具のドロップ品。何のアイテムかは ItemDatabase の番号で同期する
public class ItemPickup : Pickup
{
    private NetworkVariable<int> itemIndex = new NetworkVariable<int>(-1);
    private int pendingIndex = -1;

    // ホストが Spawn する前に呼ぶ
    public void Init(int index)
    {
        pendingIndex = index;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer) itemIndex.Value = pendingIndex;

        ApplyVisual();
        itemIndex.OnValueChanged += OnIndexChanged;
    }

    public override void OnNetworkDespawn()
    {
        itemIndex.OnValueChanged -= OnIndexChanged;
        base.OnNetworkDespawn();
    }

    private void OnIndexChanged(int previous, int current)
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        ItemData item = ItemDatabase.Get(itemIndex.Value);
        if (item == null) return;

        var r = GetComponentInChildren<Renderer>();
        if (r != null) r.material.color = item.RarityColor;

        // 大きさはホストが決めて NetworkTransform で同期する
        if (IsServer)
        {
            bool weapon = item.slot == ItemSlot.Weapon;
            transform.localScale = weapon ? new Vector3(0.25f, 0.9f, 0.25f) : new Vector3(0.6f, 0.2f, 0.6f);
        }
    }

    protected override void OnCollectedServer(PlayerStats player)
    {
        player.Equipment.ReceiveItem(ItemDatabase.Get(itemIndex.Value));
    }
}
