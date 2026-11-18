using Unity.Netcode;
using UnityEngine;

// Editor / Development Build のホスト自身だけが使う検証操作。
public class CreatorMode : MonoBehaviour
{
    // 再戦時に破棄待ちの旧プレイヤーを参照しないよう、現ホストのPlayerObjectから取得する。
    public static CreatorMode Instance
    {
        get
        {
            var network = NetworkManager.Singleton;
            if (network == null || !network.IsHost || network.LocalClient == null) return null;
            var playerObject = network.LocalClient.PlayerObject;
            return playerObject != null ? playerObject.GetComponent<CreatorMode>() : null;
        }
    }
    public PlayerStats Player { get; private set; }
    public bool IsMenuOpen { get; private set; }
    public bool MonstersPaused { get; private set; }

    public static bool SupportedBuild => Application.isEditor || Debug.isDebugBuild;
    public bool CanUse => SupportedBuild && Player != null && Player.IsSpawned && Player.IsHost && Player.IsOwner;

    public void Initialize(PlayerStats player)
    {
        if (!SupportedBuild || player == null || !player.IsSpawned || !player.IsHost || !player.IsOwner) return;
        Player = player;
    }

    public static bool CanEdit(PlayerStats player) => Instance != null && Instance.CanUse && Instance.Player == player;
    public static bool IsOpenFor(PlayerEquipment equipment) => Instance != null && Instance.CanUse
        && Instance.IsMenuOpen && Instance.Player.Equipment == equipment;
    public static bool AreMonstersPaused(NetworkManager network) => Instance != null && Instance.CanUse
        && Instance.MonstersPaused && Instance.Player.NetworkManager == network;

    public void SetMenuOpen(bool open)
    {
        IsMenuOpen = open && CanUse && !Player.IsTeamLevelUpPaused;
        if (IsMenuOpen) Player.Equipment.CloseInventory();
    }

    private void Update()
    {
        if (!CanUse) { IsMenuOpen = false; return; }
        if (Player.IsTeamLevelUpPaused) { IsMenuOpen = false; return; }
        if (GameInput.CreatorModePressed) SetMenuOpen(!IsMenuOpen);
        if (GameInput.CancelPressed) SetMenuOpen(false);
    }

    public bool TrySetLevel(int level) => CanUse && Player.TrySetCreatorLevel(level);

    public bool TryGiveWeapon(int definitionIndex, ItemRarity? rarity = null)
    {
        if (!CanUse || Player.IsDead || ItemDatabase.Instance == null) return false;
        ItemData weapon = ItemDatabase.Get(definitionIndex);
        if (weapon == null || weapon.slot != ItemSlot.Weapon) return false;
        ItemInstance instance = rarity.HasValue
            ? ItemDatabase.Instance.CreateItemForCreator(weapon, rarity.Value)
            : ItemDatabase.Instance.CreateItem(weapon);
        if (!instance.IsValid) return false;
        Player.Equipment.ReceiveItem(instance);
        return true;
    }

    public bool SetMonstersPaused(bool paused)
    {
        if (!CanUse) return false;
        MonstersPaused = paused;
        foreach (var monster in Monster.All)
            if (monster != null && monster.IsSpawned && monster.IsServer && monster.NetworkManager == Player.NetworkManager)
                monster.SetCreatorPaused(paused);
        return true;
    }

    private void OnDestroy()
    {
        var current = Instance;
        if (current != null && current != this) return;
        MonstersPaused = false;
        IsMenuOpen = false;
        foreach (var monster in Monster.All)
            if (monster != null && monster.IsSpawned && monster.IsServer) monster.SetCreatorPaused(false);
    }
}
