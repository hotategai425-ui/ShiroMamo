using System.Linq;
using System.Text;

// 保存された個体の表示だけを担当。戦闘式・抽選・設定値の合算は行わない。
public static class WeaponSkillDisplay
{
    public static string Build(WeaponSkillLoadout skills)
    {
        var text = new StringBuilder();
        text.Append("固有：").AppendLine(SkillName(skills.Innate)
            + (skills.Innate != WeaponSkillId.None ? " ×1" : "（未設定）"));
        var common = Enumerable.Range(0, 2).Select(skills.GetCommon)
            .Where(id => id != WeaponSkillId.None).GroupBy(id => id).ToArray();
        text.Append("共通：").Append(common.Length == 0 ? "なし" : string.Join(" ／ ",
            common.Select(group => SkillName(group.Key) + " ×" + group.Count())));
        text.Append("（").Append(skills.CommonCount).AppendLine("枠）");
        var ids = new[] { skills.Innate, skills.CommonFirst, skills.CommonSecond }
            .Where(id => id != WeaponSkillId.None).Distinct().ToArray();
        text.Append("合計：").AppendLine(ids.Length == 0 ? "なし／未設定" : string.Join(" ／ ",
            ids.Select(id => SkillName(id) + " ×" + skills.Count(id))));
        if (ids.Length == 0) return text.ToString();
        text.AppendLine("説明は1個あたりです。合計個数と合わせて確認してください。");
        foreach (var id in ids)
        {
            string description = IsSupported(id) ? WeaponSkillSettings.GetDescription(id)
                : "未対応のため効果説明を表示できません。";
            if (id == WeaponSkillId.Reflect)
                description = description.Replace("装備中の盾の個数で加算。", "装備中の両手の盾が持つ反射スキルの個数で加算。");
            text.Append(SkillName(id)).Append("：").AppendLine(description);
        }
        return text.ToString();
    }

    private static bool IsSupported(WeaponSkillId id) => id == WeaponSkillId.Poison
        || id == WeaponSkillId.Pierce || id == WeaponSkillId.Reflect;
    private static string SkillName(WeaponSkillId id) => id == WeaponSkillId.None ? "なし"
        : IsSupported(id) ? WeaponSkillSettings.GetDisplayName(id) : $"未対応（ID {(int)id}）";
}
