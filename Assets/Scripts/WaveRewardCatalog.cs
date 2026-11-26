using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveRewardOption
{
    public bool enabled = true;
    [Tooltip("取得結果で使う安定ID。追加後は変更・再利用しないでください。"), Min(1)] public int id;
    public string displayName;
    [TextArea] public string description;
    [Tooltip("相対抽選重み。0以下・非有限値は抽選対象外。"), Min(0f)] public float weight = 1f;
    public WaveRewardEffect effect;
    [Tooltip("取得後も抽選可能にします。守護者はこの値にかかわらず重複不可です。")]
    public bool allowRepeat = true;
    [Min(0f)] public float guardianOutsideDistance = 5f;
    [Min(0f)] public float guardianAttackBonus = 0.25f;
    public bool HasEffect => effect == WaveRewardEffect.Guardian;

    public WaveRewardGrant Snapshot() => new WaveRewardGrant { DefinitionId = id, Effect = effect,
        AllowRepeat = allowRepeat && effect != WaveRewardEffect.Guardian,
        OutsideDistance = guardianOutsideDistance, AttackBonus = guardianAttackBonus };

    public bool HasValidEffectSettings => effect == WaveRewardEffect.None || effect == WaveRewardEffect.Guardian
        && guardianOutsideDistance >= 0f && !float.IsInfinity(guardianOutsideDistance)
        && guardianAttackBonus >= 0f && !float.IsInfinity(guardianAttackBonus);
}

[CreateAssetMenu(menuName = "TD/Wave Reward Catalog", fileName = "WaveRewardCatalog")]
public sealed class WaveRewardCatalog : ScriptableObject
{
    [Tooltip("異なる有効IDを最大3件抽選。3候補だけなら重みは並び順に影響します。")]
    public WaveRewardOption[] options;

    public WaveRewardOption Find(int id)
    {
        if (options != null) foreach (var option in options)
            if (option != null && option.id == id) return option;
        return null;
    }

    public string GetDescription(int id)
    {
        var option = Find(id);
        if (option == null) return "候補の設定が見つかりません。";
        return option.HasEffect ? WaveRewardDescription.Guardian(option.guardianOutsideDistance, option.guardianAttackBonus)
            : WaveRewardDescription.None(option.description);
    }

    public int[] Draw(PlayerStats player = null)
    {
        var pool = new List<WaveRewardOption>();
        var ids = new HashSet<int>();
        if (options != null) foreach (var option in options)
            if (option != null && option.enabled && option.id > 0 && option.weight > 0f
                && !float.IsInfinity(option.weight) && option.HasValidEffectSettings && ids.Add(option.id)
                && ReferenceEquals(Find(option.id), option)
                && (player == null || player.CanReceiveWaveReward(option.Snapshot()))) pool.Add(option);
        var result = new List<int>();
        while (result.Count < 3 && pool.Count > 0)
        {
            double total = 0;
            foreach (var option in pool) total += option.weight;
            double draw = UnityEngine.Random.value * total;
            var picked = pool[pool.Count - 1];
            foreach (var option in pool)
            {
                draw -= option.weight;
                if (draw <= 0) { picked = option; break; }
            }
            result.Add(picked.id);
            pool.Remove(picked);
        }
        return result.ToArray();
    }
}
