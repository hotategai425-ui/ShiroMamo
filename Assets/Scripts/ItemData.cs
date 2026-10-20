using UnityEngine;

public enum ItemSlot { Weapon, Armor }
public enum ItemRarity { Common, Rare, Epic }

// Project ウィンドウで Create > TD > Item Data から作成
[CreateAssetMenu(menuName = "TD/Item Data", fileName = "ItemData")]
public class ItemData : ScriptableObject
{
    public string displayName = "Item";
    public ItemSlot slot = ItemSlot.Weapon;
    public ItemRarity rarity = ItemRarity.Common;

    [Header("装備ボーナス")]
    public float attackBonus;
    public float defenseBonus;
    public float maxHpBonus;

    [Header("装備しなかった場合に経験値へ変換される量")]
    public float salvageExp = 5f;

    // 装備比較用の総合スコア
    public float Score => attackBonus * 2f + defenseBonus * 2f + maxHpBonus * 0.2f;

    public Color RarityColor
    {
        get
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return new Color(0.3f, 0.5f, 1f);
                case ItemRarity.Epic: return new Color(0.7f, 0.3f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
