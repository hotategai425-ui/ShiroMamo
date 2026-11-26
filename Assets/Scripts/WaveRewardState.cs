using System;
using Unity.Netcode;

[Flags]
public enum TeamPauseReason : byte { None = 0, LevelUp = 1, WaveReward = 2 }

public struct WaveRewardOffer : INetworkSerializable, IEquatable<WaveRewardOffer>
{
    public ulong Ticket;
    public int Wave;
    public int First, Second, Third;
    public bool IsValid => Ticket != 0 && First > 0;
    public int GetChoice(int index) => index == 0 ? First : index == 1 ? Second : index == 2 ? Third : 0;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ticket); serializer.SerializeValue(ref Wave);
        serializer.SerializeValue(ref First); serializer.SerializeValue(ref Second); serializer.SerializeValue(ref Third);
    }
    public bool Equals(WaveRewardOffer other) => Ticket == other.Ticket && Wave == other.Wave
        && First == other.First && Second == other.Second && Third == other.Third;
}

public struct WaveRewardResult : INetworkSerializable, IEquatable<WaveRewardResult>
{
    public int DefinitionId, Wave;
    public ulong Ticket;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    { serializer.SerializeValue(ref DefinitionId); serializer.SerializeValue(ref Wave); serializer.SerializeValue(ref Ticket); }
    public bool Equals(WaveRewardResult other) => DefinitionId == other.DefinitionId && Wave == other.Wave && Ticket == other.Ticket;
}
