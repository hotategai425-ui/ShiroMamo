using System;
using System.Collections.Generic;
using UnityEngine;

// 廃止した防御の値2は再利用しない。既存アセットと提示IDの意味を維持する。
public enum UpgradeStat { Attack = 0, MaxHp = 1, CriticalChance = 3, CriticalDamage = 4, AttackSpeed = 5, MoveSpeed = 6, JumpHeight = 7, JumpCount = 8 }

[Serializable]
public class LevelUpOption
{
    [Min(1)] public int id;
    public string displayName;
    public UpgradeStat stat;
    [Min(0f)] public float amount;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Noneなら条件なし。指定したウェーブ効果を本人が取得した後だけ抽選します。")]
    public WaveRewardEffect requiredEffect;
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
            if (option.requiredEffect != WaveRewardEffect.None && !player.HasWaveRewardEffect(option.requiredEffect)) continue;
            if (option.stat == UpgradeStat.JumpCount && (option.amount != Math.Floor(option.amount) || player.MaxJumpCount == ulong.MaxValue)) continue;
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
            result.Add(new UpgradeChoice { DefinitionId = picked.id, Stat = picked.stat, Amount = picked.amount, RequiredEffect = picked.requiredEffect });
            pool.RemoveAll(option => option.stat == picked.stat);
        }
        return result.ToArray();
    }
}
