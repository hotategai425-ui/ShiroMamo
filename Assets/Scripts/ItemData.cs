using System;
using UnityEngine;

public enum ItemSlot { Weapon, Armor }
public enum ItemRarity { Common, Rare, Epic }
public enum WeaponAttackType { Melee, Projectile }

[Serializable]
public class WeaponRarityRoll
{
    public ItemRarity rarity;
    [Min(0f)] public float weight = 1f;
    [Range(0, 3)] public int bonusCount = 1;
    [Tooltip("武器の基本値に加算する範囲。両端が0の能力は抽選候補から外れます。")]
    public Vector2 attackBonusRange;
    public Vector2 defenseBonusRange;
    public Vector2 maxHpBonusRange;

    public float Weight => float.IsNaN(weight) || float.IsInfinity(weight) ? 0f : Mathf.Max(0f, weight);

    public Vector2 GetRange(int bonus)
    {
        switch (bonus)
        {
            case 0: return attackBonusRange;
            case 1: return defenseBonusRange;
            default: return maxHpBonusRange;
        }
    }
}

// Project ウィンドウで Create > TD > Item Data から作成
[CreateAssetMenu(menuName = "TD/Item Data", fileName = "ItemData")]
public class ItemData : ScriptableObject
{
    public string displayName = "Item";
    public ItemSlot slot = ItemSlot.Weapon;
    public ItemRarity rarity = ItemRarity.Common;
    [Tooltip("ドロップ時と武器装備時に使う表示用プレハブ。空ならドロップ時だけ仮モデルを使います。")]
    public GameObject pickupVisualPrefab;

    [Header("武器の攻撃方式")]
    public WeaponAttackType attackType = WeaponAttackType.Melee;
    public MagicProjectile projectilePrefab;
    [Min(0.1f)] public float projectileSpeed = 20f;
    [Min(0.1f)] public float projectileLifetime = 2f;

    [Header("近接武器の形と動作 (武器のローカル座標)")]
    public Vector3 bladeBase = new Vector3(0f, 0.2f, 0f);
    public Vector3 bladeTip = new Vector3(0f, 1.4f, 0f);
    [Min(0.01f)] public float bladeRadius = 0.06f;
    [Min(0.1f)] public float swingDuration = 0.3f;
    [Min(0.1f)] public float meleeScale = 0.55f;

    [Header("装備ボーナス")]
    public float attackBonus;
    public float defenseBonus;
    public float maxHpBonus;

    [Header("武器の個体抽選 (空なら上記の固定性能を使います)")]
    public WeaponRarityRoll[] weaponRarityRolls;

    [Header("装備しなかった場合に経験値へ変換される量")]
    public float salvageExp = 5f;

    // 装備比較用の総合スコア
    public float Score => attackBonus * 2f + defenseBonus * 2f + maxHpBonus * 0.2f;

    public Color RarityColor => GetRarityColor(rarity);

    public static Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare: return new Color(0.3f, 0.5f, 1f);
            case ItemRarity.Epic: return new Color(0.7f, 0.3f, 1f);
            default: return new Color(0.85f, 0.85f, 0.85f);
        }
    }
}
