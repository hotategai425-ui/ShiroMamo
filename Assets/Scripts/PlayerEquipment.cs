using Unity.Netcode;
using UnityEngine;

public struct HandEquipmentState : INetworkSerializable, System.IEquatable<HandEquipmentState>
{
    public ulong LeftInstanceId, RightInstanceId;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    { serializer.SerializeValue(ref LeftInstanceId); serializer.SerializeValue(ref RightInstanceId); }
    public bool Equals(HandEquipmentState other) => LeftInstanceId == other.LeftInstanceId && RightInstanceId == other.RightInstanceId;
}

// Both occupied hands are committed and synchronized as one instance-ID pair.
public class PlayerEquipment : NetworkBehaviour
{
    private NetworkList<ItemInstance> weapons;
    private NetworkVariable<HandEquipmentState> hands = new();
    private NetworkVariable<int> armorIndex = new NetworkVariable<int>(-1);

    private GameObject leftVisual;
    private GameObject rightVisual;
    private ItemData displayedLeft;
    private ItemData displayedRight;
    private Transform visualParent;

    public bool IsInventoryOpen { get; private set; }
    public bool IsControlBlocked => IsInventoryOpen || CreatorMode.IsOpenFor(this)
        || GetComponent<PlayerStats>().IsTeamLevelUpPaused;
    public int WeaponCount => weapons == null ? 0 : weapons.Count;
    public ItemData RightWeapon => GetInventoryWeapon(GetEquippedWeaponIndex(false));
    public ItemData LeftWeapon => GetInventoryWeapon(GetEquippedWeaponIndex(true));
    public bool UsesTwoHandedWeapon => hands.Value.LeftInstanceId != 0 && hands.Value.LeftInstanceId == hands.Value.RightInstanceId;
    public ItemInstance TwoHandedWeaponInstance => UsesTwoHandedWeapon ? GetWeaponInstance(true) : default;
    public bool IsAttackHandEnabled(bool leftHand) => !UsesTwoHandedWeapon || leftHand;
    public ItemData Armor => ItemDatabase.Get(armorIndex.Value);

    public ItemData GetWeapon(bool leftHand) => leftHand ? LeftWeapon : RightWeapon;
    public WeaponBlade GetWeaponBlade(bool leftHand)
    {
        GameObject visual = leftHand ? leftVisual : rightVisual;
        return visual != null ? visual.GetComponent<WeaponBlade>() : null;
    }
    public ItemInstance GetWeaponInstance(bool leftHand) => GetInventoryInstance(GetEquippedWeaponIndex(leftHand));
    [System.Obsolete("加算攻撃は廃止。GetWeaponAttackMultiplierを使用してください。")]
    public float GetWeaponAttackBonus(bool leftHand) => 0f;
    [System.Obsolete("防御力は廃止。IncomingDamageMultiplierを使用してください。")]
    public float TotalDefense => 0f;
    public float GetWeaponAttackMultiplier(bool leftHand)
    {
        if (!IsAttackHandEnabled(leftHand)) return 0f;
        var instance = GetWeaponInstance(leftHand);
        if (!instance.IsValid) return 1f;
        return float.IsNaN(instance.AttackMultiplier) || float.IsInfinity(instance.AttackMultiplier) ? 0f : Mathf.Max(0f, instance.AttackMultiplier);
    }
    public float IncomingDamageMultiplier => (1f - GetShieldReduction(true)) * (1f - GetShieldReduction(false));
    private float GetShieldReduction(bool leftHand)
    {
        if (!IsAttackHandEnabled(leftHand)) return 0f;
        var instance = GetWeaponInstance(leftHand);
        return instance.Definition != null && instance.Definition.IsShield ? ItemInstance.SanitizeRate(instance.DamageReductionRate) : 0f;
    }
    public float TotalMaxHp => GetWeaponInstance(true).MaxHpBonus + (UsesTwoHandedWeapon ? 0f : GetWeaponInstance(false).MaxHpBonus) + (Armor?.maxHpBonus ?? 0f);

    private void Awake()
    {
        weapons = new NetworkList<ItemInstance>();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        if (GetComponent<PlayerStats>().IsTeamLevelUpPaused) { CloseInventory(); return; }
        if (GameInput.InventoryPressed && !CreatorMode.IsOpenFor(this)) IsInventoryOpen = !IsInventoryOpen;
        if (GameInput.CancelPressed || GetComponent<PlayerStats>().IsDead) CloseInventory();
    }

