using System;
using Unity.Netcode;

// 保存・通信で使うID。追加時も既存の番号を変更しない。
public enum WeaponSkillId { None = 0, Poison = 1, Pierce = 2, Reflect = 3 }

[Serializable]
public struct WeaponSkillLoadout : INetworkSerializable, IEquatable<WeaponSkillLoadout>
{
    public WeaponSkillId Innate;
    public WeaponSkillId CommonFirst;
    public WeaponSkillId CommonSecond;

    public int CommonCount => (CommonFirst != WeaponSkillId.None ? 1 : 0)
        + (CommonSecond != WeaponSkillId.None ? 1 : 0);
    public WeaponSkillId GetCommon(int index) => index == 0 ? CommonFirst
        : index == 1 ? CommonSecond : WeaponSkillId.None;
    public int Count(WeaponSkillId skill) => skill == WeaponSkillId.None ? 0
        : (Innate == skill ? 1 : 0) + (CommonFirst == skill ? 1 : 0) + (CommonSecond == skill ? 1 : 0);

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Innate);
        serializer.SerializeValue(ref CommonFirst);
        serializer.SerializeValue(ref CommonSecond);
    }

    public bool Equals(WeaponSkillLoadout other) => Innate == other.Innate
        && CommonFirst == other.CommonFirst && CommonSecond == other.CommonSecond;
    public override bool Equals(object obj) => obj is WeaponSkillLoadout other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Innate, CommonFirst, CommonSecond);
}
