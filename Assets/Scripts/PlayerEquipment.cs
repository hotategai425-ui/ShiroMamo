using Unity.Netcode;
using UnityEngine;

// 武器・防具スロット。拾った装備が今より良ければ自動で装備、
// そうでなければ経験値に変換する。(判定はホスト、装備中の番号は全員に同期)
public class PlayerEquipment : NetworkBehaviour
{
    private NetworkVariable<int> weaponIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<int> armorIndex = new NetworkVariable<int>(-1);

    public ItemData Weapon => ItemDatabase.Get(weaponIndex.Value);
    public ItemData Armor => ItemDatabase.Get(armorIndex.Value);

    public float TotalAttack => (Weapon != null ? Weapon.attackBonus : 0f) + (Armor != null ? Armor.attackBonus : 0f);
    public float TotalDefense => (Weapon != null ? Weapon.defenseBonus : 0f) + (Armor != null ? Armor.defenseBonus : 0f);
    public float TotalMaxHp => (Weapon != null ? Weapon.maxHpBonus : 0f) + (Armor != null ? Armor.maxHpBonus : 0f);

    // ホストだけが呼ぶ(ItemPickup から)
    public void ReceiveItem(ItemData item)
    {
        if (!IsServer || item == null) return;

        var stats = GetComponent<PlayerStats>();
        int index = ItemDatabase.IndexOf(item);

        NetworkVariable<int> slotVar = item.slot == ItemSlot.Weapon ? weaponIndex : armorIndex;
        ItemData current = ItemDatabase.Get(slotVar.Value);

        if (index >= 0 && (current == null || item.Score > current.Score))
        {
            slotVar.Value = index;
            stats.ClampHp();
            stats.Notify($"Equipped: {item.displayName}");
        }
        else
        {
            stats.AddExp(item.salvageExp);
            stats.Notify($"Salvaged {item.displayName} (+{item.salvageExp:0} EXP)");
        }
    }
}
