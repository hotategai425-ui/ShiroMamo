using System.Collections.Generic;
using UnityEngine;

// サーバーだけが付与・進行する。敵と攻撃者PlayerObjectごとに1状態。
public class MonsterPoisonState : MonoBehaviour
{
    private sealed class Poison
    {
        public PlayerStats Source;
        public float Amount;
        public double NextTick;
        public double ExpiresAt;
        public float Interval;
    }

    private Monster target;
    private readonly Dictionary<ulong, Poison> poisons = new Dictionary<ulong, Poison>();
    private readonly List<ulong> expired = new List<ulong>();

    public static void Apply(Monster target, DamageContext context)
    {
        int count = context.Skills.Count(WeaponSkillId.Poison);
        var source = context.Attacker;
        if (context.Kind != DamageKind.Direct || count == 0 || target == null || !target.IsAlive || !target.IsServer
            || source == null || !source.IsSpawned || !source.IsServer || source.NetworkManager != target.NetworkManager) return;
        var settings = WeaponSkillSettings.Current;
        float amount = context.Damage.Amount * settings.PoisonRate * count;
        if (!(amount > 0f) || float.IsInfinity(amount)) return;
        var state = target.GetComponent<MonsterPoisonState>();
        if (state == null) state = target.gameObject.AddComponent<MonsterPoisonState>();
        state.target = target;
        double now = Time.timeAsDouble;
        ulong key = source.NetworkObjectId;
        if (!state.poisons.TryGetValue(key, out Poison poison) || poison.ExpiresAt < now)
        {
            poison = new Poison { NextTick = now + settings.PoisonInterval, Interval = settings.PoisonInterval };
            state.poisons[key] = poison;
        }
        poison.Source = source;
        poison.Amount = amount;
        poison.ExpiresAt = now + settings.PoisonDuration;
    }

    private void Update() => Advance(Time.timeAsDouble);

    private void Advance(double now)
    {
        if (target == null || !target.IsAlive || !target.IsServer
            || WaveManager.Instance == null || WaveManager.Instance.Phase != GamePhase.Combat)
        {
            Clear();
            return;
        }
        if (Time.timeScale == 0f) return;
        expired.Clear();
        foreach (var pair in poisons)
        {
            Poison poison = pair.Value;
            var source = poison.Source;
            if (source == null || !source.IsSpawned || !source.IsServer || source.NetworkManager != target.NetworkManager)
            {
                expired.Add(pair.Key);
                continue;
            }
            // 大きなフレーム間隔でも期限内のtickを失わない。期限と同時の最終tickも含む。
            while (poison.NextTick <= now && poison.NextTick <= poison.ExpiresAt)
            {
                poison.NextTick += poison.Interval;
                target.TryTakeDamage(DamageContext.Secondary(poison.Amount, source, DamageKind.Poison));
                // 致死DespawnではClearが呼ばれるので、その場で反復を終える。
                if (target == null || !target.IsAlive) return;
                if (Time.timeScale == 0f) return;
            }
            if (now >= poison.ExpiresAt) expired.Add(pair.Key);
        }
        foreach (ulong key in expired) poisons.Remove(key);
    }

    public void Clear() => poisons.Clear();
    private void OnDisable() => Clear();
}
