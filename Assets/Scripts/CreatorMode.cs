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

    public bool TrySetLevel(int level) => CanUse && !Player.IsGamePaused && Player.TrySetCreatorLevel(level);

    public bool TryGiveWeapon(int definitionIndex, ItemRarity? rarity = null)
    {
        if (!CanUse || Player.IsDead || Player.IsGamePaused || ItemDatabase.Instance == null) return false;
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
        if (!CanUse || Player.IsGamePaused) return false;
        MonstersPaused = paused;
        foreach (var monster in Monster.All)
            if (monster != null && monster.IsSpawned && monster.IsServer && monster.NetworkManager == Player.NetworkManager)
                monster.SetCreatorPaused(paused);
        return true;
    }

    public bool TrySpawnSuicideGoblin()
    {
        if (!CanUse || Player.IsDead || Player.IsTeamLevelUpPaused) return false;
        var wave = WaveManager.Instance;
        if (wave == null || !wave.IsServer || wave.NetworkManager != Player.NetworkManager || wave.Phase != GamePhase.Combat) return false;
        var fortress = SuicideGoblinController.FindFortress(Player.NetworkManager);
        var data = Resources.Load<MonsterData>("Monsters/SuicideGoblin");
        if (fortress == null || data == null || data.prefab == null) return false;
        var monster = Instantiate(data.prefab);
        monster.ServerInit(data, wave.CurrentSetting);
        var shapes = monster.GetComponentsInChildren<Collider>();
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Vector3 position = fortress.transform.position + Vector3.forward * (fortress.Radius + data.attackRange + 4f + attempt * 2f);
            bool floorFound = false;
            float floorY = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * 16f, Vector3.down, 64f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(monster.transform) || SuicideGoblinController.IsActor(hit.collider) || hit.normal.y < 0.9f) continue;
                if (hit.point.y > floorY) { floorY = hit.point.y; floorFound = true; }
            }
            if (!floorFound) continue;
            monster.transform.position = new Vector3(position.x, 0f, position.z);
            Physics.SyncTransforms();
            float bottom = 0f;
            foreach (var shape in shapes)
                if (shape.enabled && !shape.isTrigger) bottom = Mathf.Min(bottom, shape.bounds.min.y);
            monster.transform.position += Vector3.up * (floorY - bottom + 0.05f);
            Physics.SyncTransforms();
            bool blocked = false;
            foreach (var shape in shapes)
            {
                if (!shape.enabled || shape.isTrigger) continue;
                foreach (var other in Physics.OverlapBox(shape.bounds.center, shape.bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.transform.IsChildOf(monster.transform)) continue;
                    if (Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > 0.001f)
                    { blocked = true; break; }
                }
                if (blocked) break;
            }
            if (blocked) continue;
            monster.NetworkObject.Spawn(true);
            return true;
        }
        Destroy(monster.gameObject);
        return false;
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
