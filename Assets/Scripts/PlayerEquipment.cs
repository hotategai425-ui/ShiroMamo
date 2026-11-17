using Unity.Netcode;
using UnityEngine;

// 武器は拾った順に保管する。左右の装備枠は持ち物の番号を参照する。
public class PlayerEquipment : NetworkBehaviour
{
    private NetworkList<ItemInstance> weapons;
    private NetworkVariable<int> rightWeaponIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<int> leftWeaponIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<int> armorIndex = new NetworkVariable<int>(-1);

    private GameObject leftVisual;
    private GameObject rightVisual;
    private ItemData displayedLeft;
    private ItemData displayedRight;
    private Transform visualParent;

    public bool IsInventoryOpen { get; private set; }
    public int WeaponCount => weapons == null ? 0 : weapons.Count;
    public ItemData RightWeapon => GetInventoryWeapon(rightWeaponIndex.Value);
    public ItemData LeftWeapon => GetInventoryWeapon(leftWeaponIndex.Value);
    public ItemData Armor => ItemDatabase.Get(armorIndex.Value);

    public ItemData GetWeapon(bool leftHand) => leftHand ? LeftWeapon : RightWeapon;
    public WeaponBlade GetWeaponBlade(bool leftHand)
    {
        GameObject visual = leftHand ? leftVisual : rightVisual;
        return visual != null ? visual.GetComponent<WeaponBlade>() : null;
    }
    public ItemInstance GetWeaponInstance(bool leftHand) => GetInventoryInstance(GetEquippedWeaponIndex(leftHand));
    public float GetWeaponAttackBonus(bool leftHand) => GetWeaponInstance(leftHand).AttackBonus;
    public float TotalDefense => GetWeaponInstance(true).DefenseBonus + GetWeaponInstance(false).DefenseBonus + (Armor?.defenseBonus ?? 0f);
    public float TotalMaxHp => GetWeaponInstance(true).MaxHpBonus + GetWeaponInstance(false).MaxHpBonus + (Armor?.maxHpBonus ?? 0f);

    private void Awake()
    {
        weapons = new NetworkList<ItemInstance>();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        if (GameInput.InventoryPressed) IsInventoryOpen = !IsInventoryOpen;
        if (GameInput.CancelPressed || GetComponent<PlayerStats>().IsDead) CloseInventory();
    }

    public void CloseInventory() => IsInventoryOpen = false;

    public int GetEquippedWeaponIndex(bool leftHand) => leftHand ? leftWeaponIndex.Value : rightWeaponIndex.Value;

    public ItemData GetInventoryWeapon(int index)
    {
        return GetInventoryInstance(index).Definition;
    }

    public ItemInstance GetInventoryInstance(int index)
    {
        if (index < 0 || index >= WeaponCount) return default;
        return weapons[index];
    }

    public void EquipWeapon(int inventoryIndex, bool leftHand)
    {
        if (!IsSpawned || !IsOwner) return;
        if (inventoryIndex == -1) EquipWeaponRpc(0, leftHand);
        else EquipWeapon(GetInventoryInstance(inventoryIndex).InstanceId, leftHand);
    }

