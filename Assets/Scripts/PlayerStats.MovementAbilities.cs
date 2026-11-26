using Unity.Netcode;

public partial class PlayerStats
{
    private NetworkVariable<bool> multiJump = new(false, NetworkVariableReadPermission.Owner);
    public bool HasMultiJump => multiJump.Value;
    public ulong MaxJumpCount => HasMultiJump ? SaturatingJumpAdd(2, upgradeBonuses.Value.JumpCount) : 1;

    public bool HasWaveRewardEffect(WaveRewardEffect effect) => effect switch
    {
        WaveRewardEffect.Guardian => HasGuardian,
        WaveRewardEffect.MultiJump => HasMultiJump,
        _ => false
    };

    internal static ulong SaturatingJumpAdd(ulong current, ulong additional)
        => additional > ulong.MaxValue - current ? ulong.MaxValue : current + additional;
}
