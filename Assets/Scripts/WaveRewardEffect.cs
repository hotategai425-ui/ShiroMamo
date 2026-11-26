using System;
using Unity.Netcode;

public enum WaveRewardEffect { None = 0, Guardian = 1, MultiJump = 2 }
public enum GuardianActivationStatus { Unowned = 0, Active = 1, OutOfRange = 2, Unavailable = 3 }

// Host snapshots these settings when issuing an offer; later Inspector edits cannot alter that grant.
public struct WaveRewardGrant : INetworkSerializable, IEquatable<WaveRewardGrant>
{
    public int DefinitionId;
    public WaveRewardEffect Effect;
    public bool AllowRepeat;
    public float OutsideDistance, AttackBonus;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref DefinitionId); serializer.SerializeValue(ref Effect); serializer.SerializeValue(ref AllowRepeat);
        serializer.SerializeValue(ref OutsideDistance); serializer.SerializeValue(ref AttackBonus);
    }
    public bool Equals(WaveRewardGrant other) => DefinitionId == other.DefinitionId && Effect == other.Effect
        && AllowRepeat == other.AllowRepeat && OutsideDistance.Equals(other.OutsideDistance) && AttackBonus.Equals(other.AttackBonus);
}

public struct WaveRewardEffectOffer : INetworkSerializable, IEquatable<WaveRewardEffectOffer>
{
    public ulong Ticket;
    public WaveRewardGrant First, Second, Third;
    public WaveRewardGrant Find(int id) => First.DefinitionId == id ? First : Second.DefinitionId == id ? Second
        : Third.DefinitionId == id ? Third : default;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ticket); serializer.SerializeValue(ref First);
        serializer.SerializeValue(ref Second); serializer.SerializeValue(ref Third);
    }
    public bool Equals(WaveRewardEffectOffer other) => Ticket == other.Ticket && First.Equals(other.First)
        && Second.Equals(other.Second) && Third.Equals(other.Third);
}

public struct GuardianEffectState : INetworkSerializable, IEquatable<GuardianEffectState>
{
    public bool Owned;
    public GuardianActivationStatus Status;
    public bool Active => Status == GuardianActivationStatus.Active;
    public float OutsideDistance, AttackBonus;
    public float AttackMultiplier => Owned && Active ? 1f + AttackBonus : 1f;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Owned); serializer.SerializeValue(ref Status);
        serializer.SerializeValue(ref OutsideDistance); serializer.SerializeValue(ref AttackBonus);
    }
    public bool Equals(GuardianEffectState other) => Owned == other.Owned && Status == other.Status
        && OutsideDistance.Equals(other.OutsideDistance) && AttackBonus.Equals(other.AttackBonus);
}

public static class WaveRewardDescription
{
    public static string MultiJump => "最大ジャンプ回数が地上を含め2回になります。空中でも新しくSpaceを押してジャンプできます。取得後のレベルアップでジャンプ回数強化が抽選候補になります。重複しません。";
    public static string None(string description) => string.IsNullOrWhiteSpace(description)
        ? "効果なし（試験用）" : "効果なし（試験用）\n" + description;
    public static string Guardian(float outsideDistance, float attackBonus)
        => $"砦の外周から水平{outsideDistance:0.##}m以内で、自分の攻撃力が{attackBonus * 100f:0.##}%上昇します。重複しません。";
}
