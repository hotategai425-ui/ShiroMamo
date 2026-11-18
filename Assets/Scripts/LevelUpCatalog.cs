using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeStat { Attack, MaxHp, Defense, CriticalChance, CriticalDamage, AttackSpeed, MoveSpeed, JumpHeight }

[Serializable]
public class LevelUpOption
{
    [Min(1)] public int id;
    public string displayName;
    public UpgradeStat stat;
    [Min(0f)] public float amount;
    [Min(0f)] public float weight = 1f;
}

[CreateAssetMenu(menuName = "TD/Level Up Catalog", fileName = "LevelUpCatalog")]
public class LevelUpCatalog : ScriptableObject
{
    [Tooltip("IDは重複させず、追加後は変更しないでください。割合は0.05で5%です。")]
    public LevelUpOption[] options;

    public LevelUpOption Find(int id)
    {
        if (options != null) foreach (var option in options)
            if (option != null && option.id == id) return option;
        return null;
    }

    public UpgradeChoice[] Draw(PlayerStats player)
    {
        var pool = new List<LevelUpOption>();
        var ids = new HashSet<int>();
        if (options != null) foreach (var option in options)
        {
            if (option == null || option.id <= 0 || !(option.amount > 0f) || float.IsInfinity(option.amount)
                || !(option.weight > 0f) || float.IsInfinity(option.weight)
                || !Enum.IsDefined(typeof(UpgradeStat), option.stat) || !ids.Add(option.id)) continue;
            if (option.stat == UpgradeStat.CriticalChance && player.CriticalChance >= 1f) continue;
            pool.Add(option);
        }
        var result = new List<UpgradeChoice>();
        while (result.Count < 3 && pool.Count > 0)
        {
            // doubleで合計し、大きな抽選重みによるfloatのオーバーフローを避ける。
            double total = 0;
            foreach (var option in pool) total += option.weight;
            double draw = UnityEngine.Random.value * total;
            LevelUpOption picked = pool[pool.Count - 1];
            foreach (var option in pool)
            {
                draw -= option.weight;
                if (draw <= 0) { picked = option; break; }
            }
            result.Add(new UpgradeChoice { DefinitionId = picked.id, Stat = picked.stat, Amount = picked.amount });
            pool.RemoveAll(option => option.stat == picked.stat);
        }
        return result.ToArray();
    }
}
