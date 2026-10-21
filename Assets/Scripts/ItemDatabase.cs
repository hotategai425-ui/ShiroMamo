using System;
using UnityEngine;

// 全 ItemData の一覧。通信では「何番のアイテムか」だけを送るために使う。
// ホストとクライアントで同じシーンを使うので、並び順は自動で一致する。
// 【重要】ドロップ表に入れる ItemData は、ここにも全部登録すること。
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [SerializeField] private ItemData[] items;

    private void Awake()
    {
        Instance = this;
    }

    public static ItemData Get(int index)
    {
        var db = Instance;
        if (db == null || db.items == null || index < 0 || index >= db.items.Length) return null;
        return db.items[index];
    }

    public static int IndexOf(ItemData item)
    {
        var db = Instance;
        if (db == null || db.items == null || item == null) return -1;
        return Array.IndexOf(db.items, item);
    }
}
