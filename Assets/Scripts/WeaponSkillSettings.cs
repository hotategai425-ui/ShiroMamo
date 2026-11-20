using UnityEngine;

[CreateAssetMenu(menuName = "TD/Weapon Skill Settings", fileName = "WeaponSkillSettings")]
public class WeaponSkillSettings : ScriptableObject
{
    [Min(0f)] public float poisonDamageRate = 0.1f;
    [Min(0.01f)] public float poisonDuration = 3f;
    [Min(0.01f)] public float poisonTickInterval = 1f;
    [Min(0)] public int pierceAdditionalTargets = 2;
    [Min(0f)] public float reflectDamageRate = 0.2f;

    public float PoisonRate => NonNegative(poisonDamageRate);
    public float PoisonDuration => Mathf.Max(0.01f, NonNegative(poisonDuration));
    public float PoisonInterval => Mathf.Max(0.01f, NonNegative(poisonTickInterval));
    public float ReflectRate => NonNegative(reflectDamageRate);
    public int GetPierceTargetLimit(int count) => (int)System.Math.Min(int.MaxValue,
        1L + (long)Mathf.Max(0, pierceAdditionalTargets) * Mathf.Clamp(count, 0, 3));
    private static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);

    private static WeaponSkillSettings current;
    public static WeaponSkillSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<WeaponSkillSettings>("WeaponSkillSettings");
                if (current == null)
                {
                    current = CreateInstance<WeaponSkillSettings>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return current;
        }
    }

    public static string GetDisplayName(WeaponSkillId id)
    {
        switch (id)
        {
            case WeaponSkillId.Poison: return "毒";
            case WeaponSkillId.Pierce: return "貫通";
            case WeaponSkillId.Reflect: return "反射";
            default: return "なし";
        }
    }

    public static string GetDescription(WeaponSkillId id)
    {
        var s = Current;
        switch (id)
        {
            case WeaponSkillId.Poison:
                return $"攻撃ダメージの{s.poisonDamageRate:P0}を{s.poisonTickInterval:0.##}秒ごとに{s.poisonDuration:0.##}秒間。個数で加算、再命中で更新。";
            case WeaponSkillId.Pierce:
                return $"1個につき命中できる敵が{s.pierceAdditionalTargets}体増加。各敵に100%、同じ敵には1回。";
            case WeaponSkillId.Reflect:
                return $"敵の直接攻撃を受けた時、軽減前ダメージの{s.reflectDamageRate:P0}を攻撃元へ返す。両手の盾が持つ反射スキルの個数で加算。";
            default: return "スキルなし";
        }
    }
}
