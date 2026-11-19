using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerStats
{
    [Header("レベルアップの選択肢 (未設定ならResourcesの共通カタログ)")]
    [SerializeField] private LevelUpCatalog levelUpCatalog;
    private NetworkVariable<TeamProgressionState> teamProgression = new(new TeamProgressionState { Level = 1 });
    private NetworkVariable<PlayerUpgradeBonuses> upgradeBonuses = new();
    private NetworkVariable<LevelUpOffer> levelUpOffer = new(default, NetworkVariableReadPermission.Owner);
    private readonly Queue<int> queuedLevels = new();
    private ulong nextOfferTicket;

    public bool IsTeamLevelUpPaused => teamProgression.Value.Paused;
    public LevelUpOffer CurrentLevelUpOffer => levelUpOffer.Value;
    public int PendingLevelUps => levelUpOffer.Value.PendingCount;
    public PlayerUpgradeBonuses UpgradeBonuses => upgradeBonuses.Value;
    public LevelUpCatalog UpgradeCatalog
    {
        get
        {
            if (levelUpCatalog == null) levelUpCatalog = Resources.Load<LevelUpCatalog>("Progression/LevelUpCatalog");
            return levelUpCatalog;
        }
    }

    internal float RequiredExperience(int forLevel) => expToNextBase * Mathf.Pow(expGrowth, forLevel - 1);

    internal void SyncTeamProgression(TeamProgressionState state, bool heal)
    {
        teamProgression.Value = state;
        if (heal && !IsDead) currentHp.Value = MaxHp;
    }

    private void OnTeamProgressionChanged(TeamProgressionState previous, TeamProgressionState current)
    {
        if (current.Paused) GetComponent<PlayerAttack>()?.CancelEquipmentAttacks();
        if (IsOwner)
        {
            Time.timeScale = current.Paused ? 0f : 1f;
            if (current.Paused) equipment.CloseInventory();
        }
    }

    internal void QueueLevelUp(int forLevel)
    {
        queuedLevels.Enqueue(forLevel);
        RefreshLevelUpOffer();
    }

    internal void ClearLevelUpQueue()
    {
        queuedLevels.Clear();
        levelUpOffer.Value = default;
    }

    private void RefreshLevelUpOffer()
    {
        LevelUpOffer offer = levelUpOffer.Value;
        while (!offer.IsValid && queuedLevels.Count > 0)
        {
            int forLevel = queuedLevels.Dequeue();
            var catalog = UpgradeCatalog;
            var choices = catalog != null ? catalog.Draw(this) : new UpgradeChoice[0];
            if (choices.Length == 0)
            {
                Debug.LogError("LevelUpCatalogに有効な候補がありません。選択をスキップします。", this);
                continue;
            }
            offer = new LevelUpOffer { Ticket = ++nextOfferTicket, Level = forLevel, First = choices[0],
                Second = choices.Length > 1 ? choices[1] : default, Third = choices.Length > 2 ? choices[2] : default };
        }
        offer.PendingCount = queuedLevels.Count + (offer.IsValid ? 1 : 0);
        levelUpOffer.Value = offer;
    }

    public void ChooseLevelUp(ulong ticket, int choiceIndex)
    {
        if (!IsSpawned || !IsOwner) return;
        ChooseLevelUpRpc(ticket, choiceIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ChooseLevelUpRpc(ulong ticket, int choiceIndex)
    {
        LevelUpOffer offer = levelUpOffer.Value;
        if (!offer.IsValid || offer.Ticket != ticket || choiceIndex < 0 || choiceIndex > 2) return;
        UpgradeChoice choice = offer.GetChoice(choiceIndex);
        if (!choice.IsValid) return;
        var bonuses = upgradeBonuses.Value;
        switch (choice.Stat)
        {
            case UpgradeStat.Attack: bonuses.Attack += choice.Amount; break;
            case UpgradeStat.MaxHp: bonuses.MaxHp += choice.Amount; break;
            case UpgradeStat.CriticalChance: bonuses.CriticalChance = Mathf.Clamp01(bonuses.CriticalChance + choice.Amount); break;
            case UpgradeStat.CriticalDamage: bonuses.CriticalDamage += choice.Amount; break;
            case UpgradeStat.AttackSpeed: bonuses.AttackSpeed += choice.Amount; break;
            case UpgradeStat.MoveSpeed: bonuses.MoveSpeed += choice.Amount; break;
            case UpgradeStat.JumpHeight: bonuses.JumpHeight += choice.Amount; break;
            default: return;
        }
        float oldMaxHp = MaxHp;
        upgradeBonuses.Value = bonuses;
        if (!IsDead) currentHp.Value = Mathf.Min(MaxHp, CurrentHp + Mathf.Max(0f, MaxHp - oldMaxHp));
        levelUpOffer.Value = default;
        RefreshLevelUpOffer();
        TeamProgression.For(NetworkManager).ChoiceCompleted();
    }
}
