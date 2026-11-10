using Unity.Netcode;
using UnityEngine;

// 装備の判定はホストが行い、左右の武器と防具の番号を同期する。
public class PlayerEquipment : NetworkBehaviour
{
    private NetworkVariable<int> rightWeaponIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<int> leftWeaponIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<int> armorIndex = new NetworkVariable<int>(-1);

    private GameObject leftVisual;
    private GameObject rightVisual;
    private ItemData displayedLeft;
    private ItemData displayedRight;
    private Transform visualParent;

    public ItemData RightWeapon => ItemDatabase.Get(rightWeaponIndex.Value);
    public ItemData LeftWeapon => ItemDatabase.Get(leftWeaponIndex.Value);
    public ItemData Armor => ItemDatabase.Get(armorIndex.Value);

    public ItemData GetWeapon(bool leftHand) => leftHand ? LeftWeapon : RightWeapon;
    public float TotalDefense => (LeftWeapon?.defenseBonus ?? 0f) + (RightWeapon?.defenseBonus ?? 0f) + (Armor?.defenseBonus ?? 0f);
    public float TotalMaxHp => (LeftWeapon?.maxHpBonus ?? 0f) + (RightWeapon?.maxHpBonus ?? 0f) + (Armor?.maxHpBonus ?? 0f);

    // ホストだけが呼ぶ(ItemPickup から)
    public void ReceiveItem(ItemData item)
    {
        if (!IsServer || item == null) return;

        var stats = GetComponent<PlayerStats>();
        int index = ItemDatabase.IndexOf(item);

        NetworkVariable<int> slotVar = item.slot == ItemSlot.Weapon ? SelectWeaponSlot() : armorIndex;
        ItemData current = ItemDatabase.Get(slotVar.Value);

        if (index >= 0 && (current == null || item.Score > current.Score))
        {
            slotVar.Value = index;
            stats.ClampHp();
            string slot = slotVar == rightWeaponIndex ? "Right" : slotVar == leftWeaponIndex ? "Left" : "Armor";
            stats.Notify($"Equipped ({slot}): {item.displayName}");
        }
        else
        {
            stats.AddExp(item.salvageExp);
            stats.Notify($"Salvaged {item.displayName} (+{item.salvageExp:0} EXP)");
        }
    }

    private NetworkVariable<int> SelectWeaponSlot()
    {
        if (RightWeapon == null) return rightWeaponIndex;
        if (LeftWeapon == null) return leftWeaponIndex;
        return LeftWeapon.Score <= RightWeapon.Score ? leftWeaponIndex : rightWeaponIndex;
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
        anchor.transform.localScale = Vector3.one * (IsOwner ? 0.55f : 1f);
        Instantiate(weapon.pickupVisualPrefab, anchor.transform, false);
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
