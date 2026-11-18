using System;
using Unity.Netcode;

public struct TeamProgressionState : INetworkSerializable, IEquatable<TeamProgressionState>
{
    public int Level;
    public float Exp;
    public bool Paused;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    { serializer.SerializeValue(ref Level); serializer.SerializeValue(ref Exp); serializer.SerializeValue(ref Paused); }
    public bool Equals(TeamProgressionState other) => Level == other.Level && Exp.Equals(other.Exp) && Paused == other.Paused;
}

public struct UpgradeChoice : INetworkSerializable, IEquatable<UpgradeChoice>
{
    public int DefinitionId;
    public UpgradeStat Stat;
    public float Amount;
    public bool IsValid => DefinitionId > 0 && Amount > 0f;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    { serializer.SerializeValue(ref DefinitionId); serializer.SerializeValue(ref Stat); serializer.SerializeValue(ref Amount); }
    public bool Equals(UpgradeChoice other) => DefinitionId == other.DefinitionId && Stat == other.Stat && Amount.Equals(other.Amount);
}

public struct LevelUpOffer : INetworkSerializable, IEquatable<LevelUpOffer>
{
    public ulong Ticket;
    public int Level;
    public int PendingCount;
    public UpgradeChoice First, Second, Third;
    public bool IsValid => Ticket != 0;
    public UpgradeChoice GetChoice(int index) => index == 0 ? First : index == 1 ? Second : index == 2 ? Third : default;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ticket); serializer.SerializeValue(ref Level); serializer.SerializeValue(ref PendingCount);
        serializer.SerializeValue(ref First); serializer.SerializeValue(ref Second); serializer.SerializeValue(ref Third);
    }
    public bool Equals(LevelUpOffer other) => Ticket == other.Ticket && Level == other.Level && PendingCount == other.PendingCount
        && First.Equals(other.First) && Second.Equals(other.Second) && Third.Equals(other.Third);
}

public struct PlayerUpgradeBonuses : INetworkSerializable, IEquatable<PlayerUpgradeBonuses>
{
    public float Attack, MaxHp, Defense, CriticalChance, CriticalDamage, AttackSpeed, MoveSpeed, JumpHeight;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Attack); serializer.SerializeValue(ref MaxHp); serializer.SerializeValue(ref Defense);
        serializer.SerializeValue(ref CriticalChance); serializer.SerializeValue(ref CriticalDamage); serializer.SerializeValue(ref AttackSpeed);
        serializer.SerializeValue(ref MoveSpeed); serializer.SerializeValue(ref JumpHeight);
    }
    public bool Equals(PlayerUpgradeBonuses other) => Attack.Equals(other.Attack) && MaxHp.Equals(other.MaxHp) && Defense.Equals(other.Defense)
        && CriticalChance.Equals(other.CriticalChance) && CriticalDamage.Equals(other.CriticalDamage)
        && AttackSpeed.Equals(other.AttackSpeed) && MoveSpeed.Equals(other.MoveSpeed) && JumpHeight.Equals(other.JumpHeight);
}
