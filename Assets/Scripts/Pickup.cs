using System.Collections.Generic;
using UnityEngine;

// ドロップ品の共通処理。プレイヤーが近づくと吸い寄せられて取得される。
// コライダーは不要(距離判定)。
public abstract class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    [SerializeField] protected float magnetRange = 6f;
    [SerializeField] protected float magnetSpeed = 10f;
    [SerializeField] protected float collectRadius = 1.2f;
    [SerializeField] private float spinSpeed = 90f;

    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    protected virtual void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        var player = PlayerStats.Instance;
        if (player == null || player.IsDead) return;

        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        if (dist <= collectRadius)
        {
            OnCollected(player);
            Destroy(gameObject);
        }
        else if (dist <= magnetRange)
        {
            transform.position += to.normalized * magnetSpeed * Time.deltaTime;
        }
    }

    protected abstract void OnCollected(PlayerStats player);

    // 回収フェーズ後の拾い忘れを一掃
    public static void ClearAll()
    {
        foreach (var p in All.ToArray())
            if (p != null) Destroy(p.gameObject);
    }
}
