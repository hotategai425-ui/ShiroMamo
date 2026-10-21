using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// ドロップ品の共通処理。ホストが「一番近い生きているプレイヤー」に向けて吸い寄せ、
// 触れた人が取得する(早い者勝ち)。位置は NetworkTransform で全員に同期。
public abstract class Pickup : NetworkBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    [SerializeField] protected float magnetRange = 6f;
    [SerializeField] protected float magnetSpeed = 10f;
    [SerializeField] protected float collectRadius = 1.2f;
    [SerializeField] private float spinSpeed = 90f;

    private bool collected;

    public override void OnNetworkSpawn()
    {
        All.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
    }

    protected virtual void Update()
    {
        if (!IsServer || !IsSpawned || collected) return;

        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        // 一番近い生きているプレイヤーを探す
        PlayerStats nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var p in PlayerStats.All)
        {
            if (p == null || p.IsDead) continue;
            Vector3 d = p.transform.position - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = p;
            }
        }
        if (nearest == null) return;

        if (nearestDist <= collectRadius)
        {
            collected = true;
            OnCollectedServer(nearest);
            NetworkObject.Despawn(true);
        }
        else if (nearestDist <= magnetRange)
        {
            Vector3 to = nearest.transform.position - transform.position;
            to.y = 0f;
            transform.position += to.normalized * magnetSpeed * Time.deltaTime;
        }
    }

    // ホスト側で呼ばれる。拾ったプレイヤーに効果を与える
    protected abstract void OnCollectedServer(PlayerStats player);

    // 回収フェーズ後の拾い忘れを一掃(ホストのみ)
    public static void ClearAll()
    {
        foreach (var p in All.ToArray())
        {
            if (p != null && p.IsSpawned && p.IsServer) p.NetworkObject.Despawn(true);
        }
    }
}