    public void CloseInventory() => IsInventoryOpen = false;

    public int GetEquippedWeaponIndex(bool leftHand) => FindWeapon(leftHand ? hands.Value.LeftInstanceId : hands.Value.RightInstanceId);

    public ItemData GetInventoryWeapon(int index)
    {
        return GetInventoryInstance(index).Definition;
    }

    public ItemInstance GetInventoryInstance(int index)
    {
        if (index < 0 || index >= WeaponCount) return default;
        return weapons[index];
    }

    public WeaponSkillLoadout GetWeaponSkills(bool leftHand) => IsAttackHandEnabled(leftHand) ? GetWeaponInstance(leftHand).Skills : default;
    public int EquippedReflectCount => GetEquippedReflectCount(true) + GetEquippedReflectCount(false);

    private int GetEquippedReflectCount(bool leftHand)
    {
        if (!IsAttackHandEnabled(leftHand)) return 0;
        var item = GetWeaponInstance(leftHand);
        return item.Definition != null && item.Definition.IsShield ? item.Skills.Count(WeaponSkillId.Reflect) : 0;
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
        if (GetComponent<PlayerStats>().IsTeamLevelUpPaused) return;
        int inventoryIndex = instanceId == 0 ? -1 : FindWeapon(instanceId);
        if (instanceId != 0 && inventoryIndex < 0) return;

        var state = hands.Value;
        if (instanceId != 0 && GetInventoryWeapon(inventoryIndex).UsesTwoHands)
            state = new HandEquipmentState { LeftInstanceId = instanceId, RightInstanceId = instanceId };
        else
        {
            if (UsesTwoHandedWeapon) state = default;
            ulong previous = leftHand ? state.LeftInstanceId : state.RightInstanceId;
            if (instanceId != 0 && (leftHand ? state.RightInstanceId : state.LeftInstanceId) == instanceId)
            {
                if (leftHand) state.RightInstanceId = previous; else state.LeftInstanceId = previous;
            }
            if (leftHand) state.LeftInstanceId = instanceId; else state.RightInstanceId = instanceId;
        }
        if (state.Equals(hands.Value)) return;
        GetComponent<PlayerAttack>()?.CancelEquipmentAttacks();
        hands.Value = state;
        GetComponent<PlayerStats>().ClampHp();
    }

    private int FindWeapon(ulong instanceId)
    {
        if (instanceId == 0) return -1;
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
        if (index < 0 || stats.IsDead || stats.IsTeamLevelUpPaused || DropManager.Instance == null) return;
        if (WaveManager.Instance != null && WaveManager.Instance.IsFinished) return;
        // 足元へ置き、壁の向こうへの生成を避ける。
        Vector3 position = transform.position - Vector3.up * 0.5f;
        if (!DropManager.Instance.TryDropItem(position, weapons[index], OwnerClientId)) return;

        GetComponent<PlayerAttack>()?.CancelEquipmentAttacks();
        var state = hands.Value;
        if (state.LeftInstanceId == instanceId) state.LeftInstanceId = 0;
        if (state.RightInstanceId == instanceId) state.RightInstanceId = 0;
        hands.Value = state;
        weapons.RemoveAt(index);
        stats.ClampHp();
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
            bool bothEmpty = hands.Value.LeftInstanceId == 0 && hands.Value.RightInstanceId == 0;
            if (item.UsesTwoHands ? bothEmpty : RightWeapon == null || LeftWeapon == null)
            {
                bool equipLeft = RightWeapon != null;
                var state = hands.Value;
                if (item.UsesTwoHands) state = new HandEquipmentState { LeftInstanceId = instance.InstanceId, RightInstanceId = instance.InstanceId };
                else if (equipLeft) state.LeftInstanceId = instance.InstanceId;
                else state.RightInstanceId = instance.InstanceId;
                hands.Value = state;
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
        ItemData right = UsesTwoHandedWeapon ? null : RightWeapon;
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
            ? new Vector3(weapon.UsesTwoHands ? 0f : side * 0.38f, -0.56f, 0.85f) + weapon.heldPositionOffset
            : new Vector3(weapon.UsesTwoHands ? 0f : side * 0.65f, -0.45f, 0.32f);
        anchor.transform.localRotation = Quaternion.Euler(20f, 0f, -side * 12f);
        bool melee = weapon.IsMeleeAttack;
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