    public void EquipWeapon(ulong instanceId, bool leftHand)
    {
        if (!IsSpawned || !IsOwner || instanceId == 0) return;
        EquipWeaponRpc(instanceId, leftHand);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void EquipWeaponRpc(ulong instanceId, bool leftHand)
    {
        int inventoryIndex = instanceId == 0 ? -1 : FindWeapon(instanceId);
        if (instanceId != 0 && inventoryIndex < 0) return;

        NetworkVariable<int> target = leftHand ? leftWeaponIndex : rightWeaponIndex;
        NetworkVariable<int> other = leftHand ? rightWeaponIndex : leftWeaponIndex;
        if (target.Value == inventoryIndex) return;
        // 反対の手に持っている個体を選んだ場合は、左右を入れ替える。
        if (inventoryIndex >= 0 && other.Value == inventoryIndex) other.Value = target.Value;
        target.Value = inventoryIndex;
        GetComponent<PlayerStats>().ClampHp();
    }

    private int FindWeapon(ulong instanceId)
    {
        for (int i = 0; i < WeaponCount; i++)
            if (weapons[i].InstanceId == instanceId) return i;
        return -1;
    }

    public void DropWeapon(ulong instanceId)
    {
        if (!IsSpawned || !IsOwner || instanceId == 0) return;
        DropWeaponRpc(instanceId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void DropWeaponRpc(ulong instanceId)
    {
        var stats = GetComponent<PlayerStats>();
        int index = FindWeapon(instanceId);
        if (index < 0 || stats.IsDead || DropManager.Instance == null) return;
        if (WaveManager.Instance != null && WaveManager.Instance.IsFinished) return;
        // 足元へ置き、壁の向こうへの生成を避ける。
        Vector3 position = transform.position - Vector3.up * 0.5f;
        if (!DropManager.Instance.TryDropItem(position, weapons[index], OwnerClientId)) return;

        GetComponent<PlayerAttack>()?.CancelEquipmentAttacks();
        rightWeaponIndex.Value = IndexAfterRemoval(rightWeaponIndex.Value, index);
        leftWeaponIndex.Value = IndexAfterRemoval(leftWeaponIndex.Value, index);
        weapons.RemoveAt(index);
        stats.ClampHp();
    }

    private static int IndexAfterRemoval(int equipped, int removed)
    {
        if (equipped == removed) return -1;
        return equipped > removed ? equipped - 1 : equipped;
    }

    // ホストだけが呼ぶ(ItemPickup から)
    public void ReceiveItem(ItemData item)
    {
        if (!IsServer || item == null || ItemDatabase.Instance == null) return;
        ReceiveItem(ItemDatabase.Instance.CreateItem(item));
    }

    public void ReceiveItem(ItemInstance instance)
    {
        if (!IsServer || !instance.IsValid) return;
        ItemData item = instance.Definition;
        if (item == null) return;

        var stats = GetComponent<PlayerStats>();
        int index = instance.DefinitionIndex;

        if (item.slot == ItemSlot.Weapon)
        {
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i].InstanceId == instance.InstanceId) return;
            weapons.Add(instance);
            if (RightWeapon == null || LeftWeapon == null)
            {
                bool equipLeft = RightWeapon != null;
                NetworkVariable<int> emptyHand = equipLeft ? leftWeaponIndex : rightWeaponIndex;
                emptyHand.Value = weapons.Count - 1;
                stats.ClampHp();
                stats.Notify($"Equipped ({(equipLeft ? "Left" : "Right")}): {instance.Rarity} {item.displayName}");
                return;
            }
            stats.Notify($"Collected: {instance.Rarity} {item.displayName} (I: Inventory)");
            return;
        }

        ItemData current = Armor;

        if (index >= 0 && (current == null || item.Score > current.Score))
        {
            armorIndex.Value = index;
            stats.ClampHp();
            stats.Notify($"Equipped (Armor): {item.displayName}");
        }
        else
        {
            stats.AddExp(item.salvageExp);
            stats.Notify($"Salvaged {item.displayName} (+{item.salvageExp:0} EXP)");
        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;

        Transform parent = transform;
        if (IsOwner)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            parent = camera.transform;
        }

        ItemData left = LeftWeapon;
        ItemData right = RightWeapon;
        if (visualParent != parent || displayedLeft != left)
        {
            RemoveVisual(ref leftVisual);
            leftVisual = CreateWeaponVisual(left, parent, -1f);
            displayedLeft = left;
        }
        if (visualParent != parent || displayedRight != right)
        {
            RemoveVisual(ref rightVisual);
            rightVisual = CreateWeaponVisual(right, parent, 1f);
            displayedRight = right;
        }
        visualParent = parent;
    }

    public override void OnNetworkDespawn()
    {
        CloseInventory();
        RemoveVisual(ref leftVisual);
        RemoveVisual(ref rightVisual);
        visualParent = null;
    }

    private GameObject CreateWeaponVisual(ItemData weapon, Transform parent, float side)
    {
        if (weapon == null || weapon.pickupVisualPrefab == null) return null;

        var anchor = new GameObject(side < 0f ? "Left Weapon" : "Right Weapon");
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = IsOwner
            ? new Vector3(side * 0.38f, -0.56f, 0.85f)
            : new Vector3(side * 0.65f, -0.45f, 0.32f);
        anchor.transform.localRotation = Quaternion.Euler(20f, 0f, -side * 12f);
        bool melee = weapon.attackType == WeaponAttackType.Melee;
        anchor.transform.localScale = Vector3.one * (melee ? Mathf.Max(0.1f, weapon.meleeScale) : (IsOwner ? 0.55f : 1f));
        Instantiate(weapon.pickupVisualPrefab, anchor.transform, false);
        if (melee)
            anchor.AddComponent<WeaponBlade>().Initialize(this, side < 0f, weapon);
        return anchor;
    }

    private static void RemoveVisual(ref GameObject visual)
    {
        if (visual != null)
        {
            visual.SetActive(false);
            Destroy(visual);
        }
        visual = null;
    }
}
