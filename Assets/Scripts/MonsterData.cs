using System;
using UnityEngine;

[Serializable]
public class DropEntry
{
    public ItemData item;
    public float weight = 1f;
}

// Project ウィンドウで Create > TD > Monster Data から作成
[CreateAssetMenu(menuName = "TD/Monster Data", fileName = "MonsterData")]
public class MonsterData : ScriptableObject
{
    public string displayName = "Monster";

    [Tooltip("空ならカプセルを自動生成します(仮モデル)。本番モデルを作ったらプレハブを指定")]
    public Monster prefab;

    [Header("ステータス(ウェーブ補正前)")]
    public float maxHp = 30f;
    public float moveSpeed = 3f;
    public float attackDamage = 5f;
    public float attackInterval = 1.5f;
    public float attackRange = 1.5f;
    [Tooltip("この距離以内にプレイヤーがいたらプレイヤーを狙う")]
    public float aggroRange = 4f;
    public float scale = 1f;

    [Header("出現")]
    public float spawnWeight = 1f;
    [Tooltip("このウェーブ以降に出現(1始まり)")]
    public int firstWave = 1;

    [Header("ドロップ")]
    public float expReward = 10f;
    [Range(0f, 1f)] public float itemDropChance = 0.3f;
    public DropEntry[] dropTable;

    [Header("見た目(仮モデル用)")]
    public bool applyTint = true;
    public Color tintColor = Color.red;

    public ItemData RollDrop()
    {
        if (dropTable == null || dropTable.Length == 0) return null;

        float total = 0f;
        foreach (var e in dropTable)
            if (e != null && e.item != null) total += e.weight;
        if (total <= 0f) return null;

        float r = UnityEngine.Random.value * total;
        ItemData last = null;
        foreach (var e in dropTable)
        {
            if (e == null || e.item == null) continue;
            last = e.item;
            r -= e.weight;
            if (r <= 0f) return e.item;
        }
        return last;
    }
}
