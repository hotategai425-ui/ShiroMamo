// 追加効果の入口を直接命中に限定する。毒・反射は会心再抽選や追加効果を起こさない。
public enum DamageKind { Direct, Poison, Reflection }

public struct DamageContext
{
    public AttackDamageResult Damage;
    public PlayerStats Attacker;
    public WeaponSkillLoadout Skills;
    public DamageKind Kind;

    public static DamageContext Direct(AttackDamageResult damage, PlayerStats attacker, WeaponSkillLoadout skills = default)
        => new DamageContext { Damage = damage, Attacker = attacker, Skills = skills, Kind = DamageKind.Direct };

    public static DamageContext Secondary(float amount, PlayerStats attacker, DamageKind kind)
        => new DamageContext { Damage = new AttackDamageResult { Amount = amount }, Attacker = attacker, Kind = kind };
}
