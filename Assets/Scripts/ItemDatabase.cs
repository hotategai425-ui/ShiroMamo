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

    public int Count => items == null ? 0 : items.Length;

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

    // 検証専用。共通データを変更せず、指定したレア度の能力設定を使う。
    public ItemInstance CreateItemForCreator(ItemData definition, ItemRarity rarity)
    {
        var mode = CreatorMode.Instance;
        var network = NetworkManager.Singleton;
        int index = IndexOf(definition);
        if (mode == null || !mode.CanUse || network == null || !network.IsServer
            || network != mode.Player.NetworkManager || index < 0 || definition.slot != ItemSlot.Weapon
            || !Enum.IsDefined(typeof(ItemRarity), rarity)) return default;

        var settings = definition.weaponRarityRolls;
        if (settings != null)
        {
            foreach (var setting in settings)
                if (setting != null && setting.rarity == rarity)
                    return ItemInstance.Create(nextInstanceId++, index, definition, setting);
        }
        // 個体抽選を持たない旧武器では、元々の固定レア度だけを許可する。
        if ((settings == null || settings.Length == 0) && definition.rarity == rarity)
            return ItemInstance.Create(nextInstanceId++, index, definition);
        return default;
    }
}
