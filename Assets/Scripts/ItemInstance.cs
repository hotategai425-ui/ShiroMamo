using System;
using Unity.Netcode;
using UnityEngine;

// 試合中の1個のアイテム。共通の見た目・動作はDefinitionから取得する。
// 性能は生成時に固定し、拾得や装備変更では再抽選しない。
[Serializable]
public struct ItemInstance : INetworkSerializable, IEquatable<ItemInstance>
{
    public ulong InstanceId;
    public int DefinitionIndex;
    public ItemRarity Rarity;
    public float AttackBonus;
    public float DefenseBonus;
    public float MaxHpBonus;

    public bool IsValid => InstanceId != 0 && DefinitionIndex >= 0;
    public ItemData Definition => IsValid ? ItemDatabase.Get(DefinitionIndex) : null;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref InstanceId);
        serializer.SerializeValue(ref DefinitionIndex);
        serializer.SerializeValue(ref Rarity);
        serializer.SerializeValue(ref AttackBonus);
        serializer.SerializeValue(ref DefenseBonus);
        serializer.SerializeValue(ref MaxHpBonus);
    }

    public bool Equals(ItemInstance other) => InstanceId == other.InstanceId
        && DefinitionIndex == other.DefinitionIndex && Rarity == other.Rarity
        && AttackBonus.Equals(other.AttackBonus) && DefenseBonus.Equals(other.DefenseBonus)
        && MaxHpBonus.Equals(other.MaxHpBonus);

    public override bool Equals(object obj) => obj is ItemInstance other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InstanceId, DefinitionIndex, Rarity, AttackBonus, DefenseBonus, MaxHpBonus);

    internal static ItemInstance Create(ulong id, int definitionIndex, ItemData definition)
    {
        var item = new ItemInstance
        {
            InstanceId = id,
            DefinitionIndex = definitionIndex,
            Rarity = definition.rarity,
            AttackBonus = definition.attackBonus,
            DefenseBonus = definition.defenseBonus,
            MaxHpBonus = definition.maxHpBonus
        };
        if (definition.slot != ItemSlot.Weapon) return item;

        WeaponRarityRoll setting = PickRarity(definition.weaponRarityRolls);
        if (setting == null) return item;
        item.Rarity = setting.rarity;

        // 1つの能力を同じ個体に重複抽選しない。候補をシャッフルして選ぶ。
        int[] order = { 0, 1, 2 };
        int selected = 0;
        int count = Mathf.Clamp(setting.bonusCount, 0, order.Length);
        for (int i = 0; i < order.Length && selected < count; i++)
        {
            int swap = UnityEngine.Random.Range(i, order.Length);
            int bonus = order[swap];
            order[swap] = order[i];
            order[i] = bonus;
            Vector2 range = setting.GetRange(bonus);
            float low = Mathf.Max(0f, Mathf.Min(range.x, range.y));
            float high = Mathf.Max(0f, Mathf.Max(range.x, range.y));
            if (!(high > 0f) || float.IsInfinity(high) || float.IsNaN(low)) continue;
            float value = Mathf.Round(UnityEngine.Random.Range(low, high) * 10f) / 10f;
            switch (bonus)
            {
                case 0: item.AttackBonus += value; break;
                case 1: item.DefenseBonus += value; break;
                case 2: item.MaxHpBonus += value; break;
            }
            selected++;
        }
        return item;
    }

    private static WeaponRarityRoll PickRarity(WeaponRarityRoll[] settings)
    {
        if (settings == null) return null;
        float total = 0f;
        foreach (var setting in settings)
            if (setting != null) total += setting.Weight;
        if (!(total > 0f) || float.IsInfinity(total)) return null;

        float draw = UnityEngine.Random.value * total;
        WeaponRarityRoll last = null;
        foreach (var setting in settings)
        {
            if (setting == null || setting.Weight <= 0f) continue;
            last = setting;
            draw -= setting.Weight;
            if (draw <= 0f) return setting;
        }
        return last;
    }
}
