using UnityEngine;

// 武器・防具スロット。拾った装備が今より良ければ自動で装備、
// そうでなければ経験値に変換する。
public class PlayerEquipment : MonoBehaviour
{
    public ItemData Weapon { get; private set; }
    public ItemData Armor { get; private set; }

    public float TotalAttack => (Weapon != null ? Weapon.attackBonus : 0f) + (Armor != null ? Armor.attackBonus : 0f);
    public float TotalDefense => (Weapon != null ? Weapon.defenseBonus : 0f) + (Armor != null ? Armor.defenseBonus : 0f);
    public float TotalMaxHp => (Weapon != null ? Weapon.maxHpBonus : 0f) + (Armor != null ? Armor.maxHpBonus : 0f);

    public void ReceiveItem(ItemData item)
    {
        if (item == null) return;

        var stats = GetComponent<PlayerStats>();
        ItemData current = item.slot == ItemSlot.Weapon ? Weapon : Armor;

        if (current == null || item.Score > current.Score)
        {
            if (item.slot == ItemSlot.Weapon) Weapon = item;
            else Armor = item;

            if (stats != null) stats.ClampHp();
            GameLog.Post($"Equipped: {item.displayName}");
        }
        else
        {
            if (stats != null) stats.AddExp(item.salvageExp);
            GameLog.Post($"Salvaged {item.displayName} (+{item.salvageExp:0} EXP)");
        }
    }
}
