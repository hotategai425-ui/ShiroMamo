using Unity.Netcode;
using UnityEngine;

// 武器・防具のドロップ品。何のアイテムかは ItemDatabase の番号で同期する
public class ItemPickup : Pickup
{
    private NetworkVariable<int> itemIndex = new NetworkVariable<int>(-1);
    private int pendingIndex = -1;
    private GameObject pickupVisual;

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
        ClearVisual();
        base.OnNetworkDespawn();
    }

    private void OnIndexChanged(int previous, int current)
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        ClearVisual();

        var fallbackRenderer = GetComponent<Renderer>();
        ItemData item = ItemDatabase.Get(itemIndex.Value);
        bool hasCustomVisual = item != null && item.pickupVisualPrefab != null;
        if (fallbackRenderer != null) fallbackRenderer.enabled = !hasCustomVisual;

        if (item == null) return;
        if (hasCustomVisual)
        {
            if (IsServer) transform.localScale = Vector3.one;
            pickupVisual = Instantiate(item.pickupVisualPrefab, transform, false);
            return;
        }

        if (fallbackRenderer != null) fallbackRenderer.material.color = item.RarityColor;

        // 大きさはホストが決めて NetworkTransform で同期する
        if (IsServer)
        {
            bool weapon = item.slot == ItemSlot.Weapon;
            transform.localScale = weapon ? new Vector3(0.25f, 0.9f, 0.25f) : new Vector3(0.6f, 0.2f, 0.6f);
        }
    }

    private void ClearVisual()
    {
        if (pickupVisual == null) return;
        pickupVisual.SetActive(false);
        Destroy(pickupVisual);
        pickupVisual = null;
    }

    protected override void OnCollectedServer(PlayerStats player)
    {
        player.Equipment.ReceiveItem(ItemDatabase.Get(itemIndex.Value));
    }
}
