using Unity.Netcode;
using UnityEngine;

public partial class PlayerStats
{
    private NetworkVariable<GuardianEffectState> guardian = new(default, NetworkVariableReadPermission.Owner);

    public bool HasGuardian => guardian.Value.Owned;
    public GuardianActivationStatus GuardianStatus => !HasGuardian ? GuardianActivationStatus.Unowned
        : !IsSpawned || IsDead ? GuardianActivationStatus.Unavailable : GuardianState.Status;
    public bool IsGuardianActive => GuardianStatus == GuardianActivationStatus.Active;
    public GuardianEffectState GuardianState
    {
        get
        {
            if (IsSpawned && IsServer) RefreshGuardianState();
            return guardian.Value;
        }
    }
    public float GuardianAttackMultiplier => IsGuardianActive ? guardian.Value.AttackMultiplier : 1f;
    public string GuardianDescription => HasGuardian
        ? WaveRewardDescription.Guardian(guardian.Value.OutsideDistance, guardian.Value.AttackBonus) : "守護者は未取得です。";

    private void LateUpdate()
    {
        if (IsSpawned && IsServer) RefreshGuardianState();
    }

    private void RefreshGuardianState()
    {
        var state = guardian.Value;
        state.Status = state.Owned ? GuardianActivationStatus.Unavailable : GuardianActivationStatus.Unowned;
        if (state.Owned && !IsDead && NetworkManager != null && NetworkManager.SpawnManager != null)
        {
            foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                var fortress = obj.GetComponent<Fortress>();
                if (fortress == null || !fortress.IsSpawned || fortress.CurrentHp <= 0f) continue;
                float radius = fortress.Radius;
                if (float.IsNaN(radius) || float.IsInfinity(radius)) continue;
                Vector3 delta = transform.position - fortress.transform.position;
                delta.y = 0f;
                float range = Mathf.Max(0f, radius) + state.OutsideDistance;
                state.Status = GuardianActivationStatus.OutOfRange;
                if (delta.sqrMagnitude <= range * range) { state.Status = GuardianActivationStatus.Active; break; }
            }
        }
        if (!state.Equals(guardian.Value)) guardian.Value = state;
    }

    private void ApplyWaveRewardEffect(WaveRewardGrant grant)
    {
        if (grant.Effect == WaveRewardEffect.MultiJump)
        {
            multiJump.Value = true;
            return;
        }
        if (grant.Effect != WaveRewardEffect.Guardian || HasGuardian) return;
        guardian.Value = new GuardianEffectState { Owned = true, OutsideDistance = grant.OutsideDistance, AttackBonus = grant.AttackBonus };
        RefreshGuardianState();
    }
}
