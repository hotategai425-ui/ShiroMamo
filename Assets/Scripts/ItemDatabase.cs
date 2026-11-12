using System;
using Unity.Netcode;
using UnityEngine;

// 共通ItemDataの一覧と、ホストでの個体生成を担当する。
// ホストとクライアントで同じシーンを使うので、並び順は自動で一致する。
// 【重要】ドロップ表に入れる ItemData は、ここにも全部登録すること。
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [SerializeField] private ItemData[] items;
    private ulong nextInstanceId = 1;

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

    public ItemInstance CreateItem(ItemData definition)
    {
        var network = NetworkManager.Singleton;
        int index = IndexOf(definition);
        if (network == null || !network.IsServer || index < 0) return default;
        return ItemInstance.Create(nextInstanceId++, index, definition);
    }
}
