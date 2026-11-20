using System;
using System.Collections.Generic;
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
    public float AttackMultiplier;
    public float DamageReductionRate;
    public float MaxHpBonus;
    public WeaponSkillLoadout Skills;

    // 旧読み取りUI/APIの移行用。旧加算値や防御を新しい単位へ読み替えない。
    [Obsolete("加算攻撃は廃止。AttackMultiplierを使用してください。")]
    public float AttackBonus => 0f;
    [Obsolete("防御力は廃止。DamageReductionRateを使用してください。")]
    public float DefenseBonus => 0f;

    public bool IsValid => InstanceId != 0 && DefinitionIndex >= 0;
    public ItemData Definition => IsValid ? ItemDatabase.Get(DefinitionIndex) : null;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref InstanceId);
        serializer.SerializeValue(ref DefinitionIndex);
        serializer.SerializeValue(ref Rarity);
        serializer.SerializeValue(ref AttackMultiplier);
        serializer.SerializeValue(ref DamageReductionRate);
        serializer.SerializeValue(ref MaxHpBonus);
        serializer.SerializeValue(ref Skills);
    }

    public bool Equals(ItemInstance other) => InstanceId == other.InstanceId
        && DefinitionIndex == other.DefinitionIndex && Rarity == other.Rarity
        && AttackMultiplier.Equals(other.AttackMultiplier) && DamageReductionRate.Equals(other.DamageReductionRate)
        && MaxHpBonus.Equals(other.MaxHpBonus) && Skills.Equals(other.Skills);

    public override bool Equals(object obj) => obj is ItemInstance other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(InstanceId, DefinitionIndex, Rarity, AttackMultiplier, DamageReductionRate, MaxHpBonus, Skills);

    internal static ItemInstance Create(ulong id, int definitionIndex, ItemData definition, WeaponRarityRoll rarityOverride = null)
    {
        var item = new ItemInstance
        {
            InstanceId = id,
            DefinitionIndex = definitionIndex,
            Rarity = definition.rarity,
            AttackMultiplier = SanitizePositive(definition.attackMultiplier),
            DamageReductionRate = definition.IsShield ? SanitizeRate(definition.damageReductionRate) : 0f,
            MaxHpBonus = definition.maxHpBonus
        };
        if (definition.slot != ItemSlot.Weapon) return item;

        WeaponRarityRoll setting = rarityOverride ?? PickRarity(definition.weaponRarityRolls);
        if (setting != null)
        {
            item.Rarity = setting.rarity;
            item.AttackMultiplier = RollRange(setting.attackMultiplierRange, 1000f);
            item.MaxHpBonus += RollRange(setting.maxHpBonusRange, 10f);
            item.DamageReductionRate = definition.IsShield ? SanitizeRate(setting.damageReductionRate) : 0f;
        }
        item.Skills = RollSkills(definition, item.Rarity);
        return item;
    }

    private static WeaponSkillLoadout RollSkills(ItemData definition, ItemRarity rarity)
    {
        var result = new WeaponSkillLoadout
        {
            Innate = definition.AllowsSkill(definition.innateSkill) ? definition.innateSkill : WeaponSkillId.None
        };
        int slots = rarity == ItemRarity.Epic ? 2 : rarity == ItemRarity.Rare ? 1 : 0;
        if (slots == 0 || definition.commonSkillCandidates == null) return result;
        var candidates = new List<WeaponSkillId>();
        foreach (var skill in definition.commonSkillCandidates)
            if (definition.AllowsSkill(skill) && !candidates.Contains(skill)) candidates.Add(skill);
        if (candidates.Count == 0) return result;
        result.CommonFirst = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        if (slots == 2) result.CommonSecond = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return result;
    }

    private static float RollRange(Vector2 range, float precision)
    {
        float low = SanitizePositive(Mathf.Min(range.x, range.y));
        float high = SanitizePositive(Mathf.Max(range.x, range.y));
        return Mathf.Round(UnityEngine.Random.Range(low, high) * precision) / precision;
    }

    private static float SanitizePositive(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    internal static float SanitizeRate(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);

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
