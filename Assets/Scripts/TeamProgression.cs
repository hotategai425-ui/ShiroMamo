using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 試合の共有進捗はホストが一度だけ計算し、各PlayerStatsへ同じスナップショットを送る。
public sealed class TeamProgression
{
    private static readonly Dictionary<NetworkManager, TeamProgression> sessions = new();
    private readonly NetworkManager network;
    private readonly List<PlayerStats> players = new();
    private TeamProgressionState state = new TeamProgressionState { Level = 1 };
    private int rewardWave;
    private bool rewardRoundActive;

    private TeamProgression(NetworkManager manager) { network = manager; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessions() => sessions.Clear();

    internal static TeamProgression For(NetworkManager manager)
    {
        if (manager == null || !manager.IsServer) return null;
        if (!sessions.TryGetValue(manager, out var session))
            sessions.Add(manager, session = new TeamProgression(manager));
        return session;
    }

    public static bool IsPaused(NetworkManager manager) => manager != null
        && sessions.TryGetValue(manager, out var session) && session.state.Paused;

    internal void Register(PlayerStats player)
    {
        if (players.Contains(player)) return;
        players.Add(player);
        for (int level = 2; level <= state.Level; level++) player.QueueLevelUp(level);
        if (rewardRoundActive) player.IssueWaveReward(rewardWave);
        Publish(false);
    }

    internal static void Unregister(PlayerStats player)
    {
        if (!sessions.TryGetValue(player.NetworkManager, out var session)) return;
        session.players.Remove(player);
        session.Publish(false);
        if (session.players.Count == 0) sessions.Remove(player.NetworkManager);
    }

    internal void AddExperience(float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount) || players.Count == 0) return;
        float nextExp = state.Exp + amount;
        if (float.IsInfinity(nextExp)) return;
        state.Exp = nextExp;
        bool leveled = false;
        float required = players[0].RequiredExperience(state.Level);
        while (required > 0f && !float.IsInfinity(required) && state.Exp >= required && state.Level < int.MaxValue)
        {
            state.Exp -= required;
            state.Level++;
            foreach (var player in players) player.QueueLevelUp(state.Level);
            required = players[0].RequiredExperience(state.Level);
            leveled = true;
        }
        Publish(leveled);
    }

    internal void SetLevel(int level)
    {
        int oldLevel = state.Level;
        state.Level = level;
        state.Exp = 0f;
        foreach (var player in players)
        {
            player.ClearLevelUpQueue();
            for (int earned = oldLevel + 1; earned <= level; earned++) player.QueueLevelUp(earned);
        }
        Publish(true);
    }

    internal void ChoiceCompleted() => Publish(false);

    internal void BeginWaveRewards(int completedWave)
    {
        if (completedWave <= rewardWave) return;
        rewardWave = completedWave;
        rewardRoundActive = true;
        foreach (var player in players)
            if (player != null && player.IsSpawned) player.IssueWaveReward(completedWave);
        Publish(false);
    }

    private void Publish(bool heal)
    {
        bool levels = players.Exists(player => player != null && player.PendingLevelUps > 0);
        bool rewards = players.Exists(player => player != null && player.CurrentWaveRewardOffer.IsValid);
        state.PauseReasons = (levels ? TeamPauseReason.LevelUp : TeamPauseReason.None)
            | (rewards ? TeamPauseReason.WaveReward : TeamPauseReason.None);
        state.Paused = levels || rewards;
        if (!state.Paused) rewardRoundActive = false;
        foreach (var wave in Object.FindObjectsByType<WaveManager>(FindObjectsSortMode.None))
            if (wave.IsSpawned && wave.IsServer && wave.NetworkManager == network)
            {
                wave.SetLevelUpPaused(state.Paused);
                if (!state.Paused) wave.CompleteWaveRewardSelection();
            }
        foreach (var player in players)
            if (player != null && player.IsSpawned) player.SyncTeamProgression(state, heal);
    }
}
