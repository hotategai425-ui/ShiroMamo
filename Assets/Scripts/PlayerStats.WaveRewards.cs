using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerStats
{
    [Header("ウェーブ終了の個人3択 (未設定ならResourcesの専用カタログ)")]
    [SerializeField] private WaveRewardCatalog waveRewardCatalog;
    private NetworkVariable<WaveRewardOffer> waveRewardOffer = new(default, NetworkVariableReadPermission.Owner);
    private NetworkVariable<WaveRewardEffectOffer> waveRewardEffects = new(default, NetworkVariableReadPermission.Owner);
    private NetworkList<WaveRewardResult> waveRewardResults;
    private ulong nextWaveRewardTicket;
    private readonly Dictionary<int, WaveRewardGrant> offeredRewardGrants = new();
    private readonly HashSet<int> acquiredRewardIds = new();
    private readonly HashSet<int> nonRepeatableRewardIds = new();

    public WaveRewardOffer CurrentWaveRewardOffer => waveRewardOffer.Value;
    public NetworkList<WaveRewardResult> WaveRewardResults => waveRewardResults;
    public WaveRewardCatalog RewardCatalog
    {
        get
        {
            if (waveRewardCatalog == null) waveRewardCatalog = Resources.Load<WaveRewardCatalog>("Progression/WaveRewardCatalog");
            return waveRewardCatalog;
        }
    }

    public string GetWaveRewardDescription(int id)
    {
        if (CurrentWaveRewardOffer.IsValid && id > 0)
        {
            var effects = waveRewardEffects.Value;
            if (effects.Ticket != CurrentWaveRewardOffer.Ticket) return "提示内容を同期しています……";
            var grant = effects.Find(id);
            if (grant.DefinitionId == id)
                return grant.Effect == WaveRewardEffect.Guardian
                    ? WaveRewardDescription.Guardian(grant.OutsideDistance, grant.AttackBonus)
                    : grant.Effect == WaveRewardEffect.MultiJump ? WaveRewardDescription.MultiJump
                    : WaveRewardDescription.None(RewardCatalog?.Find(id)?.description);
        }
        return RewardCatalog != null ? RewardCatalog.GetDescription(id) : "候補の設定が見つかりません。";
    }

    internal void IssueWaveReward(int completedWave)
    {
        var catalog = RewardCatalog;
        int[] choices = catalog != null ? catalog.Draw(this) : new int[0];
        offeredRewardGrants.Clear();
        foreach (int id in choices) offeredRewardGrants.Add(id, catalog.Find(id).Snapshot());
        if (choices.Length == 0)
        {
            Debug.LogWarning("WaveRewardCatalogに有効な候補がありません。このプレイヤーのウェーブ選択をスキップします。", this);
            return;
        }
        ulong ticket = ++nextWaveRewardTicket;
        waveRewardEffects.Value = new WaveRewardEffectOffer { Ticket = ticket, First = offeredRewardGrants[choices[0]],
            Second = choices.Length > 1 ? offeredRewardGrants[choices[1]] : default,
            Third = choices.Length > 2 ? offeredRewardGrants[choices[2]] : default };
        waveRewardOffer.Value = new WaveRewardOffer { Ticket = ticket, Wave = completedWave,
            First = choices[0], Second = choices.Length > 1 ? choices[1] : 0, Third = choices.Length > 2 ? choices[2] : 0 };
    }

    internal bool CanReceiveWaveReward(WaveRewardGrant grant) => !nonRepeatableRewardIds.Contains(grant.DefinitionId)
        && (grant.AllowRepeat || !acquiredRewardIds.Contains(grant.DefinitionId))
        && (grant.Effect == WaveRewardEffect.None || !HasWaveRewardEffect(grant.Effect));

    public void ChooseWaveReward(ulong ticket, int choiceIndex)
    {
        if (!IsSpawned || !IsOwner || !IsWaveRewardChoicePaused) return;
        ChooseWaveRewardRpc(ticket, choiceIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ChooseWaveRewardRpc(ulong ticket, int choiceIndex)
    {
        var offer = waveRewardOffer.Value;
        if (!IsWaveRewardChoicePaused || !offer.IsValid || offer.Ticket != ticket || choiceIndex < 0 || choiceIndex > 2) return;
        int id = offer.GetChoice(choiceIndex);
        if (id <= 0 || !offeredRewardGrants.TryGetValue(id, out var grant) || !CanReceiveWaveReward(grant)) return;
        acquiredRewardIds.Add(id);
        if (!grant.AllowRepeat) nonRepeatableRewardIds.Add(id);
        ApplyWaveRewardEffect(grant);
        waveRewardResults.Add(new WaveRewardResult { DefinitionId = id, Wave = offer.Wave, Ticket = offer.Ticket });
        waveRewardOffer.Value = default;
        waveRewardEffects.Value = default;
        offeredRewardGrants.Clear();
        TeamProgression.For(NetworkManager).ChoiceCompleted();
    }
}
