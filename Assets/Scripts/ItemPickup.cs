using Unity.Netcode;
using UnityEngine;

// ドロップ品の個体情報を同期し、そのまま拾った人へ渡す。
public class ItemPickup : Pickup
{
    private NetworkVariable<ItemInstance> item = new NetworkVariable<ItemInstance>();
    private int pendingIndex = -1;
    private ItemInstance pendingInstance;
    private GameObject pickupVisual;
    private bool excludeDropper;
    private ulong droppedBy;
    private bool droppedByPlayer;

    protected override bool ClearOnWaveStart => !droppedByPlayer;

    // ホストが Spawn する前に呼ぶ
    public void Init(int index)
    {
        pendingIndex = index;
        pendingInstance = default;
    }

    public void Init(ItemInstance instance)
    {
        pendingInstance = instance;
    }

    public void InitDropped(ItemInstance instance, ulong ownerClientId)
    {
        Init(instance);
        droppedBy = ownerClientId;
        excludeDropper = true;
        droppedByPlayer = true;
    }

    protected override bool CanCollect(PlayerStats player)
    {
        if (!excludeDropper || player.OwnerClientId != droppedBy) return true;
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        if (delta.magnitude <= Mathf.Max(magnetRange, collectRadius)) return false;
        excludeDropper = false;
        return true;
    }

    public ItemInstance Instance => item.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            var instance = pendingInstance;
            if (!instance.IsValid && ItemDatabase.Instance != null)
                instance = ItemDatabase.Instance.CreateItem(ItemDatabase.Get(pendingIndex));
            item.Value = instance;
        }

        ApplyVisual();
        item.OnValueChanged += OnItemChanged;
    }

    public override void OnNetworkDespawn()
    {
        item.OnValueChanged -= OnItemChanged;
        ClearVisual();
        base.OnNetworkDespawn();
    }

    private void OnItemChanged(ItemInstance previous, ItemInstance current)
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        ClearVisual();

        var fallbackRenderer = GetComponent<Renderer>();
        ItemData definition = item.Value.Definition;
        bool hasCustomVisual = definition != null && definition.pickupVisualPrefab != null;
        if (fallbackRenderer != null) fallbackRenderer.enabled = !hasCustomVisual;

        if (definition == null) return;
        if (hasCustomVisual)
        {
            if (IsServer) transform.localScale = Vector3.one;
            pickupVisual = Instantiate(definition.pickupVisualPrefab, transform, false);
            return;
        }

        if (fallbackRenderer != null) fallbackRenderer.material.color = ItemData.GetRarityColor(item.Value.Rarity);

        // 大きさはホストが決めて NetworkTransform で同期する
        if (IsServer)
        {
            bool weapon = definition.slot == ItemSlot.Weapon;
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
        player.Equipment.ReceiveItem(item.Value);
    }
}
